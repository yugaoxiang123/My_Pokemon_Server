# Pokemon 多人在线项目说明文档

## 1. 项目概述
这是一个基于.NET和Unity的多人在线Pokemon游戏项目。服务端使用DotNetty处理网络通信，集成了Pokemon Showdown对战系统，使用PostgreSQL存储用户数据，Redis缓存游戏数据，Protobuf进行消息序列化，支持邮箱认证的用户系统。

## 2. 项目结构

### 2.1 服务端 (my-pokemon/src/)
```
src/
├── Program.cs                # 程序入口,配置依赖注入和服务启动
├── MyPokemon.csproj         # 项目文件,管理NuGet包引用
├── appsettings.json         # 配置文件
│
├── Network/                 # 网络层
│   ├── NettyTcpServer.cs    # TCP服务器
│   ├── MessageRouter.cs     # 消息路由
│   └── AuthHandler.cs       # 认证中间件
│
├── Services/                # 业务服务层
│   ├── MapService.cs        # 位置同步服务
│   ├── SessionManager.cs    # 会话管理
│   ├── AuthService.cs       # 认证服务
│   ├── EmailService.cs      # 邮件服务
│   ├── ShowdownService.cs   # 对战服务
│   └── DatabaseService.cs   # 数据库服务
│
├── Models/                 # 数据模型
│   ├── PlayerPosition.cs    # 玩家位置模型
│   └── User.cs             # 用户模型
│
└── Protos/                # 协议定义
    ├── Protocol.proto      # 统一消息协议定义
    └── Battle.proto        # 对战消息协议定义
```

## 3. 核心功能

### 3.1 消息系统
- 统一的消息类型
  - Auth相关消息 (1-100)
    - 注册请求/响应
    - 登录请求/响应
    - 邮箱验证请求/响应
  - Game相关消息 (101-200)
    - 位置更新
    - 玩家加入/离开
    - 初始化玩家列表
  - Battle相关消息
    - 对战请求/响应
    - 行动选择
    - 状态更新
    - 对战结果

### 3.2 位置同步系统
- 实时位置更新
  - 坐标(X,Y)
  - 朝向(Direction)
  - 时间戳
- 范围广播机制
  - 视野范围内广播
  - 新玩家加入通知
  - 玩家离开通知
- Redis位置缓存
- 断线重连处理

### 3.3 对战系统
- 集成 Pokemon Showdown
  - WebSocket 连接管理
  - 消息格式转换
  - 状态同步
- 对战功能
  - 匹配对战
  - 指定对手对战
  - 多种对战格式
- 对战数据
  - 宝可梦配置
  - 技能系统
  - 状态效果
- 断线重连
  - 对战状态保持
  - 自动重连机制
  - 超时处理

## 4. 关键配置

### appsettings.json
```json
{
  "Server": {
    "Port": 5000,
    "ViewDistance": 15
  },
  "ShowdownServer": {
    "Url": "ws://localhost:8000/showdown/websocket",
    "ReconnectInterval": 5000,
    "HeartbeatInterval": 30000
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "Email": {
    "SmtpHost": "smtp.qq.com",
    "SmtpPort": 587,
    "Username": "your-email@qq.com",
    "Password": "your-smtp-password",
    "FromAddress": "your-email@qq.com"
  },
  "Jwt": {
    "SecretKey": "your-very-long-secret-key-at-least-32-bytes",
    "Issuer": "pokemon-game",
    "Audience": "pokemon-players",
    "ExpiryDays": 7
  },
  "Database": {
    "ConnectionString": "Host=localhost;Database=pokemon;Username=pokemon_user;Password=your_password"
  }
}
```

## 5. 主要流程

### 5.1 用户认证流程
1. 用户注册
   - 发送 RegisterRequest 消息
   - 检查邮箱是否已注册
   - 创建用户记录到数据库
   - 生成验证码并发送邮件
2. 邮箱验证
   - 发送 VerifyEmailRequest 消息
   - 验证验证码有效性
   - 更新数据库验证状态
3. 用户登录
   - 发送 LoginRequest 消息
   - 验证密码正确性
   - 检查邮箱验证状态
   - 更新最后登录时间
   - 生成并返回JWT令牌
4. 游戏消息认证
   - 请求携带JWT令牌
   - AuthHandler验证令牌
   - 未认证请求会被拒绝

### 5.2 游戏流程
1. 认证成功后获取初始位置
2. 接收附近玩家信息
3. 定期发送位置更新（需要认证）
4. 接收位置广播
5. 断开时清理数据

### 5.3 对战流程
1. 发起对战
   - 发送对战请求
   - 等待对手匹配/接受
   - 初始化对战房间
2. 对战进行
   - 选择行动
   - 执行行动
   - 状态更新
3. 对战结束
   - 结算结果
   - 清理资源
   - 更新战绩

## 6. 安全机制

### 6.1 认证安全
- 密码加密存储
  - 使用SHA256加密
  - 密码从不明文传输
- JWT令牌验证
  - 包含用户标识信息
  - 签名防篡改
  - 过期自动失效
- 邮箱验证
  - 验证码时效控制
  - 防止重复发送
  - 验证码长度保证
- 会话状态检查
  - 认证中间件拦截
  - 会话状态维护
  - 异常行为检测

### 6.2 游戏安全
- 位置合法性验证
- 未认证限制
- 消息频率限制
- 异常行为检测

### 6.3 对战安全
- 行动合法性验证
- 超时控制
- 断线保护
- 作弊检测

## 7. 调试指南

### 7.1 服务端调试
- 认证流程日志
- Redis数据查看
- JWT令牌验证
- 会话状态检查

### 7.2 性能监控
- 连接数监控
- 消息吞吐量
- Redis性能
- 内存使用情况

### 7.3 对战调试
- 对战日志查看
- WebSocket连接状态
- 消息转换验证
- 状态同步检查

## 8. 部署说明

### 8.1 环境要求
- .NET 8.0+
- Redis 6.0+
- PostgreSQL 14+
- SMTP邮件服务
- Pokemon Showdown 服务器
- SSL证书（推荐）

### 8.2 部署步骤
1. 配置环境变量
2. 初始化数据库
3. 设置邮件服务
4. 配置Redis连接
5. 生成SSL证书
6. 启动服务器

## 9. 注意事项
1. 定期清理过期会话
2. 监控邮件发送限制
3. 备份数据库数据
4. 定期更新JWT密钥
5. 处理并发连接
6. 确保SMTP配置正确
7. 监控验证码发送频率
8. 处理认证失败重试
9. 维护令牌黑名单
10. 注意数据安全存储
11. 定期数据库维护
12. 监控连接池状态
13. 监控对战服务器状态
14. 处理对战超时情况
15. 维护对战房间状态