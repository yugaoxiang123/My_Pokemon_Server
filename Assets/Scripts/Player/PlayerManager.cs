using UnityEngine;
using MyPokemon.Protocol;
using System.Collections.Generic;

public class PlayerManager : MonoBehaviour
{
    public GameObject playerPrefab;
    private Dictionary<string, GameObject> players = new Dictionary<string, GameObject>();

    void Start()
    {
        // 订阅网络消息事件
        NetworkManager.Instance.OnPlayerJoined += HandlePlayerJoined;
        NetworkManager.Instance.OnPlayerLeft += HandlePlayerLeft;
        NetworkManager.Instance.OnInitialPlayers += HandleInitialPlayers;
        NetworkManager.Instance.OnPositionUpdate += HandlePositionUpdate;
    }

    private void HandlePlayerJoined(PlayerJoinedMessage msg)
    {
        if (!players.ContainsKey(msg.PlayerId))
        {
            GameLogger.LogPlayer($"新玩家加入 - ID: {msg.PlayerId}, " +
                               $"位置: ({msg.X:F2}, {msg.Y:F2}), " +
                               $"朝向: {msg.Direction}, 状态: {msg.MotionState}");

            var playerObj = Instantiate(playerPrefab, new Vector3(msg.X, msg.Y, 0), Quaternion.identity);
            
            // 设置初始状态
            var animator = playerObj.GetComponent<Animator>();
            if (animator != null)
            {
                animator.SetInteger("Direction", (int)msg.Direction);
                animator.SetInteger("MotionState", (int)msg.MotionState);
            }

            players.Add(msg.PlayerId, playerObj);
            GameLogger.LogPlayer($"玩家对象已创建 - 当前在线玩家数: {players.Count}");
        }
        else
        {
            GameLogger.LogPlayer($"收到重复的玩家加入消息 - ID: {msg.PlayerId}");
        }
    }

    private void HandlePlayerLeft(PlayerLeftMessage msg)
    {
        if (players.TryGetValue(msg.PlayerId, out var playerObj))
        {
            GameLogger.LogPlayer($"玩家离开 - ID: {msg.PlayerId}");
            Destroy(playerObj);
            players.Remove(msg.PlayerId);
            GameLogger.LogPlayer($"玩家对象已销毁 - 当前在线玩家数: {players.Count}");
        }
        else
        {
            GameLogger.LogPlayer($"收到未知玩家的离开消息 - ID: {msg.PlayerId}");
        }
    }

    private void HandleInitialPlayers(InitialPlayersMessage msg)
    {
        GameLogger.LogPlayer($"收到初始玩家列表 - 玩家数量: {msg.Players.Count}");
        foreach (var player in msg.Players)
        {
            if (!players.ContainsKey(player.PlayerId))
            {
                GameLogger.LogPlayer($"加载现有玩家 - ID: {player.PlayerId}, 位置: ({player.X:F2}, {player.Y:F2}), 朝向: {player.Direction}");
                var playerObj = Instantiate(playerPrefab, 
                    new Vector3(player.X, player.Y, 0), 
                    Quaternion.identity);
                players.Add(player.PlayerId, playerObj);
            }
            else
            {
                GameLogger.LogPlayer($"跳过已存在的玩家 - ID: {player.PlayerId}");
            }
        }
        GameLogger.LogPlayer($"初始玩家加载完成 - 当前在线玩家数: {players.Count}");
    }

    private void HandlePositionUpdate(PlayerPosition pos)
    {
        if (players.TryGetValue(pos.PlayerId, out var playerObj))
        {
            playerObj.transform.position = new Vector3(pos.X, pos.Y, 0);
            
            // 获取或添加动画组件
            var animator = playerObj.GetComponent<Animator>();
            if (animator != null)
            {
                animator.SetInteger("Direction", (int)pos.Direction);
                animator.SetInteger("MotionState", (int)pos.MotionState);
            }

            // 处理精灵翻转
            var spriteRenderer = playerObj.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = pos.Direction == MoveDirection.MoveDirectionLeft;
            }

            GameLogger.LogPlayer($"更新玩家位置 - ID: {pos.PlayerId}, " +
                               $"位置: ({pos.X:F2}, {pos.Y:F2}), " +
                               $"朝向: {pos.Direction}, 状态: {pos.MotionState}");
        }
        else
        {
            GameLogger.LogPlayer($"收到未知玩家的位置更新 - ID: {pos.PlayerId}");
        }
    }

    void OnDestroy()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnPlayerJoined -= HandlePlayerJoined;
            NetworkManager.Instance.OnPlayerLeft -= HandlePlayerLeft;
            NetworkManager.Instance.OnInitialPlayers -= HandleInitialPlayers;
            NetworkManager.Instance.OnPositionUpdate -= HandlePositionUpdate;
        }
    }
}