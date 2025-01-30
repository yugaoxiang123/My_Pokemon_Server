namespace MyPokemon.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using DotNetty.Transport.Channels;
using Google.Protobuf;
using DotNetty.Common.Utilities;
using MyPokemon.Utils;
using MyPokemon.Protocol;
using ProtoPosition = MyPokemon.Protocol.PlayerPosition;

public class Session
{
    // 通信通道的上下文，可以为空。用于与客户端进行网络通信
    public IChannelHandlerContext? Channel { get; set; }
    
    // 玩家的唯一标识符，使用 required 关键字表示在创建对象时必须提供该值
    public required string PlayerId { get; set; }
    
    // 记录玩家最后活跃的时间，用于检测不活跃的会话
    public DateTime LastActiveTime { get; set; }

    // 异步发送消息的方法
    public async Task SendAsync(IMessage message)
    {
        if (Channel != null && Channel.Channel.Active)
        {
            var gameMessage = new GameMessage();
            
            switch (message)
            {
                case PlayerPosition pos:
                    gameMessage.Type = MessageType.PositionUpdate;
                    gameMessage.PositionUpdate = pos;
                    break;
                    
                case PlayerJoinedMessage join:
                    gameMessage.Type = MessageType.PlayerJoined;
                    gameMessage.PlayerJoined = join;
                    break;
                    
                case PlayerLeftMessage left:
                    gameMessage.Type = MessageType.PlayerLeft;
                    gameMessage.PlayerLeft = left;
                    break;
                    
                case InitialPlayersMessage initial:
                    gameMessage.Type = MessageType.InitialPlayers;
                    gameMessage.InitialPlayers = initial;
                    break;
            }
            
            await Channel.WriteAndFlushAsync(gameMessage);
        }
    }
}

public class SessionManager
{
    // 用于从Channel获取Session的Key
    public static readonly AttributeKey<Session> SessionKey = AttributeKey<Session>.ValueOf("Session");
    
    // 使用线程安全的字典存储会话
    private readonly ConcurrentDictionary<IChannel, Session> _sessions = new();
    
    public SessionManager()
    {
    }
    
    // 创建新会话时通过参数传入 MapService
    public async Task<Session> CreateSession(IChannelHandlerContext context, MapService mapService)
    {
        var session = new Session
        {
            Channel = context,
            PlayerId = "Player_" + Guid.NewGuid().ToString("N").AsSpan(0, 8).ToString(),
            LastActiveTime = DateTime.UtcNow
        };

        context.Channel.GetAttribute(SessionKey).Set(session);
        _sessions.TryAdd(context.Channel, session);
                
        // 1. 先设置新玩家的初始位置
        await mapService.UpdatePosition(session.PlayerId, 0, 0, 0);

        // 2. 创建新玩家加入的消息
        var joinMessage = new PlayerJoinedMessage 
        { 
            PlayerId = session.PlayerId,
            X = 0,
            Y = 0,
            Direction = 0
        };

        // 3. 获取附近的玩家列表
        var nearbyPlayers = await mapService.GetNearbyPlayers(0, 0, 15, session.PlayerId);
        var initialMessage = new InitialPlayersMessage
        {
            Players = { } 
        };

        // 4. 处理附近的玩家
        foreach(var player in nearbyPlayers)
        {
            initialMessage.Players.Add(new ProtoPosition
            {
                PlayerId = player.PlayerId,
                X = player.X,
                Y = player.Y,
                Direction = player.Direction,
                LastUpdateTime = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds()
            });
        }

        try 
        {
            // 5. 只在有玩家时发送初始化消息
            if (initialMessage.Players.Count > 0)
            {
                ServerLogger.LogPlayer($"发送初始化消息，玩家数量: {initialMessage.Players.Count}");
                await session.SendAsync(initialMessage);
            }
            else
            {
                ServerLogger.LogPlayer("没有其他在线玩家，跳过发送初始化消息");
            }

            // 6. 广播新玩家加入消息
            if (nearbyPlayers.Any())
            {
                ServerLogger.LogPlayer($"广播新玩家加入消息给 {nearbyPlayers.Count} 个玩家");
                await BroadcastToPlayersAsync(joinMessage, nearbyPlayers.Select(p => p.PlayerId).ToList());
            }
        }
        catch (Exception e)
        {
            ServerLogger.LogError($"创建会话时发送消息失败: {e.Message}");
            throw;
        }

        ServerLogger.LogPlayer($"创建新会话 - 玩家ID: {session.PlayerId}");
        return session;
    }

    // 移除会话
    public void RemoveSession(string playerId)
    {
        var sessionToRemove = _sessions.FirstOrDefault(kvp => kvp.Value.PlayerId == playerId);
        if (sessionToRemove.Key != null && _sessions.TryRemove(sessionToRemove.Key, out var session))
        {
            ServerLogger.LogPlayer($"移除会话 - 玩家ID: {playerId}");
        }
    }

    // 获取会话
    // 方法定义：接受一个 IChannel 参数，返回类型是 Session?（可空的 Session）
    public Session? GetSession(IChannel channel)
    {
        // 尝试从 _sessions 字典中获取与指定 channel 关联的 Session
        // TryGetValue 是字典的一个方法，有两个参数：
        // 1. channel: 要查找的键
        // 2. out var session: 如果找到了，将值存储在 session 变量中
        _sessions.TryGetValue(channel, out var session);
        
        // 返回找到的 session（如果没找到则返回 null）
        return session;
    }

    // 获取所有会话
    public IEnumerable<Session> GetAllSessions()
    {
        return _sessions.Values;
    }

    // 广播消息给所有会话
    public async Task BroadcastAsync(IMessage message, string? excludePlayerId = null)
    {
        try
        {
            foreach (var session in _sessions.Values)
            {
                if (session.PlayerId != excludePlayerId)
                {
                    try
                    {
                        await session.SendAsync(message);
                    }
                    catch (Exception e)
                    {
                        ServerLogger.LogError($"向玩家 {session.PlayerId} 广播消息失败", e);
                    }
                }
            }
        }
        catch (Exception e)
        {
            ServerLogger.LogError("广播消息时出错", e);
        }
    }

    // 更新会话活跃时间
    public void UpdateSessionActivity(string playerId)
    {
        var session = _sessions.Values.FirstOrDefault(s => s.PlayerId == playerId);
        if (session != null)
        {
            session.LastActiveTime = DateTime.UtcNow;
        }
    }

    // 清理不活跃的会话
    public void CleanupInactiveSessions(TimeSpan timeout)
    {
        var now = DateTime.UtcNow;
        var inactivePlayers = _sessions.Values
            .Where(s => now - s.LastActiveTime > timeout)
            .Select(s => s.PlayerId)
            .ToList();

        foreach (var playerId in inactivePlayers)
        {
            RemoveSession(playerId);
        }
    }

    // 向指定玩家列表广播消息
    public async Task BroadcastToPlayersAsync(IMessage message, List<string> playerIds)
    {
        try
        {
            var tasks = new List<Task>();
            foreach (var session in _sessions.Values)
            {
                if (playerIds.Contains(session.PlayerId))
                {
                    tasks.Add(session.SendAsync(message));
                }
            }
            await Task.WhenAll(tasks);
        }
        catch (Exception e)
        {
            ServerLogger.LogError("广播消息时出错", e);
        }
    }
} 