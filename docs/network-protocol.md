# 宝可梦多人游戏网络协议文档

## 1. 概述

本文档描述了宝可梦多人游戏的网络通信协议和实现细节。系统采用 TCP 长连接，使用 Protobuf 作为序列化方案。

## 2. 通信流程

### 2.1 服务器启动流程

1. **Program.cs**
   - `Main()`: 程序入口点
   - `CreateHostBuilder()`: 配置依赖注入
   - `ConfigureServices()`: 注册服务
     - 注册 `SessionManager`
     - 注册 `MapService`
     - 注册 `NettyTcpServer`

2. **NettyTcpServer.cs**
   - `StartAsync(int port)`: 启动服务器
   - 配置 DotNetty 管道：
     - `ProtobufVarint32FrameDecoder`: 解码消息长度
     - `ProtobufDecoder`: 解析 protobuf 消息
     - `ProtobufVarint32LengthFieldPrepender`: 编码消息长度
     - `ProtobufEncoder`: 序列化 protobuf 消息

### 2.2 客户端连接流程

1. **客户端初始化 (NetworkManager.cs)**
   - `Awake()`: 初始化单例
   - `Connect(string host, int port)`: 连接服务器
   - `BeginReadVarint()`: 开始接收消息

2. **服务器处理连接 (NettyTcpServer.cs -> ServerHandler)**
   - `ChannelActive()`: 处理新连接
   - 调用 `SessionManager.CreateSession()`

3. **会话创建 (SessionManager.cs)**
   - `CreateSession()`: 创建新会话
   - 生成唯一 PlayerId
   - 保存会话信息到 `_sessions` 字典

### 2.3 位置同步流程

#### 客户端发送位置：

1. **PlayerController.cs**
   - `Update()`: 检测玩家输入
   - `UpdateServerPosition()`: 定时发送位置
   - 调用 `NetworkManager.SendPosition()`

2. **NetworkManager.cs**
   - `SendPosition(float x, float y, int direction)`
     - 创建 `PositionRequest` 消息
     - `EncodeVarint32()`: 编码消息长度
     - 发送数据到服务器

#### 服务器处理位置：

1. **NettyTcpServer.cs**
   - 接收数据并通过编解码器处理
   - 传递给 `MessageRouter`

2. **MessageRouter.cs**
   - `ChannelRead0()`: 接收消息
   - `HandleMessageAsync()`: 处理 `PositionRequest`
     - 获取会话信息
     - 调用 `MapService.UpdatePosition()`

3. **MapService.cs**
   - `UpdatePosition()`: 更新玩家位置
     - 保存位置到 Redis
     - 调用 `BroadcastPosition()`
   - `GetAllPositions()`: 获取所有玩家位置
   - `BroadcastPosition()`: 广播位置给其他玩家
     - 创建 `PositionBroadcast` 消息
     - 通过 `SessionManager.BroadcastAsync()` 发送

#### 客户端接收广播：

1. **NetworkManager.cs**
   - `OnVarintByte()`: 读取消息长度
   - `OnMessageReceived()`: 接收消息内容
   - 解析 `PositionBroadcast` 消息
   - 触发 `OnPositionBroadcast` 事件

2. **PlayerManager.cs**
   - `UpdatePlayerPositions()`: 更新玩家位置
     - 创建/更新其他玩家的游戏对象
     - 更新位置和朝向

### 2.4 断开连接流程

1. **客户端断开 (NetworkManager.cs)**
   - `OnDestroy()`: 清理资源
     - 关闭 TCP 连接
     - 释放网络流

2. **服务器处理断开 (NettyTcpServer.cs -> ServerHandler)**
   - `ChannelInactive()`: 处理连接断开
   - 调用 `SessionManager.RemoveSession()`

3. **会话清理 (SessionManager.cs)**
   - `RemoveSession()`: 移除会话
   - `CleanupInactiveSessions()`: 清理超时会话

## 3. 消息定义

### 3.1 位置请求 (PositionRequest)


当有新的客户端连接时，DotNetty 的事件循环会：
接受 TCP 连接
创建新的 Channel
初始化 Channel 的管道
自动调用管道中所有处理器的 ChannelActive 方法
调用顺序是这样的：
客户端连接 -> DotNetty EventLoop -> Channel -> Pipeline -> ServerHandler.ChannelActive