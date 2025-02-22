# Pokemon Showdown 消息定义对照表

## 1. 对战初始化消息

### 1.1 对战请求 (BattleRequest)
**我方协议定义**:
```protobuf
message BattleRequest {
    string player_id = 1;           // 发起对战的玩家ID
    string opponent_id = 2;         // 对手ID(可选,为空则匹配)
    BattleFormat format = 3;        // 对战格式
    repeated Pokemon team = 4;      // 对战队伍
}
```

**Showdown 对应格式**:
```
|/search [format]
|/challenge [username], [format]
```
参考: https://github.com/smogon/pokemon-showdown/blob/master/PROTOCOL.md#global-messages

### 1.2 对战开始 (BattleResponse)
**我方协议定义**:
```protobuf
message BattleResponse {
    bool success = 1;
    string message = 2;
    string battle_id = 3;
    string opponent_id = 4;
}
```

**Showdown 对应格式**:
```
>battle-gen9ou-1
|init|battle
|player|p1|Username1|1
|player|p2|Username2|2
```
参考: https://github.com/smogon/pokemon-showdown/blob/master/PROTOCOL.md#battle-messages

## 2. 对战行动消息

### 2.1 行动选择 (BattleAction)
**我方协议定义**:
```protobuf
message BattleAction {
    string battle_id = 1;
    string player_id = 2;
    ActionType action_type = 3;
    int32 move_index = 4;
    int32 target_position = 5;
    int32 switch_position = 6;
}
```

**Showdown 对应格式**:
```
>battle-gen9ou-1
|/move [move], [target]
|/switch [position]
```
参考: https://github.com/smogon/pokemon-showdown/blob/master/PROTOCOL.md#battle-choices

### 2.2 状态更新 (BattleStateUpdate)
**我方协议定义**:
```protobuf
message BattleStateUpdate {
    string battle_id = 1;
    repeated PokemonState active = 2;
    repeated PokemonState bench = 3;
    repeated BattleEffect effects = 4;
    string last_action_description = 5;
}
```

**Showdown 对应格式**:
```
|-damage|p2a: Pokemon|75/100
|-status|p1a: Pokemon|brn
|-weather|Sandstorm
```
参考: https://github.com/smogon/pokemon-showdown/blob/master/sim/SIM-PROTOCOL.md

## 3. 宝可梦数据

### 3.1 宝可梦信息 (Pokemon)
**我方协议定义**:
```protobuf
message Pokemon {
    string species = 1;
    string item = 2;
    string ability = 3;
    repeated string moves = 4;
    string nature = 5;
    EVs evs = 6;
    IVs ivs = 7;
    int32 level = 8;
    bool shiny = 9;
}
```

**Showdown 对应格式**:
```
Pikachu|lifeorb|static|thunderbolt,volttackle,grassknot,protect|Jolly|,,,252,4,252|31,31,31,31,31,31||50|S|
```
参考: https://github.com/smogon/pokemon-showdown/blob/master/sim/TEAMS.md

## 4. 消息验证

### 4.1 测试方法
```bash
# 使用 wscat 测试消息格式
wscat -c ws://localhost:8000/showdown/websocket

# 发送测试消息
>battle-gen9ou-1
|/move 1
```
参考: https://github.com/smogon/pokemon-showdown/blob/master/CONTRIBUTING.md#testing

### 4.2 错误处理
- 非法操作返回: `|error|[错误信息]`
- 超时处理: `|inactive|[玩家] 已超时`
- 断线处理: `|player|[玩家]|null`

参考: https://github.com/smogon/pokemon-showdown/blob/master/PROTOCOL.md#global-messages 