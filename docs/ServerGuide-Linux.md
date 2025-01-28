# Pokemon 服务端启动指南 (CentOS)

## 1. 环境准备

### 1.1 安装 .NET SDK
1. 添加 Microsoft 包仓库：
```bash
sudo rpm -Uvh https://packages.microsoft.com/config/centos/7/packages-microsoft-prod.rpm
```

2. 安装 .NET SDK：
```bash
sudo yum install -y dotnet-sdk-8.0
```

3. 验证安装：
```bash
dotnet --version
```

### 1.2 安装 Redis
1. 启用 EPEL 仓库：
```bash
sudo yum install -y epel-release
```

2. 安装 Redis：
```bash
sudo yum install -y redis
```

3. 启动 Redis 服务：
```bash
sudo systemctl start redis
```

4. 设置开机自启：
```bash
sudo systemctl enable redis
```

5. 验证 Redis：
```bash
redis-cli ping
# 应返回 PONG
```

### 1.3 安装 Protocol Buffers
1. 安装依赖：
```bash
sudo yum install -y gcc gcc-c++ make
```

2. 下载并安装 protobuf：
```bash
# 下载最新版本
curl -LO https://github.com/protocolbuffers/protobuf/releases/download/v3.15.8/protoc-3.15.8-linux-x86_64.zip

# 安装 unzip（如果未安装）
sudo yum install -y unzip

# 解压到 /usr/local
sudo unzip protoc-3.15.8-linux-x86_64.zip -d /usr/local

# 设置权限
sudo chmod 755 /usr/local/bin/protoc
```

3. 验证安装：
```bash
protoc --version

### 1.4 安装 VS Code (可选)
1. 下载 VS Code：
```bash
sudo snap install code --classic
```

2. 安装扩展：
```bash
code --install-extension ms-dotnettools.csharp
code --install-extension zxh404.vscode-proto3
```

## 2. 创建项目

### 2.1 创建项目结构
```bash
# 创建项目目录
mkdir my-pokemon
cd my-pokemon

# 创建解决方案
dotnet new sln

# 创建项目目录结构
mkdir -p src/{Network,Services,Protocol/Generated,Models,Protos}
cd src
dotnet new console
```

### 2.2 创建配置文件
```bash
# 创建并编辑 appsettings.json
cat > appsettings.json << EOF
{
  "Server": {
    "Port": 5000
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  }
}
EOF
```

### 2.3 安装项目依赖
```bash
# 在 src 目录下执行
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
```bash
# 假设源代码在当前目录
# Program.cs
cp Program.cs src/

# Network
cp NettyTcpServer.cs src/Network/

# Services
cp SessionManager.cs src/Services/
cp MapService.cs src/Services/

# Protocol
cp MessageRouter.cs src/Protocol/

# Models
cp PlayerPosition.cs src/Models/

# Protos
cp Map.proto src/Protos/
```

### 3.2 生成 Protobuf 代码
```bash
cd src
protoc --csharp_out=./Protocol/Generated --proto_path=./Protos ./Protos/Map.proto
```

## 4. 构建和运行

### 4.1 添加项目到解决方案
```bash
cd ..
dotnet sln add src/MyPokemon.csproj
```

### 4.2 构建项目
```bash
dotnet build
```

### 4.3 运行项目
1. 确保 Redis 正在运行：
```bash
sudo systemctl status redis-server
```

2. 运行项目：
```bash
cd src
dotnet run
```

## 5. 验证运行状态

### 5.1 检查 Redis 连接
```bash
redis-cli
> ping
PONG
> keys *
```

### 5.2 检查服务器端口
```bash
netstat -tulpn | grep 5000
```

### 5.3 检查服务日志
1. 应用日志：
```bash
tail -f /var/log/syslog | grep dotnet
```

2. Redis 日志：
```bash
tail -f /var/log/redis/redis-server.log
```

## 6. 常见问题解决

