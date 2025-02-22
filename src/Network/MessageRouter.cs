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
    private readonly AuthService _authService;
    private readonly ShowdownService _showdownService;

    public MessageRouter(SessionManager sessionManager, MapService mapService, AuthService authService, ShowdownService showdownService)
    {
        _sessionManager = sessionManager;
        _mapService = mapService;
        _authService = authService;
        _showdownService = showdownService;
    }

    public override async void ChannelRead(IChannelHandlerContext context, object message)
    {
        try 
        {
            ServerLogger.LogNetwork($"收到消息类型: {message.GetType().Name}");

            if (message is Message msg)
            {
                ServerLogger.LogNetwork($"处理消息 - 类型: {msg.Type}");
                switch (msg.Type)
                {
                    // Auth相关消息
                    case MessageType.RegisterRequest:
                        var registerRequest = msg.RegisterRequest;
                        ServerLogger.LogNetwork($"处理注册请求 - 邮箱: {registerRequest.Email}, 玩家名: {registerRequest.PlayerName}");
                        var result = await _authService.Register(
                            registerRequest.Email, 
                            registerRequest.Password,
                            registerRequest.PlayerName
                        );
                        await context.WriteAndFlushAsync(new Message
                        {
                            Type = MessageType.RegisterResponse,
                            RegisterResponse = new RegisterResponse 
                            {
                                Success = result.success,
                                Message = result.message
                            }
                        });
                        break;

                    case MessageType.LoginRequest:
                        var loginRequest = msg.LoginRequest;
                        ServerLogger.LogNetwork($"处理登录请求 - 邮箱: {loginRequest.Email}");
                        var loginResult = await _authService.Login(loginRequest.Email, loginRequest.Password);
                        ServerLogger.LogNetwork($"登录结果 - 成功: {loginResult.success}, 消息: {loginResult.message}");
                        
                        await context.WriteAndFlushAsync(new Message
                        {
                            Type = MessageType.LoginResponse,
                            LoginResponse = new LoginResponse
                            {
                                Success = loginResult.success,
                                Message = loginResult.message,
                                Token = loginResult.success ? loginResult.token : "",
                                PlayerName = loginResult.success ? loginResult.playerName : "",
                                PositionX = loginResult.success ? loginResult.positionX : 0,
                                PositionY = loginResult.success ? loginResult.positionY : 0,
                                Direction = loginResult.success ? loginResult.direction : MoveDirection.None
                            }
                        });

                        if (loginResult.success)
                        {
                            var session = _sessionManager.GetSession(context.Channel);
                            if (session != null)
                            {
                                await _sessionManager.OnAuthenticationSuccess(session, loginRequest.Email, loginResult.token!);
                                ServerLogger.LogNetwork($"认证成功 - 玩家ID: {session.PlayerId}");
                            }
                        }
                        break;

                    case MessageType.VerifyEmailRequest:
                        var verifyRequest = msg.VerifyRequest;
                        ServerLogger.LogNetwork($"处理邮箱验证请求 - 邮箱: {verifyRequest.Email}, 验证码: {verifyRequest.Code}");
                        var verifyResult = await _authService.VerifyEmail(verifyRequest.Email, verifyRequest.Code);
                        ServerLogger.LogNetwork($"验证结果 - 成功: {verifyResult.success}, 消息: {verifyResult.message}");
                        await context.WriteAndFlushAsync(new Message
                        {
                            Type = MessageType.VerifyEmailResponse,
                            VerifyResponse = new VerifyEmailResponse
                            {
                                Success = verifyResult.success,
                                Message = verifyResult.message
                            }
                        });
                        break;

                    // Game相关消息
                    case MessageType.PositionUpdate:
                        var currentSession = _sessionManager.GetSession(context.Channel);
                        if (currentSession?.IsAuthenticated == true)
                        {
                            var position = msg.PositionUpdate;
                            await _mapService.UpdatePosition(
                                currentSession.PlayerId, 
                                position.X, 
                                position.Y, 
                                position.Direction,
                                position.MotionState  // 添加运动状态
                            );
                        }
                        break;


                    // ... 其他消息处理
                }

            }
            else if (message is BattleMessage battleMsg)
            {
                var session = _sessionManager.GetSession(context.Channel);
                if (session?.IsAuthenticated != true)
                {
                    ServerLogger.LogError($"未认证的会话尝试发送对战消息");
                    return;
                }

                switch (battleMsg.Type)
                {
                    case BattleMessageType.Request:
                        var battleRequest = battleMsg.BattleRequest;
                        await _showdownService.StartBattleAsync(battleRequest);
                        break;

                    case BattleMessageType.Action:
                        var battleAction = battleMsg.BattleAction;
                        await _showdownService.SendBattleActionAsync(battleAction);
                        break;
                }
            }
        }
    
        catch (Exception e)
        {
            ServerLogger.LogError($"处理消息时出错: {e.Message}", e);
        }
        finally
        {
            ReferenceCountUtil.Release(message);
        }
    }
} 