# Pokemon 多人在线项目说明文档

## 1. 项目概述
这是一个基于.NET和Unity的多人在线Pokemon游戏项目。服务端使用DotNetty处理网络通信,Redis缓存数据,Protobuf进行消息序列化。

## 2. 项目结构

### 2.1 服务端 (my-pokemon/src/)
```
src/
├── Program.cs                # 程序入口,配置依赖注入和服务启动
├── MyPokemon.csproj         # 项目文件,管理NuGet包引用
├── appsettings.json         # 配置文件,包含服务器端口和Redis连接
│
├── Network/                 # 网络层
│   └── NettyTcpServer.cs    # TCP服务器,处理客户端连接和消息
│
├── Services/                # 业务服务层
│   ├── MapService.cs        # 位置同步服务,处理玩家位置更新和广播
│   └── SessionManager.cs    # 会话管理,处理玩家连接状态
│
├── Protocol/               # 协议层
│   ├── MessageRouter.cs     # 消息路由,分发消息到对应处理器
│   └── Generated/          # Protobuf生成的C#代码
│
├── Models/                 # 数据模型
│   └── PlayerPosition.cs    # 玩家位置模型
│
└── Protos/                # 协议定义
    └── Map.proto           # 位置同步相关消息定义
```

### 2.2 客户端 (my-pokemon/Client/)
```
Client/
├── PokemonClient.cs        # Unity客户端主要逻辑
└── NettyClient.cs          # 网络客户端,处理与服务器通信
```

## 3. 核心文件说明

### 3.1 服务端文件

#### Program.cs
- 程序入口点
- 配置依赖注入
- 启动TCP服务器
- 连接Redis缓存

#### NettyTcpServer.cs
- 基于DotNetty的TCP服务器
- 处理客户端连接/断开
- 配置Protobuf编解码器
- 转发消息到MessageRouter

#### SessionManager.cs
- 管理所有客户端连接
- 维护玩家ID和连接的映射
- 处理玩家上线/下线
- 提供广播功能

#### MapService.cs
- 处理位置同步逻辑
- 使用Redis缓存位置数据
- 向相关玩家广播位置更新

#### MessageRouter.cs
- 解析和分发消息到对应处理器
- 维护消息类型和处理器的映射

#### Map.proto
- 定义位置同步相关的消息格式
- 包含PositionRequest和PositionBroadcast

### 3.2 客户端文件

#### PokemonClient.cs
- Unity客户端主要逻辑
- 处理玩家输入
- 更新其他玩家位置
- 管理游戏对象

#### NettyClient.cs
- 处理与服务器的网络通信
- 发送位置更新
- 处理位置广播消息
- 管理连接状态

## 4. 关键配置文件

### appsettings.json
```json
{
  "Server": {
    "Port": 5000          // 服务器监听端口
  },
  "Redis": {
    "ConnectionString": "localhost:6379"  // Redis连接配置
  }
}
```

### MyPokemon.csproj
- 定义项目依赖
- 配置Protobuf生成
- 管理项目设置

## 5. 主要功能流程

### 5.1 位置同步
1. 客户端发送位置更新(PositionRequest)
2. 服务器接收并更新Redis缓存
3. MapService广播位置给其他玩家(PositionBroadcast)
4. 其他客户端更新对应玩家位置

### 5.2 会话管理
1. 客户端连接服务器
2. SessionManager创建新会话
3. 分配玩家ID
4. 断开时清理会话数据

## 6. 扩展建议

### 6.1 功能扩展
- 添加玩家认证系统
- 实现对战系统
- 添加NPC和野生精灵
- 实现物品系统
- 添加聊天功能

### 6.2 技术改进
- 添加日志系统
- 实现负载均衡
- 优化网络同步
- 添加数据持久化
- 实现热更新

## 7. 调试信息

### 7.1 服务端调试
- 使用Visual Studio调试器
- 查看Redis数据
- 检查网络连接
- 监控内存使用

### 7.2 客户端调试
- Unity Profiler分析
- 网络延迟监控
- 内存泄漏检测
- 帧率优化

## 8. 注意事项
1. 确保Redis服务运行
2. 生成Protobuf代码
3. 正确配置端口映射
4. 定期清理Redis缓存
5. 处理网络异常情况 