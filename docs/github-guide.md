# GitHub 操作指南 - My Pokemon 项目

## 1. 初始配置

### 1.1 本地配置
```bash
# 进入项目目录
cd C:\Users\90953\Downloads\baokemeng\my-pokemon

# 配置用户信息
git config --global user.name "yugaoxiang123"
git config --global user.email "909532614@qq.com"

# 生成 SSH 密钥
ssh-keygen -t rsa -b 4096 -C "909532614@qq.com"

# 查看并复制公钥
cat ~/.ssh/id_rsa.pub  # Windows 用户使用: type %USERPROFILE%\.ssh\id_rsa.pub
```

### 1.2 GitHub 配置
1. 登录 GitHub 账号
2. 进入 Settings -> SSH and GPG keys
3. 点击 "New SSH key"
4. 粘贴刚才复制的公钥内容
5. 点击 "Add SSH key"

## 2. 项目初始化

### 2.1 初始化本地仓库
```bash
# 进入项目目录
cd C:\Users\90953\Downloads\baokemeng\my-pokemon

# 初始化 Git 仓库
git init

# 添加远程仓库
git remote add origin git@github.com:yugaoxiang123/my-pokemon.git
```

### 2.2 创建 .gitignore 文件
```bash
# 在项目根目录创建 .gitignore 文件
echo "bin/
obj/
.vs/
*.user
*.suo
.vscode/
*.cache" > .gitignore
```

## 3. 提交代码

### 3.1 首次提交
```bash
# 添加所有文件
git add .

# 提交更改
git commit -m "初始化提交: Pokemon 多人游戏服务端"

# 推送到主分支
git push -u origin main
```

### 3.2 后续更新
```bash
# 查看变更
git status

# 添加修改
git add .

# 提交变更
git commit -m "更新: 描述你的更改"

# 推送到远程
git push origin main
```

## 4. 团队协作

### 4.1 邀请团队成员
1. 在 GitHub 仓库页面
2. 点击 Settings -> Collaborators
3. 点击 "Add people"
4. 输入团队成员的 GitHub 用户名或邮箱
5. 选择适当的权限角色

### 4.2 团队成员加入
1. 成员接受邀请
2. 克隆仓库：
```bash
git clone git@github.com:yugaoxiang123/my-pokemon.git
```

### 4.3 分支管理
```bash
# 创建功能分支
git checkout -b feature/位置同步

# 完成功能后合并到主分支
git checkout main
git merge feature/位置同步
```

## 5. 日常工作流程

### 5.1 开始新功能
```bash
# 更新主分支
git checkout main
git pull origin main

# 创建新分支
git checkout -b feature/新功能
```

### 5.2 提交更改
```bash
# 查看更改
git status

# 添加更改
git add .

# 提交
git commit -m "feat: 添加新功能描述"

# 推送到远程
git push origin feature/新功能
```

### 5.3 创建合并请求
1. 在 GitHub 网页上创建 Pull Request
2. 从你的功能分支合并到 main 分支
3. 等待审核和合并

## 6. 注意事项

1. 不要直接在 main 分支上开发
2. 经常同步远程代码：`git pull origin main`
3. 提交前先测试代码
4. 写清晰的提交信息
5. 定期清理本地分支

## 7. 安全提示

⚠️ 注意：请立即更改你的 GitHub 密码，因为它已经在文档中暴露。
建议开启双因素认证(2FA)来提高账号安全性。 