### 6.1 Redis 问题
1. 检查服务状态：
```bash
sudo systemctl status redis-server
```

2. 重启服务：
```bash
sudo systemctl restart redis-server
```

3. 查看日志：
```bash
sudo journalctl -u redis-server
```

### 6.2 权限问题
1. 设置目录权限：
```bash
sudo chown -R $USER:$USER my-pokemon/
chmod -R 755 my-pokemon/
```

2. 设置端口访问权限：
```bash
sudo ufw allow 5000/tcp
```

### 6.3 依赖问题
1. 清理项目：
```bash
dotnet clean
```

2. 删除生成目录：
```bash
rm -rf src/obj src/bin
```

3. 重新还原依赖：
```bash
dotnet restore
```

## 7. 开发工具使用

### 7.1 使用 VS Code
1. 打开项目：
```bash
code .
```

2. 常用终端命令：
```bash
# 构建
dotnet build

# 运行
dotnet run

# 清理
dotnet clean

# 还原包
dotnet restore
```

### 7.2 使用 Redis CLI
1. 监控命令：
```bash
# 监控所有命令
redis-cli monitor

# 查看内存使用
redis-cli info memory

# 查看客户端连接
redis-cli client list
```

### 7.3 系统监控
1. 查看进程：
```bash
ps aux | grep dotnet
ps aux | grep redis
```

2. 查看端口：
```bash
sudo lsof -i :5000
sudo lsof -i :6379
```

## 8. 部署建议

### 8.1 使用 systemd 服务
1. 创建服务文件：
```bash
sudo nano /etc/systemd/system/pokemon-server.service
```

2. 添加以下内容：
```ini
[Unit]
Description=Pokemon Game Server
After=network.target redis-server.service

[Service]
WorkingDirectory=/path/to/my-pokemon/src
ExecStart=/usr/bin/dotnet run
Restart=always
RestartSec=10
User=your_username

[Install]
WantedBy=multi-user.target
```

3. 启用服务：
```bash
sudo systemctl enable pokemon-server
sudo systemctl start pokemon-server
```

### 8.2 日志管理
1. 查看服务日志：
```bash
sudo journalctl -u pokemon-server -f
```

2. 配置日志轮转：
```bash
sudo nano /etc/logrotate.d/pokemon-server
``` 


使用pm2管理项目 一直启动
# 安装 Node.js（如果没有安装）
curl -fsSL https://rpm.nodesource.com/setup_18.x | sudo bash -
sudo yum install -y nodejs




1. **首先安装 Node.js 和 pm2**
````bash
# 安装 Node.js（如果没有安装）
curl -fsSL https://rpm.nodesource.com/setup_18.x | sudo bash -
sudo yum install -y nodejs

# 安装 pm2
sudo npm install pm2 -g

# 验证安装
pm2 --version
````


2. **创建启动脚本**
````bash
# 创建启动脚本
cat > start-pokemon.sh << 'EOF'
#!/bin/bash
cd /www/wwwroot/my-pokemon/src
dotnet run
EOF

# 添加执行权限
chmod +x start-pokemon.sh
````



3. **使用 pm2 启动应用**
````bash
# 启动应用
pm2 start start-pokemon.sh --name "pokemon-server"

# 其他常用 pm2 命令
pm2 list                    # 查看所有应用
pm2 stop pokemon-server     # 停止应用
pm2 restart pokemon-server  # 重启应用
pm2 logs pokemon-server     # 查看日志
pm2 monit                   # 监控应用
````


# 添加执行权限
chmod +x start-pokemon.sh

#设置pm2开机自启
# 生成开机自启脚本
pm2 startup

# 保存当前运行的应用列表，以便开机自启
pm2 save

#pm2常用管理命令
# 查看应用状态
pm2 status

# 查看详细信息
pm2 show pokemon-server

# 重载应用
pm2 reload pokemon-server

# 删除应用
pm2 delete pokemon-server

# 清除日志
pm2 flush

# 查看资源使用
pm2 monit