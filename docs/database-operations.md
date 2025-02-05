# PostgreSQL 数据库操作指南


# 启动数据库服务
pg_ctl -D C:\PostgreSQL\data start
```

## 1. 连接数据库

```bash
# 使用 postgres 用户连接
psql -U postgres -d pokemon

# 使用 pokemon_user 连接
psql -U pokemon_user -d pokemon
```

## 2. 查看数据

```sql
-- 查看所有表
\dt

-- 查看表结构
\d users

-- 查看表的详细信息（包含注释）
\d+ users

-- 查看所有用户数据
SELECT * FROM users;

-- 按条件查询
SELECT * FROM users WHERE email = 'example@email.com';
SELECT * FROM users WHERE player_name = 'playername';

-- 查看特定字段
SELECT email, player_name, created_at FROM users;
```

## 3. 删除数据

```sql
-- 删除特定用户
DELETE FROM users WHERE email = 'example@email.com';
DELETE FROM users WHERE player_name = 'playername';

-- 删除所有用户数据（谨慎使用！）
DELETE FROM users;

-- 重置自增ID（如果有的话）
TRUNCATE TABLE users RESTART IDENTITY;
```

## 4. 修改数据

```sql
-- 更新用户信息
UPDATE users SET player_name = 'newname' WHERE email = 'example@email.com';

-- 更新多个字段
UPDATE users 
SET last_position_x = 0, 
    last_position_y = 0, 
    last_direction = 0 
WHERE email = 'example@email.com';
```

## 5. 其他常用命令

```sql
-- 查看数据库大小
SELECT pg_size_pretty(pg_database_size('pokemon'));

-- 查看表大小
SELECT pg_size_pretty(pg_total_relation_size('users'));

-- 查看表的行数
SELECT COUNT(*) FROM users;

-- 退出psql
\q
```

## 6. 备份和恢复

```bash
# 备份数据库
pg_dump -U postgres pokemon > backup.sql

# 恢复数据库
psql -U postgres pokemon < backup.sql
```

## 7. 注意事项

1. 在执行删除操作前先备份数据
2. 使用 WHERE 子句限制影响范围
3. 在生产环境谨慎使用 DELETE/TRUNCATE
4. 保持良好的查询习惯，避免全表扫描
5. 定期维护和清理数据 