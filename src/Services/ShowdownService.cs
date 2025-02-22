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
                    // 房间初始化
                    break;
                    
                case "player":
                    // 玩家信息
                    break;
                    
                case "request":
                    // 行动请求
                    await HandleBattleRequestAsync(roomId, parts[2]);
                    break;
                    
                case "move":
                case "switch":
                    // 战斗行动
                    await HandleBattleActionAsync(roomId, messageType, parts);
                    break;
                    
                case "win":
                    // 对战结束
                    await HandleBattleEndAsync(roomId, parts[2]);
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
            // 转换为我们的消息格式并发送给客户端
            var battleUpdate = new BattleStateUpdate
            {
                BattleId = roomId,
                // ... 转换状态信息 ...
            };

            // 获取相关玩家的会话并发送更新
            if (_battleRooms.TryGetValue(roomId, out var playerId))
            {
                var session = _sessionManager.GetAllSessions()
                    .FirstOrDefault(s => s.PlayerId == playerId);
                if (session != null)
                {
                    await session.SendAsync(battleUpdate);
                }
            }
        }
        catch (Exception e)
        {
            ServerLogger.LogError("处理对战请求失败", e);
        }
    }

    private async Task HandleBattleActionAsync(string roomId, string actionType, string[] messageParts)
    {
        // 处理对战行动并通知客户端
    }

    private async Task HandleBattleEndAsync(string roomId, string winner)
    {
        // 处理对战结束并通知客户端
    }
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