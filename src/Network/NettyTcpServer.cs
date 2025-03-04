// 定义命名空间
namespace MyPokemon.Network;

// 引入需要的命名空间
using DotNetty.Transport.Channels;        // DotNetty通道相关
using DotNetty.Transport.Bootstrapping;   // 服务器启动相关
using DotNetty.Transport.Channels.Sockets;// TCP socket相关
using DotNetty.Codecs.Protobuf;          // Protobuf编解码器
using MyPokemon.Services;                 // 自定义服务
using MyPokemon.Protocol;                 // 协议消息
using MyPokemon.Utils;                    // 工具类

// TCP服务器主类
public class NettyTcpServer
{
    // 会话管理器，管理所有客户端连接
    private readonly SessionManager _sessionManager;
    // 主线程组，用于接受新连接
    private readonly MultithreadEventLoopGroup _bossGroup;
    // 工作线程组，用于处理已建立连接的数据
    private readonly MultithreadEventLoopGroup _workerGroup;
    // 地图服务，处理位置相关逻辑
    private readonly MapService _mapService;
    // 认证服务，处理认证相关逻辑
    private readonly AuthService _authService;

    private readonly ShowdownService _showdownService;
    // 构造函数，注入依赖
    public NettyTcpServer(SessionManager sessionManager, MapService mapService, AuthService authService, ShowdownService showdownService)
    {
        _sessionManager = sessionManager;
        // 创建一个单线程的主线程组
        _bossGroup = new MultithreadEventLoopGroup(1);
        // 创建工作线程组，线程数默认为CPU核心数*2
        _workerGroup = new MultithreadEventLoopGroup();
        _mapService = mapService;
        _authService = authService;
        _showdownService = showdownService;
    }

    // 启动服务器方法
    public async Task StartAsync(int port)
    {
        try
        {
            // 创建服务器启动器
            var bootstrap = new ServerBootstrap();
            // 设置线程组
            bootstrap.Group(_bossGroup, _workerGroup);
            // 指定使用TCP通道
            bootstrap.Channel<TcpServerSocketChannel>();
            // 设置TCP连接队列的大小
            bootstrap.Option(ChannelOption.SoBacklog, 100);
            
            // 配置每个新连接的处理管道
            bootstrap.ChildHandler(new ActionChannelInitializer<ISocketChannel>(channel =>
            {
                var pipeline = channel.Pipeline;
                
                pipeline.AddLast(new ProtobufVarint32FrameDecoder());     // 1.解析消息长度
                
                // 使用统一的消息解码器
                pipeline.AddLast(new ProtobufDecoder(Message.Parser));  // 2.消息解码
                // 使用统一的消息编码器是有bug的*******************************
                pipeline.AddLast(new ProtobufVarint32LengthFieldPrepender()); // 3.添加消息长度
                pipeline.AddLast(new ProtobufEncoder());                      // 4.编码消息体
                
                pipeline.AddLast(new MessageRouter(_sessionManager, _mapService, _authService, _showdownService));
                pipeline.AddLast(new ServerHandler(_sessionManager, _mapService));
            }));

            // 绑定端口并启动服务器
            var boundChannel = await bootstrap.BindAsync(port);
            ServerLogger.LogNetwork($"服务器已启动，监听端口 {port}");
        }
        catch (Exception ex)
        {
            ServerLogger.LogError("服务器启动失败", ex);
            throw;
        }
    }

    // 处理连接生命周期的处理器类
    private class ServerHandler : ChannelHandlerAdapter
    {
        private readonly SessionManager _sessionManager;
        private readonly MapService _mapService;

        public ServerHandler(SessionManager sessionManager, MapService mapService)
        {
            _sessionManager = sessionManager;
            _mapService = mapService;
        }

        public override void ChannelActive(IChannelHandlerContext context)
        {
            ServerLogger.LogNetwork($"新客户端连接: {context.Channel.RemoteAddress}");
            _sessionManager.CreateSession(context);
            base.ChannelActive(context);
        }

        public override async void ChannelInactive(IChannelHandlerContext context)
        {
            var session = context.Channel.GetAttribute(SessionManager.SessionKey).Get();
            if (session != null)
            {
                ServerLogger.LogNetwork($"客户端断开连接: {session.PlayerId}");
                
                if (session.IsAuthenticated)
                {
                    // 只有认证过的玩家才广播离开消息
                    await _sessionManager.BroadcastAsync(new PlayerLeftMessage 
                    { 
                        PlayerId = session.PlayerId 
                    });
                }
                
               await _sessionManager.RemoveSession(session.PlayerId);
            }
            base.ChannelInactive(context);
        }
    }

    // 优雅关闭服务器的方法
    public async Task StopAsync()
    {
        try
        {
            // 等待所有线程组优雅关闭
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