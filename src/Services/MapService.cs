namespace MyPokemon.Services;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using MyPokemon.Utils;
using System.Threading;
using System.Linq;
using ModelPosition = MyPokemon.Models.PlayerPosition;
using ProtoPosition = MyPokemon.Protocol.PlayerPosition;
using Microsoft.Extensions.Configuration;

public class MapService
{
    private readonly IDistributedCache _cache;
    private readonly IConfiguration _config;
    private SessionManager? _sessionManager;  // 改为可空
    private readonly float _viewDistance;
    
    // 缓存键前缀
    private const string POSITION_KEY = "position:";
    
    public MapService(
        IDistributedCache cache, 
        IConfiguration config)  // 移除 SessionManager 依赖
    {
        _cache = cache;
        _config = config;
        _viewDistance = config.GetValue<float>("Server:ViewDistance");
    }

    // 添加设置方法
    public void SetSessionManager(SessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }

    // 在使用 _sessionManager 的地方添加空检查
    private SessionManager SessionManager => 
        _sessionManager ?? throw new InvalidOperationException("SessionManager not initialized");

    // 更新玩家位置
    public async Task UpdatePosition(string playerId, float x, float y, int direction)
    {
        try 
        {
            // ServerLogger.LogPlayer($"开始更新位置 - ID: {playerId}");
            var position = new ModelPosition
            {
                PlayerId = playerId,
                X = x,
                Y = y,
                Direction = direction,
                LastUpdateTime = DateTime.UtcNow
            };

            // 只保存到Redis
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(position);
                // ServerLogger.LogPlayer($"准备保存到Redis - Key: {POSITION_KEY + playerId}");
                
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await _cache.SetStringAsync(
                    POSITION_KEY + playerId,
                    json,
                    new DistributedCacheEntryOptions
                    {
                        SlidingExpiration = TimeSpan.FromMinutes(5)
                    },
                    cts.Token
                ).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                ServerLogger.LogError($"Redis操作失败: {e.Message}");
            }

            // 广播位置
            await BroadcastPosition(position);
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
            var sessions = SessionManager.GetAllSessions();
            
            // ServerLogger.LogPlayer($"获取在线玩家位置 - 会话数量: {sessions.Count()}");
            
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
            await SessionManager.BroadcastToPlayersAsync(positionUpdate, receiversIds);
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