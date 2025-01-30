namespace MyPokemon.Network;

using System;
using DotNetty.Transport.Channels;
using MyPokemon.Protocol;
using MyPokemon.Services;
using MyPokemon.Utils;
using DotNetty.Common.Utilities;

public class MessageRouter : ChannelHandlerAdapter
{
    private readonly SessionManager _sessionManager;
    private readonly MapService _mapService;

    public MessageRouter(SessionManager sessionManager, MapService mapService)
    {
        _sessionManager = sessionManager;
        _mapService = mapService;
    }

    public override async void ChannelRead(IChannelHandlerContext context, object message)
    {
        try 
        {
            if (message is PositionRequest positionRequest)
            {
                var session = _sessionManager.GetSession(context.Channel);
                if (session != null)
                {
                    ServerLogger.LogPlayer($"收到位置更新 - 玩家: {session.PlayerId}, 位置: ({positionRequest.X:F2}, {positionRequest.Y:F2}), 朝向: {positionRequest.Direction}");
                    await _mapService.UpdatePosition(session.PlayerId, positionRequest.X, positionRequest.Y, positionRequest.Direction);
                }
            }
            else if (message is GameMessage gameMessage)
            {
                // 处理其他类型的消息...
            }
        }
        catch (Exception e)
        {
            ServerLogger.LogError("处理消息时出错", e);
        }
        finally
        {
            ReferenceCountUtil.Release(message);
        }
    }
} 