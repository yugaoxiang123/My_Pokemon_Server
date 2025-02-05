# CentOS PostgreSQL 安装配置指南

## 1. 安装 PostgreSQL

### 1.1 清理环境（如果之前安装过）
```bash
# 删除旧的 PostgreSQL 仓库
sudo rm -f /etc/yum.repos.d/pgdg*

# 清理 yum 缓存
sudo yum clean all
sudo yum makecache
```

### 1.2 安装 PostgreSQL
```bash
# 更新系统
sudo yum update -y

# 安装 PostgreSQL 服务器和贡献包
sudo yum install -y postgresql-server postgresql-contrib
```

### 1.3 初始化数据库
```bash
# 初始化数据库
sudo postgresql-setup initdb

# 启动服务
sudo systemctl start postgresql

# 设置开机自启
sudo systemctl enable postgresql

# 检查服务状态
sudo systemctl status postgresql
```

## 2. 配置 PostgreSQL

### 2.1 修改密码和创建数据库
```bash
# 切换到 postgres 用户
sudo -i -u postgres

# 进入 PostgreSQL 命令行
psql

# 修改 postgres 用户密码
\password postgres
# 输入: ygx131953

# 创建游戏数据库和用户
CREATE DATABASE pokemon;
CREATE USER pokemon_user WITH PASSWORD 'ygx131953';
GRANT ALL PRIVILEGES ON DATABASE pokemon TO pokemon_user;

# 退出 psql
\q
```

### 2.2 配置远程访问
```bash
# 编辑配置文件
sudo nano /var/lib/pgsql/data/postgresql.conf
```

修改以下行：
```
listen_addresses = '*'
```

```bash
# 编辑客户端认证配置
sudo nano /var/lib/pgsql/data/pg_hba.conf
```

添加以下行（允许远程访问）：
```
host    all             all             0.0.0.0/0               scram-sha-256
```

### 2.3 重启服务
```bash
sudo systemctl restart postgresql
```

## 3. 创建数据库表

```bash
# 连接到数据库
psql -U postgres -d pokemon
```

```sql
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

## 4. 防火墙配置

```bash
# 开放 PostgreSQL 端口
sudo firewall-cmd --permanent --add-port=5432/tcp
sudo firewall-cmd --reload
```

## 5. 常用操作命令

```bash
# 启动服务
sudo systemctl start postgresql

# 停止服务
sudo systemctl stop postgresql

# 重启服务
sudo systemctl restart postgresql

# 查看服务状态
sudo systemctl status postgresql

# 连接数据库
psql -U pokemon_user -d pokemon

# 备份数据库
pg_dump -U postgres pokemon > backup.sql

# 还原数据库
psql -U postgres pokemon < backup.sql
```

## 6. 常见问题处理

### 6.1 权限问题
```bash
# 修改数据目录权限
sudo chown -R postgres:postgres /var/lib/pgsql/data/
sudo chmod 700 /var/lib/pgsql/data/
```

### 6.2 连接问题
```bash
# 查看日志
sudo tail -f /var/lib/pgsql/data/log/postgresql-*.log
```

### 6.3 SELinux 设置
```bash
# 允许 PostgreSQL 网络访问
sudo setsebool -P postgresql_tcp_connections on
``` 