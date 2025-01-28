# Pokemon 项目运行指南

## 1. 环境准备

### 1.1 服务端环境
- .NET 6.0 SDK
- Redis 服务器
- Visual Studio 2022 或 VS Code
- protoc (Protocol Buffers编译器)

### 1.2 客户端环境
- Unity 2021.3 或更高版本
- Visual Studio 2022 或 VS Code

## 2. 服务端配置

### 2.1 安装NuGet包
```bash
dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis
dotnet add package DotNetty.Transport
dotnet add package Google.Protobuf
dotnet add package Grpc.Tools
```

### 2.2 配置文件
在 `my-pokemon/src/appsettings.json` 中添加以下配置：

```json
{
  "Server": {
    "Port": 5000
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  }
}
```

### 2.3 生成Protobuf代码
```bash
# Windows
protoc --csharp_out=./src/Protocol/Generated --proto_path=./src/Protos Map.proto

# Linux/Mac
protoc --csharp_out=./src/Protocol/Generated --proto_path=./src/Protos ./src/Protos/Map.proto
```

## 3. 客户端配置

### 3.1 创建Unity项目
1. 创建新的2D项目
2. 导入必要的资源(精灵图等)

### 3.2 配置网络
1. 创建 `Plugins` 文件夹
2. 复制服务端生成的Protobuf文件到 `Plugins` 目录
3. 添加以下DLL引用到 `Plugins`:
   - Google.Protobuf.dll
   - DotNetty.Transport.dll

### 3.3 创建预制体
1. 创建玩家预制体(Player Prefab)
   - 添加精灵渲染器
   - 添加必要的组件(如Rigidbody2D等)
2. 创建其他玩家预制体(Other Player Prefab)
   - 类似玩家预制体，但不需要控制脚本

## 4. 启动顺序

1. 启动Redis服务器
```bash
# Windows
redis-server

# Linux
sudo service redis start

# Mac
brew services start redis
```

2. 启动服务端
```bash
cd my-pokemon/src
dotnet run
```

3. 启动Unity客户端
- 在Unity编辑器中运行
- 或构建并运行可执行文件

## 5. 测试位置同步

1. 运行两个客户端实例
2. 在第一个客户端中移动角色
3. 观察第二个客户端中的角色是否同步移动
4. 检查服务端日志确认位置更新请求

## 6. 常见问题

### 6.1 连接问题
- 确认Redis服务器正在运行
- 检查防火墙设置
- 验证服务器端口(5000)是否被占用

### 6.2 同步问题
- 检查网络延迟
- 确认Protobuf消息格式正确
- 验证客户端PlayerId是否正确设置

### 6.3 Unity相关
- 确保预制体正确配置
- 检查组件引用
- 验证场景设置

## 7. 调试工具

### 7.1 服务端
- Visual Studio调试器
- Redis命令行工具
```bash
redis-cli
# 查看所有位置键
keys position:*
# 获取特定玩家位置
get position:player1
```

### 7.2 客户端
- Unity Profiler
- Unity Debug.Log
- Unity Scene视图

## 8. 下一步开发建议

1. 添加玩家认证系统
2. 实现地图分区
3. 添加碰撞检测
4. 优化同步频率
5. 添加动画系统
6. 实现聊天功能

## 9. 项目结构
```
my-pokemon/
├── src/
│   ├── Program.cs                # 程序入口
│   ├── appsettings.json         # 配置文件
│   ├── Services/
│   │   └── MapService.cs        # 位置服务
│   ├── Protocol/
│   │   ├── MessageRouter.cs     # 消息路由
│   │   └── Generated/           # 生成的Protobuf代码
│   ├── Models/
│   │   └── PlayerPosition.cs    # 位置模型
│   └── Protos/
│       └── Map.proto            # 协议定义
└── Client/
    └── PokemonClient.cs         # Unity客户端代码
``` 