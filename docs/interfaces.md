# Pokemon 服务端接口说明文档

## 1. DotNetty 相关接口

### IChannelHandlerContext
- **用途**: 提供对 Channel 管道操作的上下文环境
- **主要功能**:
  - 消息写入和发送: `WriteAndFlushAsync()`
  - 获取通道: `Channel`
  - 关闭连接: `CloseAsync()`
  - 触发事件: `FireChannelRead()`
- **使用场景**:
  ```csharp
  // 在消息处理器中发送响应
  await context.WriteAndFlushAsync(response);
  
  // 获取远程地址
  var remoteAddress = context.Channel.RemoteAddress;
  
  // 关闭连接
  await context.CloseAsync();
  ```

### IChannel
- **用途**: 表示一个网络连接通道
- **主要功能**:
  - 检查连接状态: `Active`
  - 获取通道配置: `Configuration`
  - 获取远程地址: `RemoteAddress`
- **使用场景**:
  ```csharp
  // 检查连接是否活跃
  if (channel.Active) {
      // 处理连接
  }
  ```

## 2. 依赖注入相关接口

### IServiceCollection
- **用途**: 用于注册依赖注入服务
- **主要功能**:
  - 注册单例服务: `AddSingleton()`
  - 注册作用域服务: `AddScoped()`
  - 注册瞬态服务: `AddTransient()`
- **使用场景**:
  ```csharp
  // 在 Program.cs 中注册服务
  services.AddSingleton<MapService>();
  services.AddSingleton<SessionManager>();
  services.AddSingleton<AuthService>();
  ```

### IConfiguration
- **用途**: 访问配置信息
- **主要功能**:
  - 读取配置值: `GetValue<T>()`
  - 获取配置节: `GetSection()`
  - 绑定配置: `Bind()`
- **使用场景**:
  ```csharp
  // 读取配置
  var port = config.GetValue<int>("Server:Port");
  var redisConn = config["Redis:ConnectionString"];
  ```

## 3. 缓存相关接口

### IDistributedCache
- **用途**: 分布式缓存操作
- **主要功能**:
  - 存储数据: `SetStringAsync()`
  - 读取数据: `GetStringAsync()`
  - 删除数据: `RemoveAsync()`
- **使用场景**:
  ```csharp
  // 存储验证码
  await _cache.SetStringAsync(
      key: $"verify:{email}",
      value: code,
      options: new DistributedCacheEntryOptions
      {
          AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
      }
  );
  ```

## 4. 托管服务相关接口

### IHostedService
- **用途**: 实现后台服务
- **主要功能**:
  - 启动服务: `StartAsync()`
  - 停止服务: `StopAsync()`
- **使用场景**:
  ```csharp
  // Redis 健康检查服务
  public class RedisHealthCheck : IHostedService
  {
      public async Task StartAsync(CancellationToken cancellationToken)
      {
          // 检查 Redis 连接
      }

      public Task StopAsync(CancellationToken cancellationToken)
      {
          // 清理资源
          return Task.CompletedTask;
      }
  }
  ```

## 5. 消息处理相关接口

### IMessage (Google.Protobuf)
- **用途**: Protobuf 消息接口
- **主要功能**:
  - 序列化: `ToByteArray()`
  - 消息描述: `Descriptor`
- **使用场景**:
  ```csharp
  // 发送消息
  if (message is IMessage protoMessage)
  {
      await channel.WriteAndFlushAsync(protoMessage);
  }
  ```

## 6. 数据库相关接口

### IDbConnection (Dapper)
- **用途**: 数据库连接
- **主要功能**:
  - 执行查询: `QueryAsync<T>()`
  - 执行命令: `ExecuteAsync()`
  - 事务管理: `BeginTransaction()`
- **使用场景**:
  ```csharp
  using var conn = CreateConnection();
  var user = await conn.QueryFirstOrDefaultAsync<User>(
      "SELECT * FROM users WHERE email = @Email",
      new { Email = email }
  );
  ```

## 7. 最佳实践

### 接口使用建议
1. **依赖注入**:
   - 通过构造函数注入接口
   - 避免服务定位器模式
   - 使用适当的生命周期

2. **异步操作**:
   - 使用异步方法
   - 正确处理取消令牌
   - 避免阻塞操作

3. **资源管理**:
   - 使用 using 语句
   - 实现 IDisposable
   - 及时释放资源

4. **错误处理**:
   - 使用 try-catch 块
   - 记录异常信息
   - 返回适当的错误响应

### 示例代码
```csharp
public class ExampleService
{
    private readonly IDistributedCache _cache;
    private readonly IConfiguration _config;
    private readonly ILogger<ExampleService> _logger;

    public ExampleService(
        IDistributedCache cache,
        IConfiguration config,
        ILogger<ExampleService> logger)
    {
        _cache = cache;
        _config = config;
        _logger = logger;
    }

    public async Task<Result> ProcessAsync(
        string key, 
        CancellationToken cancellationToken)
    {
        try
        {
            var value = await _cache.GetStringAsync(key, cancellationToken);
            if (value == null)
            {
                _logger.LogWarning("Key not found: {Key}", key);
                return Result.NotFound();
            }

            // 处理数据...
            return Result.Success(value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing key: {Key}", key);
            return Result.Error(ex.Message);
        }
    }
} 