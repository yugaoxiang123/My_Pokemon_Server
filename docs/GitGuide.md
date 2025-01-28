# Git 使用指南

## 1. 基本配置
```bash
# 设置用户信息
git config --global user.name "你的名字"
git config --global user.email "你的邮箱"
```

## 2. 项目版本控制

### 2.1 初始化仓库
```bash
cd my-pokemon
git init
```

### 2.2 添加文件
```bash
# 添加所有文件
git add .

# 或添加特定文件
git add src/Program.cs
```

### 2.3 提交更改
```bash
git commit -m "描述你的更改"
```

### 2.4 查看状态
```bash
# 查看仓库状态
git status

# 查看提交历史
git log
```

## 3. 分支管理

### 3.1 创建分支
```bash
# 创建新分支
git branch feature-position-sync

# 切换到新分支
git checkout feature-position-sync

# 或者一步完成
git checkout -b feature-position-sync
```

### 3.2 合并分支
```bash
# 切回主分支
git checkout main

# 合并功能分支
git merge feature-position-sync
```

## 4. 常用命令
- `git status` - 查看仓库状态
- `git add` - 添加文件到暂存区
- `git commit` - 提交更改
- `git log` - 查看提交历史
- `git branch` - 查看分支
- `git checkout` - 切换分支
- `git merge` - 合并分支 