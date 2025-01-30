namespace MyPokemon.Services;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using MyPokemon.Protocol;
using MyPokemon.Utils;
using System.Threading;
using System.Linq;
using ModelPosition = MyPokemon.Models.PlayerPosition;
using ProtoPosition = MyPokemon.Protocol.PlayerPosition;
using Microsoft.Extensions.Configuration;

public class MapService
{
    private readonly IDistributedCache _cache;
    private readonly SessionManager _sessionManager;
    private readonly float _viewDistance;
    
    // 缓存键前缀
    private const string POSITION_KEY = "position:";
    
    public MapService(IDistributedCache cache, SessionManager sessionManager, IConfiguration config)
    {
        _cache = cache;
        _sessionManager = sessionManager;
        _viewDistance = config.GetValue<float>("Server:ViewDistance");
    }

    // 更新玩家位置
    public async Task UpdatePosition(string playerId, float x, float y, int direction)
    {
        try 
        {
            ServerLogger.LogPlayer($"开始更新位置 - ID: {playerId}");
            var position = new ModelPosition
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
                    // 键名：将前缀和玩家ID拼接，例如 "position:player123"
                    POSITION_KEY + playerId,
                    
                    // 值：将位置对象序列化成的 JSON 字符串
                    json,
                    
                    // 缓存选项配置
                    new DistributedCacheEntryOptions
                    {
                        // 滑动过期：如果 5 分钟内没有访问，这条数据就会被自动删除
                        SlidingExpiration = TimeSpan.FromMinutes(5)
                    },
                    
                    // 取消令牌：如果操作超时（在你的代码中设置为1秒），会取消操作
                    cts.Token
                    
                ).ConfigureAwait(false);  // 避免死锁，提高性能
                
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
    public async Task<List<ProtoPosition>> GetAllPositions()
    {
        try
        {
            var positions = new List<ProtoPosition>();
            var sessions = _sessionManager.GetAllSessions();
            
            ServerLogger.LogPlayer($"获取在线玩家位置 - 会话数量: {sessions.Count()}");
            
            foreach (var session in sessions)
            {
                var json = await _cache.GetStringAsync(POSITION_KEY + session.PlayerId);
                
                if (json != null)
                {
                    var position = System.Text.Json.JsonSerializer.Deserialize<ModelPosition>(json);
                    if (position != null)
                    {
                        positions.Add(new ProtoPosition
                        {
                            PlayerId = position.PlayerId,
                            X = position.X,
                            Y = position.Y,
                            Direction = position.Direction,
                            LastUpdateTime = ((DateTimeOffset)position.LastUpdateTime).ToUnixTimeSeconds()
                        });
                    }
                }
            }

            ServerLogger.LogPlayer($"获取位置完成 - 返回 {positions.Count} 个有效位置");
            return positions;
        }
        catch (Exception e)
        {
            ServerLogger.LogError($"获取位置失败: {e.Message}", e);
            throw;
        }
    }

    // 广播位置更新
    private async Task BroadcastPosition(ModelPosition position)
    {
        try
        {
            var nearbyPositions = await GetNearbyPlayers(position.X, position.Y, _viewDistance,position.PlayerId);
            // nearbyPositions = nearbyPositions.Where(p => p.PlayerId != position.PlayerId).ToList();

            if (nearbyPositions.Count == 0)
            {
                ServerLogger.LogNetwork($"玩家 {position.PlayerId} 附近没有其他玩家");
                return;
            }

            var positionUpdate = new ProtoPosition
            {
                PlayerId = position.PlayerId,
                X = position.X,
                Y = position.Y,
                Direction = position.Direction,
                LastUpdateTime = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds()
            };

            var receiversIds = nearbyPositions.Select(p => p.PlayerId).ToList();
            ServerLogger.LogNetwork($"广播位置 - 玩家: {position.PlayerId}, 接收者: {receiversIds.Count}人");
            await _sessionManager.BroadcastToPlayersAsync(positionUpdate, receiversIds);
        }
        catch (Exception e)
        {
            ServerLogger.LogError($"广播位置失败: {e.Message}", e);
            throw;
        }
    }

    private bool IsInViewRange(float x1, float y1, float x2, float y2)
    {
        return Math.Abs(x1 - x2) < _viewDistance && 
               Math.Abs(y1 - y2) < _viewDistance;
    }

    public async Task<List<ProtoPosition>> GetNearbyPlayers(float x, float y, float radius, string excludePlayerId)
    {
        var allPositions = await GetAllPositions();
        return allPositions.Where(p => 
            p.PlayerId != excludePlayerId && 
            Math.Sqrt(Math.Pow(p.X - x, 2) + Math.Pow(p.Y - y, 2)) <= _viewDistance
        ).ToList();
    }
}