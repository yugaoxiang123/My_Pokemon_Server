# Pokemon 服务端启动指南 (Windows)

## 1. 环境准备

### 1.1 安装 .NET SDK
1. 检查是否已安装：
```powershell
dotnet --version
```

2. 如果未安装：
   - 访问 https://dotnet.microsoft.com/download/dotnet/8.0
   - 下载 ".NET SDK 8.0" Windows 安装程序
   - 双击运行安装程序
   - 安装完成后重启命令提示符

### 1.2 安装 Scoop (包管理器)
1. 打开 PowerShell (管理员)
2. 允许执行脚本：
```powershell
Set-ExecutionPolicy RemoteSigned -Scope CurrentUser
```

3. 安装 Scoop：
```powershell
irm get.scoop.sh | iex
```

### 1.3 安装 Redis
1. 使用 Scoop 安装：
```powershell
scoop install redis
```

2. 启动 Redis 服务器：
```powershell
redis-server
```

3. 验证 Redis：
   - 打开新的命令提示符
```powershell
redis-cli ping
# 应返回 PONG
```

### 1.4 安装 Protocol Buffers
1. 使用 Scoop 安装：
```powershell
# 安装 git (如果未安装)
scoop install git

   # 验证git安装
   git --version

# 添加 extras bucket
scoop bucket add extras

# 安装 protobuf
scoop install protobuf

# 验证安装
protoc --version
```

### 1.5 安装 Visual Studio Code
1. 下载并安装 VS Code：https://code.visualstudio.com/
2. 安装扩展：
   - C# Dev Kit
   - .NET Extension Pack
   - Proto Language Support

## 2. 创建项目

### 2.1 创建项目结构
```powershell
# 创建项目目录
mkdir my-pokemon
cd my-pokemon

# 创建解决方案
dotnet new sln

# 创建项目目录结构
mkdir src
cd src
dotnet new console
mkdir Network Services Protocol Models Protos
cd Protocol
mkdir Generated
cd ..
```

### 2.2 创建配置文件
在 src 目录下创建 appsettings.json：
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

### 2.3 安装项目依赖
在 src 目录下执行：
```powershellcd ..
dotnet add package DotNetty.Transport --version 0.7.5
dotnet add package DotNetty.Codecs --version 0.7.5
dotnet add package DotNetty.Codecs.Protobuf --version 0.7.5
dotnet add package Google.Protobuf --version 3.21.0
dotnet add package Grpc.Tools --version 2.47.0
dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis
dotnet add package Microsoft.Extensions.Hosting
dotnet add package DotNetty.Common
```

## 3. 复制源代码

### 3.1 复制各个文件
1. Program.cs → src/
2. NettyTcpServer.cs → src/Network/
3. SessionManager.cs → src/Services/
4. MapService.cs → src/Services/
5. MessageRouter.cs → src/Protocol/
6. PlayerPosition.cs → src/Models/
7. Map.proto → src/Protos/

### 3.2 生成 Protobuf 代码
```powershell
cd src
protoc --csharp_out=./Protocol/Generated --proto_path=./Protos Map.proto
```

## 4. 构建和运行

### 4.1 添加项目到解决方案
```powershell
cd ..
dotnet sln add src/MyPokemon.csproj
```

### 4.2 构建项目
```powershell
dotnet build
```

### 4.3 运行项目
1. 确保 Redis 正在运行：
```powershell
# 新开一个命令提示符
redis-server
```

2. 运行项目：
```powershell
cd src
dotnet run
```

## 5. 验证运行状态

### 5.1 检查 Redis 连接
```powershell
redis-cli
> ping
PONG
> keys *
```

### 5.2 检查服务器端口
```powershell
netstat -an | findstr 5000
```

### 5.3 查看日志输出
应该看到：
```
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
Server started on port 5000
```

## 6. 常见问题解决

### 6.1 Redis 启动失败
1. 检查是否已安装：
```powershell
redis-cli --version
```

2. 检查端口占用：
```powershell
netstat -ano | findstr 6379
```

3. 重新安装：
```powershell
scoop uninstall redis
scoop install redis
```

### 6.2 Protobuf 生成失败
1. 检查 protoc 是否正确安装：
```powershell
protoc --version
```

2. 检查目录结构：
```powershell
dir src\Protocol\Generated
dir src\Protos
```

3. 重新生成：
```powershell
cd src
protoc --csharp_out=./Protocol/Generated --proto_path=./Protos Map.proto
```

### 6.3 依赖包还原失败
1. 清理解决方案：
```powershell
dotnet clean
```

2. 删除 obj 和 bin 目录：
```powershell
rd /s /q src\obj src\bin
```

3. 重新还原和构建：
```powershell
dotnet restore
dotnet build
```

## 7. 开发工具使用

### 7.1 VS Code
1. 打开项目：
```powershell
code .
```

2. 常用快捷键：
   - F5: 启动调试
   - Ctrl+Shift+B: 构建
   - Ctrl+`: 打开终端

### 7.2 Redis Desktop Manager
1. 下载安装 Another Redis Desktop Manager
2. 连接配置：
   - Host: localhost
   - Port: 6379
   - Name: Pokemon Server 