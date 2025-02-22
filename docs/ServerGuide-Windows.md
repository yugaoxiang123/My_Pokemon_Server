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

### 1.6 安装邮件服务（可选）
1. 准备QQ邮箱
   - 登录QQ邮箱
   - 开启SMTP服务
         让我为您介绍QQ邮箱SMTP的配置步骤：
         首先开启QQ邮箱的SMTP服务：
         登录您的QQ邮箱 (https://mail.qq.com)
         点击"设置" -> "账户"
         找到"POP3/SMTP服务"
         点击"开启"
         按照提示用手机验证
         系统会生成一个"授权码"，请保存好这个授权码
         修改服务器的 appsettings.json 配置：
                           {
               "Email": {
                  "SmtpHost": "smtp.qq.com",
                  "SmtpPort": 587,
                  "Username": "您的QQ邮箱@qq.com",
                  "Password": "您获取的授权码",  // 注意：这里填写授权码，不是QQ密码
                  "FromAddress": "您的QQ邮箱@qq.com"
               }
               }

2. 或使用其他SMTP服务
   - Gmail
   - Outlook
   - 自建邮件服务器

### 1.7 安装 PostgreSQL
1. 使用 Scoop 安装：
```powershell
scoop install postgresql
```

2. 初始化数据库：
```powershell
# 初始化数据库目录
initdb -D C:\PostgreSQL\data -U postgres -W -E UTF8 -A scram-sha-256

# 启动数据库服务
pg_ctl -D C:\PostgreSQL\data start
```

3. 创建数据库和用户：
```powershell
# 连接到默认数据库
psql -U postgres

# 创建数据库
CREATE DATABASE pokemon;

# 创建用户并设置密码
CREATE USER pokemon_user WITH PASSWORD 'ygx131953';

# 授予权限
GRANT ALL PRIVILEGES ON DATABASE pokemon TO pokemon_user;
```

4. 创建表结构：
```sql
\c pokemon

-- 创建用户表
CREATE TABLE users (
    -- 基本信息
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_name VARCHAR(50) UNIQUE NOT NULL,
    email VARCHAR(255) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    
    -- 认证相关
    is_email_verified BOOLEAN DEFAULT FALSE,
    verification_code VARCHAR(6),
    verification_code_expires_at TIMESTAMP WITH TIME ZONE,
    
    -- 时间戳
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    last_login_at TIMESTAMP WITH TIME ZONE,
    
    -- 位置信息
    last_position_x FLOAT DEFAULT 0,
    last_position_y FLOAT DEFAULT 0,
    last_direction INT DEFAULT 0
);

-- 创建索引
CREATE INDEX idx_users_player_name ON users(player_name);
CREATE INDEX idx_users_email ON users(email);

-- 添加注释
COMMENT ON TABLE users IS '用户信息表';
COMMENT ON COLUMN users.id IS '用户唯一标识符';
COMMENT ON COLUMN users.player_name IS '玩家名称（唯一）';
COMMENT ON COLUMN users.email IS '邮箱地址（唯一）';
COMMENT ON COLUMN users.password_hash IS '密码哈希值';
COMMENT ON COLUMN users.is_email_verified IS '邮箱是否已验证';
COMMENT ON COLUMN users.verification_code IS '邮箱验证码';
COMMENT ON COLUMN users.verification_code_expires_at IS '验证码过期时间';
COMMENT ON COLUMN users.created_at IS '账号创建时间';
COMMENT ON COLUMN users.last_login_at IS '最后登录时间';
COMMENT ON COLUMN users.last_position_x IS '最后位置X坐标';
COMMENT ON COLUMN users.last_position_y IS '最后位置Y坐标';
COMMENT ON COLUMN users.last_direction IS '最后朝向';
```

//# 连接数据库
psql -U pokemon_user -d pokemon

# 查看所有表
\dt

# 查看指定表结构
\d users

# 查看表的详细信息（包含注释）
\d+ users

如果user权限不够
-- 切换到 postgres 用户执行
\c pokemon postgres

-- 授予 pokemon_user 对 users 表的所有权限
GRANT ALL PRIVILEGES ON TABLE users TO pokemon_user;

-- 如果有序列，也需要授权
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO pokemon_user;

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
    "Port": 5000,
    "ViewDistance": 15
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
  }
}
```

### 2.3 安装项目依赖
在 src 目录下执行：
```powershell
# 添加其他必要的包
dotnet add package DotNetty.Transport --version 0.7.5
dotnet add package DotNetty.Codecs --version 0.7.5
dotnet add package DotNetty.Codecs.Protobuf --version 0.7.5
dotnet add package Google.Protobuf --version 3.25.2
dotnet add package Grpc.Tools --version 2.60.0
dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis --version 8.0.2
dotnet add package Microsoft.Extensions.Hosting --version 8.0.0
dotnet add package DotNetty.Common --version 0.7.5

# 添加认证相关包
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package System.IdentityModel.Tokens.Jwt --version 7.3.1
```
# 添加数据库相关包
dotnet add package Npgsql --version 8.0.1
dotnet add package Dapper --version 2.1.28

//dotnet restore

### 2.4 生成密钥（可选）
```powershell
# 生成随机密钥
$key = -join ((65..90) + (97..122) | Get-Random -Count 32 | % {[char]$_})
echo $key
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

新增文件：
1. AuthService.cs → src/Services/
2. EmailService.cs → src/Services/
3. AuthHandler.cs → src/Network/
4. User.cs → src/Models/
5. Auth.proto → src/Protos/

### 3.2 生成 Protobuf 代码
```powershell
cd src
# 生成位置同步相关代码
protoc --csharp_out=./Protocol/Generated --proto_path=./Protos Map.proto
# 生成认证相关代码
protoc --csharp_out=./Protocol/Generated --proto_path=./Protos Battle.proto
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

### 4.4 测试邮件服务
1. 测试SMTP连接：
```powershell
telnet smtp.qq.com 587
```
//windows
Test-NetConnection smtp.qq.com -Port 587

2. 测试发送邮件：
```powershell
cd src
dotnet run -- test-email your-test@email.com
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

### 5.4 测试认证功能
1. 注册新用户：
```powershell
curl -X POST http://localhost:5000/register -d '{
  "email": "test@example.com",
  "password": "your-password"
}'
```

2. 验证邮箱：
```powershell
# 检查邮箱获取验证码
curl -X POST http://localhost:5000/verify -d '{
  "email": "test@example.com",
  "code": "123456"
}'
```

3. 登录测试：
```powershell
curl -X POST http://localhost:5000/login -d '{
  "email": "test@example.com",
  "password": "your-password"
}'
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

### 6.4 邮件发送失败
1. 检查SMTP配置：
   - 确认端口是否正确
   - 验证账号密码
   - 检查防火墙设置

2. QQ邮箱特别说明：
   - 需要使用授权码而不是密码
   - 可能需要设置白名单
   - 注意发送频率限制

### 6.5 JWT相关问题
1. 令牌生成失败：
   - 检查密钥长度（至少32字节）
   - 确认配置完整性
   - 验证时间设置

2. 令牌验证失败：
   - 检查令牌格式
   - 确认密钥一致
   - 验证时间有效性

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

### 7.3 JWT调试工具
1. jwt.io
   - 在线解析JWT令牌
   - 验证签名
   - 查看Payload

2. Postman
   - 测试认证接口
   - 管理令牌
   - 自动化测试 