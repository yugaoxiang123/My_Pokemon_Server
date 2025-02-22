# Pokemon Showdown 协议文档

## 1. 协议来源

Pokemon Showdown 的协议信息主要来自以下几个地方：

1. **官方文档**:
   - 协议文档: https://github.com/smogon/pokemon-showdown/blob/master/PROTOCOL.md
   - WebSocket 接口: https://github.com/smogon/pokemon-showdown/blob/master/server/sockets.ts

2. **源代码分析**:
   - 对战逻辑: https://github.com/smogon/pokemon-showdown/tree/master/sim
   - 消息处理: https://github.com/smogon/pokemon-showdown/tree/master/server/rooms

3. **客户端实现**:
   - 官方客户端: https://github.com/smogon/pokemon-showdown-client
   - 消息处理: https://github.com/smogon/pokemon-showdown-client/tree/master/src/battle

## 2. 协议验证方法

### 2.1 本地测试服务器
```bash
# 克隆服务器代码
git clone https://github.com/smogon/pokemon-showdown.git
cd pokemon-showdown

# 安装依赖
npm install

# 配置服务器
cp config/config-example.js config/config.js

# 启动服务器
node pokemon-showdown start

# 默认端口 8000
```

### 2.2 WebSocket 测试
```javascript
// 使用 wscat 工具测试
wscat -c ws://localhost:8000/showdown/websocket

// 发送消息示例
|/utm null
|/search gen9ou
```

### 2.3 关键消息格式

1. **对战初始化**:
```
>battle-gen9ou-1
|init|battle
|title|Username1 vs. Username2
|player|p1|Username1|1
|player|p2|Username2|2
```

2. **选择行动**:
```
>battle-gen9ou-1
|request|{"active":[{"moves":[...]}],"side":{"pokemon":[...]}}
```

3. **执行行动**:
```
>battle-gen9ou-1
|move|p1a: Pokemon|Move Name|p2a: Pokemon
|
|-damage|p2a: Pokemon|75/100
```

## 3. 重要注意事项

1. **协议版本**:
   - 当前使用的是 Gen 9 (第九世代) 的协议
   - 需要定期检查更新，确保兼容性

2. **消息格式**:
   - 所有消息都以 `|` 分隔
   - 房间ID在消息开头，格式为 `>battle-{format}-{number}`
   - 支持的对战格式在 `config/formats.ts` 中定义

3. **认证机制**:
   - 支持 OAuth2 认证
   - 也支持自定义认证服务器

4. **数据验证**:
   - 所有宝可梦数据需要符合官方规则
   - 技能、特性等需要在合法范围内

## 4. 调试工具

1. **Pokemon Showdown 开发工具**:
   ```bash
   # 启动开发模式
   node pokemon-showdown start --debug
   ```

2. **日志查看**:
   ```bash
   # 查看对战日志
   tail -f logs/battles/battle-{format}-{number}.log
   ```

3. **数据验证工具**:
   ```bash
   # 验证队伍合法性
   node tools/validate-team.js "团队数据字符串"
   ```

## 5. 常见问题

1. **连接问题**:
   - 确保 WebSocket 连接使用正确的协议版本
   - 检查防火墙设置

2. **消息同步**:
   - 所有消息都是有序的
   - 需要正确处理消息队列

3. **错误处理**:
   - 非法操作会返回错误消息
   - 需要实现重连机制

## 6. 参考资源

1. **官方资源**:
   - Smogon 论坛: https://www.smogon.com/forums/
   - Pokemon Showdown 主页: https://pokemonshowdown.com/

2. **开发资源**:
   - API 文档: https://github.com/smogon/pokemon-showdown/tree/master/docs
   - 开发指南: https://github.com/smogon/pokemon-showdown/blob/master/CONTRIBUTING.md

3. **社区资源**:
   - Discord: https://discord.gg/showdown
   - Bug 追踪: https://github.com/smogon/pokemon-showdown/issues 