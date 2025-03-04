using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using MyPokemon.Protocol;
using MyPokemon.Utils;
using Microsoft.Extensions.Configuration;

namespace MyPokemon.Services;

public class ShowdownService
{
    private readonly ClientWebSocket _webSocket;
    private readonly string _showdownServerUrl;
    private readonly SessionManager _sessionManager;
    private readonly Dictionary<string, string> _battleRooms = new();
    private bool _isConnected = false;
    private readonly int _reconnectInterval;
    private readonly CancellationTokenSource _cts = new();

    public ShowdownService(IConfiguration config, SessionManager sessionManager)
    {
        _webSocket = new ClientWebSocket();
        _showdownServerUrl = config["ShowdownServer:Url"] ?? "ws://localhost:8000/showdown/websocket";
        _reconnectInterval = config.GetValue<int>("ShowdownServer:ReconnectInterval", 5000);
        _sessionManager = sessionManager;
        
        // 启动重连任务
        _ = MaintainConnectionAsync(_cts.Token);
    }

    private async Task MaintainConnectionAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (!_isConnected)
                {
                    await ConnectAsync();
                }
                await Task.Delay(_reconnectInterval, token);
            }
            catch (Exception e)
            {
                ServerLogger.LogError("Showdown 服务器连接失败", e);
                _isConnected = false;
            }
        }
    }

    public async Task ConnectAsync()
    {
        try
        {
            await _webSocket.ConnectAsync(new Uri(_showdownServerUrl), CancellationToken.None);
            _isConnected = true;
            ServerLogger.LogNetwork("已连接到 Pokemon Showdown 服务器");
            
            // 启动消息接收循环
            _ = ReceiveMessagesAsync();
        }
        catch (Exception e)
        {
            ServerLogger.LogError("连接 Pokemon Showdown 服务器失败", e);
            throw;
        }
    }

    private async Task ReceiveMessagesAsync()
    {
        var buffer = new byte[4096];
        try
        {
            while (_webSocket.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await HandleShowdownMessageAsync(message);
                }
            }
        }
        catch (Exception e)
        {
            ServerLogger.LogError("接收 Showdown 消息时出错", e);
            _isConnected = false;
        }
    }

    private async Task HandleShowdownMessageAsync(string message)
    {
        try
        {
            // 解析 Showdown 消息
            var parts = message.Split('|');
            if (parts.Length < 2) return;

            var roomId = parts[0].Trim();
            var messageType = parts[1];

            switch (messageType)
            {
                case "init":
                    await HandleBattleInitAsync(roomId, parts);
                    break;
                    
                case "player":
                    await HandlePlayerInfoAsync(roomId, parts);
                    break;
                    
                case "request":
                    // 行动请求
                    await HandleBattleRequestAsync(roomId, parts[2]);
                    break;
                    
                case "move":
                    break;
                case "switch":
                    // 战斗行动
                    await HandleBattleActionAsync(roomId, messageType, parts);
                    break;
                    
                case "win":
                    // 对战结束
                    await HandleBattleEndAsync(roomId, parts[2]);
                    break;

                case "error":
                    // 处理错误消息
                    ServerLogger.LogError($"Showdown错误 - 房间: {roomId}, 消息: {parts[2]}");
                    break;

                case "updateuser":
                    // 用户更新,可以记录日志
                    ServerLogger.LogNetwork($"Showdown用户更新: {parts[2]}");
                    break;
            }
        }
        catch (Exception e)
        {
            ServerLogger.LogError($"处理 Showdown 消息时出错: {message}", e);
        }
    }

    public async Task StartBattleAsync(BattleRequest request)
    {
        try
        {
            // 构造 Showdown 格式的对战请求
            var format = request.Format switch
            {
                BattleFormat.Singles => "gen9ou",
                BattleFormat.Doubles => "gen9doublesou",
                _ => throw new ArgumentException("不支持的对战格式")
            };

            var challengeMessage = new
            {
                roomid = "lobby",
                messagetype = "challenge",
                user = request.OpponentId,
                format = format,
                team = ConvertTeamToShowdownFormat(request.Team)
            };

            await SendShowdownMessageAsync(JsonSerializer.Serialize(challengeMessage));
        }
        catch (Exception e)
        {
            ServerLogger.LogError("发起对战失败", e);
            throw;
        }
    }

    public async Task SendBattleActionAsync(BattleAction action)
    {
        try
        {
            if (!_battleRooms.TryGetValue(action.BattleId, out var roomId))
            {
                throw new Exception("找不到对应的对战房间");
            }

            string command = action.ActionType switch
            {
                ActionType.Move => $"/choose move {action.MoveIndex + 1}",
                ActionType.Switch => $"/choose switch {action.SwitchPosition + 1}",
                ActionType.Forfeit => "/forfeit",
                _ => throw new ArgumentException("不支持的行动类型")
            };

            await SendShowdownMessageAsync($"{roomId}|{command}");
        }
        catch (Exception e)
        {
            ServerLogger.LogError("发送对战行动失败", e);
            throw;
        }
    }

    private async Task SendShowdownMessageAsync(string message)
    {
        if (!_isConnected)
        {
            throw new Exception("未连接到 Showdown 服务器");
        }

        var bytes = Encoding.UTF8.GetBytes(message);
        await _webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None
        );
    }

    private string ConvertTeamToShowdownFormat(IEnumerable<Pokemon> team)
    {
        var sb = new StringBuilder();
        foreach (var pokemon in team)
        {
            // 转换为 Showdown 的队伍格式
            sb.AppendLine($"{pokemon.Species} @ {pokemon.Item}");
            sb.AppendLine($"Ability: {pokemon.Ability}");
            sb.AppendLine($"Level: {pokemon.Level}");
            sb.AppendLine($"EVs: {FormatEVs(pokemon.Evs)}");
            sb.AppendLine($"Nature: {pokemon.Nature}");
            foreach (var move in pokemon.Moves)
            {
                sb.AppendLine($"- {move}");
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private string FormatEVs(EVs evs)
    {
        var parts = new List<string>();
        if (evs.Hp > 0) parts.Add($"{evs.Hp} HP");
        if (evs.Attack > 0) parts.Add($"{evs.Attack} Atk");
        if (evs.Defense > 0) parts.Add($"{evs.Defense} Def");
        if (evs.SpecialAttack > 0) parts.Add($"{evs.SpecialAttack} SpA");
        if (evs.SpecialDefense > 0) parts.Add($"{evs.SpecialDefense} SpD");
        if (evs.Speed > 0) parts.Add($"{evs.Speed} Spe");
        return string.Join(" / ", parts);
    }

    private async Task HandleBattleRequestAsync(string roomId, string requestData)
    {
        try
        {
            var request = JsonSerializer.Deserialize<ShowdownRequest>(requestData);
            if (request == null) return;

            // 转换为我们的消息格式
            var battleUpdate = new BattleStateUpdate
            {
                BattleId = roomId,
                Active = request.Active.Select(p => new PokemonState 
                {
                    Species = p.Details,
                    CurrentHp = ParseHP(p.Condition).current,
                    MaxHp = ParseHP(p.Condition).max,
                    Status = ParseStatus(p.Condition)
                }).ToList(),
                LastActionDescription = request.RequestType
            };

            // 获取相关玩家并发送更新
            if (_battleRooms.TryGetValue(roomId, out var playerId))
            {
                var session = _sessionManager.GetAllSessions()
                    .FirstOrDefault(s => s.PlayerId == playerId);
                if (session != null)
                {
                    await session.SendAsync(new BattleMessage 
                    { 
                        Type = BattleMessageType.StateUpdate,
                        BattleStateUpdate = battleUpdate
                    });
                }
            }
        }
        catch (Exception e)
        {
            ServerLogger.LogError($"处理对战请求失败: {e.Message}", e);
        }
    }

    private async Task HandleBattleActionAsync(string roomId, string actionType, string[] messageParts)
    {
        try 
        {
            if (!_battleRooms.TryGetValue(roomId, out var playerId))
                return;

            var session = _sessionManager.GetAllSessions()
                .FirstOrDefault(s => s.PlayerId == playerId);
            if (session == null) return;

            var battleAction = new BattleAction
            {
                BattleId = roomId,
                PlayerId = playerId,
                ActionType = actionType switch
                {
                    "move" => ActionType.Move,
                    "switch" => ActionType.Switch,
                    _ => ActionType.Unknown
                }
            };

            // 解析行动详情
            if (messageParts.Length > 3)
            {
                switch (actionType)
                {
                    case "move":
                        battleAction.MoveIndex = ParseMoveIndex(messageParts[3]);
                        if (messageParts.Length > 4)
                            battleAction.TargetPosition = ParseTargetPosition(messageParts[4]);
                        break;

                    case "switch":
                        battleAction.SwitchPosition = ParseSwitchPosition(messageParts[3]);
                        break;
                }
            }

            await session.SendAsync(new BattleMessage
            {
                Type = BattleMessageType.Action,
                BattleAction = battleAction
            });
        }
        catch (Exception e)
        {
            ServerLogger.LogError($"处理对战行动失败: {e.Message}", e);
        }
    }

    private async Task HandleBattleEndAsync(string roomId, string winner)
    {
        try
        {
            if (!_battleRooms.TryGetValue(roomId, out var playerId))
                return;

            var session = _sessionManager.GetAllSessions()
                .FirstOrDefault(s => s.PlayerId == playerId);
            if (session == null) return;

            var battleResult = new BattleResult
            {
                BattleId = roomId,
                WinnerId = winner,
                LoserId = playerId != winner ? playerId : "",
                ResultDescription = $"对战结束，获胜者: {winner}"
            };

            await session.SendAsync(new BattleMessage
            {
                Type = BattleMessageType.Result,
                BattleResult = battleResult
            });

            // 清理对战房间记录
            _battleRooms.Remove(roomId);
        }
        catch (Exception e)
        {
            ServerLogger.LogError($"处理对战结束失败: {e.Message}", e);
        }
    }

    private async Task HandleBattleInitAsync(string roomId, string[] messageParts)
    {
        try
        {
            // 解析对战格式
            var format = messageParts.Length > 2 ? messageParts[2] : "gen9ou";
            
            // 创建对战响应消息
            var battleResponse = new BattleResponse
            {
                Success = true,
                BattleId = roomId,
                Message = $"对战已开始: {format}"
            };

            // 获取相关玩家并发送消息
            if (_battleRooms.TryGetValue(roomId, out var playerId))
            {
                var session = _sessionManager.GetAllSessions()
                    .FirstOrDefault(s => s.PlayerId == playerId);
                if (session != null)
                {
                    await session.SendAsync(new BattleMessage
                    {
                        Type = BattleMessageType.Response,
                        BattleResponse = battleResponse
                    });
                }
            }

            ServerLogger.LogNetwork($"对战初始化完成 - 房间: {roomId}, 格式: {format}");
        }
        catch (Exception e)
        {
            ServerLogger.LogError($"处理对战初始化失败: {e.Message}", e);
        }
    }

    private async Task HandlePlayerInfoAsync(string roomId, string[] messageParts)
    {
        try
        {
            if (messageParts.Length < 4) return;

            // 解析玩家信息
            var playerPosition = messageParts[2]; // p1 或 p2
            var playerName = messageParts[3];
            
            // 更新对战房间信息
            if (playerPosition == "p1")
            {
                if (!_battleRooms.ContainsKey(roomId))
                {
                    _battleRooms[roomId] = playerName;
                }
            }
            else if (playerPosition == "p2")
            {
                // 更新对战响应中的对手信息
                if (_battleRooms.TryGetValue(roomId, out var player1))
                {
                    var session = _sessionManager.GetAllSessions()
                        .FirstOrDefault(s => s.PlayerId == player1);
                    if (session != null)
                    {
                        var battleResponse = new BattleResponse
                        {
                            Success = true,
                            BattleId = roomId,
                            OpponentId = playerName,
                            Message = $"对手已加入: {playerName}"
                        };

                        await session.SendAsync(new BattleMessage
                        {
                            Type = BattleMessageType.Response,
                            BattleResponse = battleResponse
                        });
                    }
                }
            }

            ServerLogger.LogNetwork($"玩家信息更新 - 房间: {roomId}, 位置: {playerPosition}, 玩家: {playerName}");
        }
        catch (Exception e)
        {
            ServerLogger.LogError($"处理玩家信息失败: {e.Message}", e);
        }
    }

    // 辅助方法
    private (int current, int max) ParseHP(string condition)
    {
        try
        {
            var parts = condition.Split('/');
            if (parts.Length == 2)
            {
                var current = int.Parse(parts[0]);
                var max = int.Parse(parts[1].Split(' ')[0]);
                return (current, max);
            }
        }
        catch { }
        return (0, 0);
    }

    private List<StatusCondition> ParseStatus(string condition)
    {
        var status = new List<StatusCondition>();
        var parts = condition.Split(' ');
        if (parts.Length > 1)
        {
            foreach (var part in parts.Skip(1))
            {
                status.Add(part switch
                {
                    "brn" => StatusCondition.Burn,
                    "frz" => StatusCondition.Freeze,
                    "par" => StatusCondition.Paralysis,
                    "psn" => StatusCondition.Poison,
                    "slp" => StatusCondition.Sleep,
                    _ => StatusCondition.None
                });
            }
        }
        return status;
    }

    private int ParseMoveIndex(string moveData) =>
        int.TryParse(moveData, out var index) ? index : 0;

    private int ParseTargetPosition(string targetData) =>
        int.TryParse(targetData, out var pos) ? pos : 0;

    private int ParseSwitchPosition(string switchData) =>
        int.TryParse(switchData, out var pos) ? pos : 0;
}

// Showdown 请求数据结构
public class ShowdownRequest
{
    public bool Wait { get; set; }
    public string[] Active { get; set; } = Array.Empty<string>();
    public ShowdownSide Side { get; set; } = new();
    public string RequestType { get; set; } = string.Empty;
}

public class ShowdownSide
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ShowdownPokemon[] Pokemon { get; set; } = Array.Empty<ShowdownPokemon>();
}

public class ShowdownPokemon
{
    public string Ident { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public bool Active { get; set; }
    public ShowdownStats Stats { get; set; } = new();
    public string[] Moves { get; set; } = Array.Empty<string>();
    public string[] BaseAbility { get; set; } = Array.Empty<string>();
    public string Item { get; set; } = string.Empty;
}

public class ShowdownStats
{
    public int Atk { get; set; }
    public int Def { get; set; }
    public int Spa { get; set; }
    public int Spd { get; set; }
    public int Spe { get; set; }
} 