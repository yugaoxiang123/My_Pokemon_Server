namespace MyPokemon.Services;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using MyPokemon.Protocol;
using MyPokemon.Utils;
using System.Threading;
using System.Linq;

public class MapService
{
    private readonly IDistributedCache _cache;
    private readonly SessionManager _sessionManager;
    
    // 缓存键前缀
    private const string POSITION_KEY = "position:";
    
    public MapService(IDistributedCache cache, SessionManager sessionManager)
    {
        _cache = cache;
        _sessionManager = sessionManager;
    }

    // 更新玩家位置
    public async Task UpdatePosition(string playerId, float x, float y, int direction)
    {
        try 
        {
            ServerLogger.LogPlayer($"开始更新位置 - ID: {playerId}");
            var position = new PlayerPosition
            {
                PlayerId = playerId,
                X = x,
                Y = y,
                Direction = direction,
                LastUpdateTime = DateTime.UtcNow
            };

            // 先同步保存到Redis，确保后续能读取到
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(position);
                ServerLogger.LogPlayer($"准备保存到Redis - Key: {POSITION_KEY + playerId}");
                
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await _cache.SetStringAsync(
                    POSITION_KEY + playerId,
                    json,
                    new DistributedCacheEntryOptions
                    {
                        SlidingExpiration = TimeSpan.FromMinutes(5)
                    },
                    cts.Token
                ).ConfigureAwait(false);  // 添加 ConfigureAwait(false)
                
                // 验证写入是否成功
                var savedJson = await _cache.GetStringAsync(POSITION_KEY + playerId, cts.Token);
                if (savedJson == json)
                {
                    ServerLogger.LogPlayer("Redis保存并验证成功");
                }
                else
                {
                    throw new Exception("Redis写入验证失败");
                }
            }
            catch (Exception e)
            {
                ServerLogger.LogError($"Redis操作失败: {e.Message}");
                // 继续执行，不要让Redis问题影响游戏体验
            }

            // 然后广播位置
            ServerLogger.LogPlayer($"开始广播位置 - ID: {playerId}");
            await BroadcastPosition(position).ConfigureAwait(false);
            ServerLogger.LogPlayer($"位置更新完成 - ID: {playerId}");
        }
        catch (Exception e)
        {
            ServerLogger.LogError($"更新位置失败: {e.Message}", e);
        }
    }

    // 获取同一地图的所有玩家位置
    public async Task<List<PlayerPosition>> GetAllPositions()
    {
        try
        {
            var positions = new List<PlayerPosition>();
            var sessions = _sessionManager.GetAllSessions();
            ServerLogger.LogPlayer($"GetAllPositions - 获取在线会话数量: {sessions.Count()}");
            
            foreach(var session in sessions)
            {
                try
                {
                    ServerLogger.LogPlayer($"GetAllPositions - 正在获取玩家 {session.PlayerId} 的位置");
                    var positionJson = await _cache.GetStringAsync(POSITION_KEY + session.PlayerId);
                    ServerLogger.LogPlayer($"GetAllPositions - 获取到位置JSON: {positionJson ?? "null"}");
                    
                    if(!string.IsNullOrEmpty(positionJson))
                    {
                        var position = System.Text.Json.JsonSerializer.Deserialize<PlayerPosition>(positionJson);
                        if(position != null)
                        {
                            var timeSinceUpdate = (DateTime.UtcNow - position.LastUpdateTime).TotalSeconds;
                            ServerLogger.LogPlayer($"GetAllPositions - 玩家 {position.PlayerId} 位置更新时间: {timeSinceUpdate:F1}秒前");
                            
                            if(timeSinceUpdate < 5)
                            {
                                positions.Add(position);
                                ServerLogger.LogPlayer($"GetAllPositions - 添加有效位置 - ID: {position.PlayerId}");
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    ServerLogger.LogError($"GetAllPositions - 处理玩家 {session.PlayerId} 时出错: {e.Message}", e);
                }
            }
            
            ServerLogger.LogPlayer($"GetAllPositions - 完成，返回 {positions.Count} 个位置");
            return positions;
        }
        catch (Exception e)
        {
            ServerLogger.LogError($"GetAllPositions - 致命错误: {e.Message}", e);
            throw;
        }
    }

    // 广播位置更新
    private async Task BroadcastPosition(PlayerPosition position)
    {
        try
        {
            ServerLogger.LogPlayer($"BroadcastPosition - 开始处理玩家 {position.PlayerId} 的位置广播");
            var positions = await GetAllPositions();
            
            // 按区域分组广播
            var nearbyPositions = positions.Where(p => 
                Math.Abs(p.X - position.X) < 10 && // 10是视野范围，可以调整
                Math.Abs(p.Y - position.Y) < 10 && 
                p.PlayerId != position.PlayerId
            ).ToList();

            if (nearbyPositions.Count == 0)
            {
                ServerLogger.LogNetwork("BroadcastPosition - 附近没有其他玩家，跳过广播");
                return;
            }

            var broadcast = new PositionBroadcast();
            broadcast.Positions.Add(new PositionBroadcast.Types.PlayerPosition
            {
                PlayerId = position.PlayerId,
                X = position.X,
                Y = position.Y,
                Direction = position.Direction
            });

            // 获取需要接收广播的玩家ID列表
            var receiversIds = nearbyPositions.Select(p => p.PlayerId).ToList();
            
            ServerLogger.LogNetwork($"BroadcastPosition - 向 {receiversIds.Count} 个附近玩家发送位置更新");
            await _sessionManager.BroadcastToPlayersAsync(broadcast, receiversIds);
        }
        catch (Exception e)
        {
            ServerLogger.LogError($"BroadcastPosition - 广播失败: {e.Message}", e);
            throw;
        }
    }
}