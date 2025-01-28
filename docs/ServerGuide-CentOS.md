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
```

### 1.4 安装 VS Code (可选)
1. 添加 VS Code 仓库：
```bash
# 导入微软的 GPG key
sudo rpm --import https://packages.microsoft.com/keys/microsoft.asc

# 添加 VS Code 仓库
sudo sh -c 'echo -e "[code]\nname=Visual Studio Code\nbaseurl=https://packages.microsoft.com/yumrepos/vscode\nenabled=1\ngpgcheck=1\ngpgkey=https://packages.microsoft.com/keys/microsoft.asc" > /etc/yum.repos.d/vscode.repo'
```

2. 安装 VS Code：
```bash
# 更新包缓存
sudo yum update -y

# 安装 VS Code
sudo yum install -y code
```

3. 安装必要扩展：
```bash
# C# 开发工具包
code --install-extension ms-dotnettools.csdevkit

# .NET 扩展包
code --install-extension ms-dotnettools.vscode-dotnet-runtime

# Protobuf 支持
code --install-extension zxh404.vscode-proto3

# C# 语言支持
code --install-extension ms-dotnettools.csharp
```

4. 配置 VS Code：
```bash
# 创建用户配置目录
mkdir -p ~/.config/Code/User/

# 创建设置文件
cat > ~/.config/Code/User/settings.json << EOF
{
    "files.autoSave": "afterDelay",
    "editor.formatOnSave": true,
    "omnisharp.enableRoslynAnalyzers": true,
    "omnisharp.enableEditorConfigSupport": true
}
EOF
```
### 1.4 安装 VS Code (仅开发环境，服务器无需安装)
> 注意：VS Code 和扩展只需要在开发机器上安装，生产服务器不需要安装这部分内容。

5. 验证安装：
```bash
# 检查 VS Code 版本
code --version

# 列出已安装的扩展
code --list-extensions
```

## 2. 系统配置

### 2.1 防火墙配置
1. 开放必要端口：
```bash
# 开放游戏服务器端口
sudo firewall-cmd --permanent --add-port=5000/tcp

# 重新加载防火墙配置
sudo firewall-cmd --reload
```

### 2.2 SELinux 配置
1. 允许网络访问：
```bash
# 允许程序监听端口
sudo setsebool -P httpd_can_network_connect 1
```

### 2.3 系统优化
1. 调整系统限制：
```bash
sudo vi /etc/security/limits.conf

# 添加以下内容
* soft nofile 65535
* hard nofile 65535
```

2. 调整内核参数：
```bash
sudo vi /etc/sysctl.conf

# 添加以下内容
net.core.somaxconn = 1024
net.ipv4.tcp_max_syn_backlog = 1024
net.ipv4.tcp_fin_timeout = 30
net.ipv4.tcp_keepalive_time = 300
```

## 3. 服务配置

### 3.1 创建服务用户
```bash
sudo useradd -r -s /sbin/nologin pokemon
```

### 3.2 创建应用目录
```bash
sudo mkdir -p /opt/pokemon
sudo chown -R pokemon:pokemon /opt/pokemon
```

### 3.3 配置服务
1. 创建服务文件：
```bash
sudo vi /etc/systemd/system/pokemon-server.service
```

2. 添加服务配置：
```ini
[Unit]
Description=Pokemon Game Server
After=network.target redis.service

[Service]
User=pokemon
WorkingDirectory=/opt/pokemon/src
ExecStart=/usr/bin/dotnet run
Restart=always
RestartSec=10
LimitNOFILE=65535

[Install]
WantedBy=multi-user.target
```

## 4. 部署应用

### 4.1 复制应用文件
```bash
# 假设源代码在当前目录
sudo cp -r * /opt/pokemon/
sudo chown -R pokemon:pokemon /opt/pokemon
```

### 4.2 配置日志
1. 创建日志目录：
```bash
sudo mkdir -p /var/log/pokemon
sudo chown pokemon:pokemon /var/log/pokemon
```

2. 配置日志轮转：
```bash
sudo vi /etc/logrotate.d/pokemon

# 添加以下内容
/var/log/pokemon/*.log {
    daily
    rotate 7
    compress
    delaycompress
    missingok
    notifempty
    create 644 pokemon pokemon
}
```

## 5. 启动服务

### 5.1 启动服务
```bash
# 重新加载 systemd
sudo systemctl daemon-reload

# 启动服务
sudo systemctl start pokemon-server

# 设置开机自启
sudo systemctl enable pokemon-server
```

### 5.2 验证服务状态
```bash
# 检查服务状态
sudo systemctl status pokemon-server

# 查看日志
sudo journalctl -u pokemon-server -f

# 检查端口
sudo netstat -tulpn | grep 5000
```

## 6. 监控和维护

### 6.1 服务监控
```bash
# 查看服务状态
sudo systemctl status pokemon-server

# 查看内存使用
ps -o pid,ppid,%mem,rss,cmd -p $(pgrep -f pokemon-server)

# 查看CPU使用
top -p $(pgrep -f pokemon-server)
```

### 6.2 Redis 监控
```bash
# 查看Redis状态
redis-cli info

# 监控Redis命令
redis-cli monitor
```

## 7. 常见问题解决

### 7.1 权限问题
```bash
# 检查SELinux状态
sestatus

# 如果遇到权限问题，可以临时关闭SELinux
sudo setenforce 0
```

### 7.2 网络问题
```bash
# 检查防火墙状态
sudo systemctl status firewalld

# 检查端口是否开放
sudo firewall-cmd --list-ports

# 测试端口连接
telnet localhost 5000
```

### 7.3 服务问题
```bash
# 重启服务
sudo systemctl restart pokemon-server

# 查看详细日志
sudo journalctl -u pokemon-server -f --since "1 hour ago"
```

### 7.4 性能问题
```bash
# 查看系统负载
uptime

# 查看内存使用
free -m

# 查看磁盘使用
df -h
``` 