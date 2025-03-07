namespace MyPokemon;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyPokemon.Services;
using MyPokemon.Network;
using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Threading;
using System.Threading.Tasks;
using MyPokemon.Utils;
using StackExchange.Redis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        
        // 获取TCP服务器
        var tcpServer = host.Services.GetRequiredService<NettyTcpServer>();
        
        // 从配置中获取端口
        var config = host.Services.GetRequiredService<IConfiguration>();
        var port = config.GetValue<int>("Server:Port");

        // 启动TCP服务器
        await tcpServer.StartAsync(port);
        
        // 运行主机
        await host.RunAsync();
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostContext, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory())
                      .AddJsonFile("appsettings.json", optional: false)
                      .AddJsonFile($"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json", optional: true)
                      .AddEnvironmentVariables()
                      .AddCommandLine(args);
            })
            .ConfigureServices((hostContext, services) =>
            {
                ConfigureServices(hostContext, services);
            });

    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        // 添加Redis缓存
        var redisConnection = context.Configuration.GetValue<string>("Redis:ConnectionString");
        ServerLogger.LogNetwork($"Redis连接配置: {redisConnection ?? "未配置"}");

        if (string.IsNullOrEmpty(redisConnection))
        {
            redisConnection = "127.0.0.1:6379";
            ServerLogger.LogNetwork($"使用默认Redis连接: {redisConnection}");
        }

        services.AddStackExchangeRedisCache(options =>
        {
            var configOptions = new ConfigurationOptions
            {
                ConnectTimeout = 1000,    // 1秒连接超时
                SyncTimeout = 1000,       // 1秒同步超时
                AbortOnConnectFail = true,  // 改为true，这样连接失败时会立即报错
                ConnectRetry = 1,         // 只重试1次
                KeepAlive = 5,           // 每5秒发送一次心跳
                AllowAdmin = true        // 允许管理员操作
            };
            configOptions.EndPoints.Add(redisConnection);
            
            ServerLogger.LogNetwork($"配置Redis - 连接: {redisConnection}, 超时: {configOptions.ConnectTimeout}ms");
            options.ConfigurationOptions = configOptions;
        });

        // 添加Redis健康检查
        services.AddSingleton<IHostedService>(provider =>
        {
            var cache = provider.GetRequiredService<IDistributedCache>();
            return new RedisHealthCheck(cache);
        });

        // 1. 首先注册基础服务
        services.AddSingleton<DatabaseService>();
        services.AddSingleton<EmailService>();
        services.AddSingleton<AuthService>();

        // 2. 然后创建和注册核心服务
        var mapService = new MapService(
            services.BuildServiceProvider().GetRequiredService<IDistributedCache>(),
            context.Configuration
        );
        
        var sessionManager = new SessionManager(
            mapService,
            services.BuildServiceProvider().GetRequiredService<DatabaseService>(),
            services.BuildServiceProvider().GetRequiredService<IDistributedCache>()
        );
        
        mapService.SetSessionManager(sessionManager);

        // var showdownService = new ShowdownService(
        //     context.Configuration["ShowdownServer:Url"] ?? "ws://localhost:8000/showdown/websocket",
        //     sessionManager
        // );
        // 3. 注册到容器
        services.AddSingleton(mapService);
        services.AddSingleton(sessionManager);
        services.AddSingleton(provider => 
            new ShowdownService(
                provider.GetRequiredService<IConfiguration>(),
                provider.GetRequiredService<SessionManager>()
            )
        );
        services.AddSingleton<NettyTcpServer>();

        // 4. 最后配置认证
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = context.Configuration["Jwt:Issuer"],
                    ValidAudience = context.Configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            context.Configuration["Jwt:SecretKey"] ?? 
                            throw new Exception("JWT密钥未配置")
                        )
                    )
                };
            });

    }

    // Redis健康检查服务
    public class RedisHealthCheck : IHostedService
    {
        private readonly IDistributedCache _cache;
        
        public RedisHealthCheck(IDistributedCache cache)
        {
            _cache = cache;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                ServerLogger.LogNetwork("开始Redis健康检查...");
                
                // 测试Redis连接
                await _cache.SetStringAsync("health_check", "ok", 
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) }, 
                    cts.Token);
                    
                var result = await _cache.GetStringAsync("health_check", cts.Token);
                if (result != "ok")
                {
                    throw new Exception("Redis健康检查失败: 写入和读取不一致");
                }
                ServerLogger.LogNetwork("Redis健康检查通过");
            }
            catch (OperationCanceledException)
            {
                var error = "Redis健康检查超时";
                ServerLogger.LogError(error);
                throw new Exception(error);
            }
            catch (Exception e)
            {
                ServerLogger.LogError("Redis健康检查失败", e);
                throw;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
} 