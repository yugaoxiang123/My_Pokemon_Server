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
using ModelPosition = MyPokemon.Models.PlayerPosition;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using MoveDirection = MyPokemon.Protocol.MoveDirection;
using MotionStates = MyPokemon.Protocol.MotionStates;

public class Session
{
    // 通信通道的上下文，可以为空。用于与客户端进行网络通信
    public IChannelHandlerContext? Channel { get; set; }
    
    // 玩家的唯一标识符，使用 required 关键字表示在创建对象时必须提供该值
    public required string PlayerId { get; set; }
    
    // 记录玩家最后活跃的时间，用于检测不活跃的会话
    public DateTime LastActiveTime { get; set; }

    // 添加认证相关属性
    public bool IsAuthenticated { get; set; }
    public string? UserEmail { get; set; }
    public string? AuthToken { get; set; }

    // 异步发送消息的方法
    public async Task SendAsync(IMessage message)
    {
        if (Channel != null && Channel.Channel.Active)
        {
            if (message is PlayerPosition && !IsAuthenticated)
            {
                ServerLogger.LogError($"未认证的会话尝试发送位置更新: {PlayerId}");
                return;
            }

            var gameMessage = new Message();
            
            switch (message)
            {
                case ProtoPosition pos:
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
    
    // 缓存键前缀
    private const string POSITION_KEY = "position:";
    
    private readonly ConcurrentDictionary<IChannel, Session> _sessions = new();
    
    private readonly MapService _mapService;
    private readonly DatabaseService _db;
    private readonly IDistributedCache _cache;

    public SessionManager(MapService mapService, DatabaseService db, IDistributedCache cache)
    {
        _mapService = mapService;
        _db = db;
        _cache = cache;
    }
    
    // 创建新会话时通过参数传入 MapService
    public Session CreateSession(IChannelHandlerContext context)
    {
        var session = new Session
        {
            Channel = context,
            PlayerId = "未认证用户",  // 初始设置为未认证
            LastActiveTime = DateTime.UtcNow,
            IsAuthenticated = false
        };

        context.Channel.GetAttribute(SessionKey).Set(session);
        _sessions.TryAdd(context.Channel, session);
        
        ServerLogger.LogNetwork($"创建新会话: {session.PlayerId}");
        return session;
    }

    // 添加认证成功后的处理方法
    public async Task OnAuthenticationSuccess(Session session, string email, string token)
    {
        var user = await _db.GetUserByEmail(email);
        if (user != null)
        {
            session.IsAuthenticated = true;
            session.UserEmail = email;
            session.AuthToken = token;
            session.PlayerId = user.PlayerName;

            // 恢复到上次的位置
            float x = user.LastPositionX;
            float y = user.LastPositionY;
            MoveDirection direction = user.LastDirection;
            MotionStates motionState = MotionStates.Idle;  // 修正枚举值名称
            
            // 设置位置
            await _mapService.UpdatePosition(session.PlayerId, x, y, direction, motionState);

            // 获取附近的玩家列表
            var nearbyPlayers = await _mapService.GetNearbyPlayers(x, y, 15, session.PlayerId);
            

            // 发送初始化消息
            if (nearbyPlayers.Any())
            {
                var initialMessage = new InitialPlayersMessage
                {
                    Players = { nearbyPlayers }
                };
                ServerLogger.LogNetwork($"发送初始化消息 - 玩家: {session.PlayerId}, 附近玩家: {string.Join(", ", nearbyPlayers.Select(p => p.PlayerId))}");
                await session.SendAsync(initialMessage);
                
                // 广播新玩家加入消息
                var joinMessage = new PlayerJoinedMessage 
                { 
                    PlayerId = session.PlayerId,
                    X = x,
                    Y = y,
                    Direction = direction
                };
                await BroadcastToPlayersAsync(joinMessage, nearbyPlayers.Select(p => p.PlayerId).ToList());
            }
        }
    }

    // 移除会话
    public async Task RemoveSession(string playerId)
    {
        var sessionToRemove = _sessions.FirstOrDefault(kvp => kvp.Value.PlayerId == playerId);
        if (sessionToRemove.Key != null && _sessions.TryRemove(sessionToRemove.Key, out var session))
        {
            // 保存玩家最后位置
            await SavePlayerPosition(playerId);
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
    public async Task CleanupInactiveSessions(TimeSpan timeout)
    {
        var now = DateTime.UtcNow;
        var inactivePlayers = _sessions.Values
            .Where(s => now - s.LastActiveTime > timeout)
            .Select(s => s.PlayerId)
            .ToList();

        foreach (var playerId in inactivePlayers)
        {
            await RemoveSession(playerId);
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

    public async Task SavePlayerPosition(string playerId)
    {
        try
        {
            // 从Redis获取最后位置
            var json = await _cache.GetStringAsync(POSITION_KEY + playerId);
            if (json != null)
            {
                var position = JsonSerializer.Deserialize<ModelPosition>(json);
                if (position != null)
                {
                    // 保存到数据库
                    await _db.UpdateLastPosition(playerId, position.X, position.Y, position.Direction);
                    ServerLogger.LogPlayer($"保存玩家离线位置成功 - ID: {playerId}");
                }
            }
        }
        catch (Exception e)
        {
            ServerLogger.LogError($"保存玩家位置失败: {e.Message}", e);
        }
    }
} 