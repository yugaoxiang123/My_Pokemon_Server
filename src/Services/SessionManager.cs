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

public class Session
{
    public IChannelHandlerContext? Channel { get; set; }
    public required string PlayerId { get; set; }
    public DateTime LastActiveTime { get; set; }

    public async Task SendAsync(IMessage message)
    {
        if (Channel != null && Channel.Channel.Active)
        {
            await Channel.WriteAndFlushAsync(message);
        }
    }
}

public class SessionManager
{
    // 用于从Channel获取Session的Key
    public static readonly AttributeKey<Session> SessionKey = AttributeKey<Session>.ValueOf("Session");
    
    // 使用线程安全的字典存储会话
    private readonly ConcurrentDictionary<IChannel, Session> _sessions = new();
    
    // 创建新会话
    public Session CreateSession(IChannelHandlerContext context)
    {
        var session = new Session
        {
            Channel = context,
            PlayerId = "Player_" + Guid.NewGuid().ToString("N").AsSpan(0, 8).ToString(),
            LastActiveTime = DateTime.UtcNow
        };

        // 将Session绑定到Channel
        context.Channel.GetAttribute(SessionKey).Set(session);
        
        // 添加到会话字典
        _sessions.TryAdd(context.Channel, session);
        
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
    public Session? GetSession(IChannel channel)
    {
        _sessions.TryGetValue(channel, out var session);
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