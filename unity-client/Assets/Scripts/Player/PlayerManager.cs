using UnityEngine;
using MyPokemon.Protocol;
using System.Collections.Generic;

public class PlayerManager : MonoBehaviour
{
    public GameObject playerPrefab;
    private Dictionary<string, GameObject> players = new Dictionary<string, GameObject>();

    void Start()
    {
        NetworkManager.Instance.OnPositionBroadcast += HandlePositionBroadcast;
    }

    private void HandlePositionBroadcast(PositionBroadcast broadcast)
    {
        foreach (var position in broadcast.Positions)
        {
            if (!players.ContainsKey(position.PlayerId))
            {
                GameLogger.LogPlayer($"新玩家加入: {position.PlayerId}");
                var playerObj = Instantiate(playerPrefab, 
                    new Vector3(position.X, position.Y, 0), 
                    Quaternion.identity);
                players.Add(position.PlayerId, playerObj);
            }

            var player = players[position.PlayerId];
            player.transform.position = new Vector3(position.X, position.Y, 0);
            GameLogger.LogPlayer($"更新玩家 {position.PlayerId} 位置: ({position.X:F2}, {position.Y:F2})");
            
            // 更新朝向
            float rotation = position.Direction * 90f;
            player.transform.rotation = Quaternion.Euler(0, 0, rotation);
        }
    }

    void OnDestroy()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnPositionBroadcast -= HandlePositionBroadcast;
        }
    }
} 