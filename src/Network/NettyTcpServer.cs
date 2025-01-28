namespace MyPokemon.Network;

using DotNetty.Transport.Channels;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels.Sockets;
using DotNetty.Codecs.Protobuf;
using MyPokemon.Services;
using MyPokemon.Protocol;
using MyPokemon.Utils;

public class NettyTcpServer
{
    private readonly SessionManager _sessionManager;
    private readonly IEventLoopGroup _bossGroup;
    private readonly IEventLoopGroup _workerGroup;
    private readonly MapService _mapService;
    
    public NettyTcpServer(SessionManager sessionManager, MapService mapService)
    {
        _sessionManager = sessionManager;
        _bossGroup = new MultithreadEventLoopGroup(1);
        _workerGroup = new MultithreadEventLoopGroup();
        _mapService = mapService;
    }

    public async Task StartAsync(int port)
    {
        try
        {
            var bootstrap = new ServerBootstrap();
            bootstrap.Group(_bossGroup, _workerGroup);
            bootstrap.Channel<TcpServerSocketChannel>();
            bootstrap.Option(ChannelOption.SoBacklog, 100);
            bootstrap.ChildHandler(new ActionChannelInitializer<ISocketChannel>(channel =>
            {
                var pipeline = channel.Pipeline;
                
                // 添加编解码器
                pipeline.AddLast(new ProtobufVarint32FrameDecoder());
                pipeline.AddLast(new ProtobufDecoder(PositionRequest.Parser));
                pipeline.AddLast(new ProtobufVarint32LengthFieldPrepender());
                pipeline.AddLast(new ProtobufEncoder());
                
                // 添加MessageRouter
                pipeline.AddLast(new MessageRouter(_sessionManager, _mapService));
                
                // 添加连接处理器
                pipeline.AddLast(new ServerHandler(_sessionManager));
            }));

            var boundChannel = await bootstrap.BindAsync(port);
            ServerLogger.LogNetwork($"服务器已启动，监听端口 {port}");
        }
        catch (Exception ex)
        {
            ServerLogger.LogError("服务器启动失败", ex);
            throw;
        }
    }

    private class ServerHandler : ChannelHandlerAdapter
    {
        private readonly SessionManager _sessionManager;

        public ServerHandler(SessionManager sessionManager)
        {
            _sessionManager = sessionManager;
        }

        public override void ChannelActive(IChannelHandlerContext context)
        {
            var remoteAddress = context.Channel.RemoteAddress;
            ServerLogger.LogNetwork($"新客户端连接: {remoteAddress}");
            _sessionManager.CreateSession(context);
            base.ChannelActive(context);
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            var session = context.Channel.GetAttribute(SessionManager.SessionKey).Get();
            if (session != null)
            {
                ServerLogger.LogNetwork($"客户端断开连接: {session.PlayerId}");
                _sessionManager.RemoveSession(session.PlayerId);
            }
            base.ChannelInactive(context);
        }
    }

    public async Task StopAsync()
    {
        try
        {
            await Task.WhenAll(
                _bossGroup.ShutdownGracefullyAsync(),
                _workerGroup.ShutdownGracefullyAsync()
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error stopping server: {ex.Message}");
            throw;
        }
    }
}