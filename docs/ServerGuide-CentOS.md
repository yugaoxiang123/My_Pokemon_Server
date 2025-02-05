# Pokemon 服务器 Linux 部署指南

## 1. 环境准备

### 1.1 安装 .NET SDK
```bash
# 添加 Microsoft 包仓库
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# 安装 SDK
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
```

### 1.2 安装 Redis
```bash
# 安装 Redis
sudo apt-get install redis-server

# 启动 Redis 服务
sudo systemctl start redis-server
sudo systemctl enable redis-server

# 验证 Redis 运行状态
redis-cli ping
```

### 1.3 安装 PostgreSQL
```bash
# 安装 PostgreSQL
sudo apt-get install postgresql postgresql-contrib

# 启动服务
sudo systemctl start postgresql
sudo systemctl enable postgresql

# 切换到 postgres 用户
sudo -i -u postgres

# 创建数据库和用户
psql
CREATE DATABASE pokemon;
CREATE USER pokemon_user WITH PASSWORD 'your_password';
GRANT ALL PRIVILEGES ON DATABASE pokemon TO pokemon_user;
\q

# 配置远程访问（如需要）
sudo nano /etc/postgresql/14/main/postgresql.conf
# 修改 listen_addresses = '*'

sudo nano /etc/postgresql/14/main/pg_hba.conf
# 添加 host all all 0.0.0.0/0 md5
```

## 2. 项目部署

### 2.1 获取代码
```bash
# 克隆项目
git clone https://github.com/your-repo/pokemon-server.git
cd pokemon-server

# 安装依赖
dotnet restore
```

### 2.2 配置文件
```bash
# 创建并编辑配置文件
cp appsettings.Example.json appsettings.json
nano appsettings.json

# 配置示例
{
  "Server": {
    "Port": 5000,
    "ViewDistance": 15
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "Database": {
    "ConnectionString": "Host=localhost;Database=pokemon;Username=pokemon_user;Password=your_password"
  }
}
```

### 2.3 编译运行
```bash
# 发布项目
dotnet publish -c Release

# 运行服务器
cd bin/Release/net8.0/publish
dotnet MyPokemon.dll
```

### 2.4 使用 systemd 管理服务
```bash
# 创建服务文件
sudo nano /etc/systemd/system/pokemon-server.service

[Unit]
Description=Pokemon Game Server
After=network.target postgresql.service redis-server.service

[Service]
WorkingDirectory=/opt/pokemon-server
ExecStart=/usr/bin/dotnet MyPokemon.dll
Restart=always
RestartSec=10
User=pokemon
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target

# 启动服务
sudo systemctl enable pokemon-server
sudo systemctl start pokemon-server

# 查看日志
sudo journalctl -u pokemon-server -f
```

## 3. 防火墙配置

```bash
# 开放游戏服务器端口
sudo ufw allow 5000/tcp

# 如果需要开放 PostgreSQL 端口
sudo ufw allow 5432/tcp

# 启用防火墙
sudo ufw enable
```

## 4. 监控和维护

### 4.1 日志查看
```bash
# 查看系统日志
tail -f /var/log/syslog

# 查看服务日志
sudo journalctl -u pokemon-server -f
```

### 4.2 数据库备份
```bash
# 备份数据库
pg_dump -U pokemon_user pokemon > backup.sql

# 还原数据库
psql -U pokemon_user pokemon < backup.sql
```

### 4.3 Redis 监控
```bash
# 连接到 Redis
redis-cli

# 监控命令
INFO
MONITOR
```

## 5. 常见问题处理

### 5.1 服务无法启动
- 检查日志: `sudo journalctl -u pokemon-server -f`
- 检查端口占用: `sudo lsof -i :5000`
- 检查权限: `ls -l /opt/pokemon-server`

### 5.2 数据库连接问题
- 检查 PostgreSQL 状态: `sudo systemctl status postgresql`
- 检查连接字符串
- 验证用户权限

### 5.3 Redis 连接问题
- 检查 Redis 状态: `sudo systemctl status redis-server`
- 检查连接配置
- 验证内存使用情况

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