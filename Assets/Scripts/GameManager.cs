using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public string serverHost = "127.0.0.1";
    public int serverPort = 5000;
    
    private void Start()
    {
        if (string.IsNullOrEmpty(PlayerData.Instance?.Token))
        {
            // 如果没有Token，返回登录场景
            SceneManager.LoadScene("LoginScene");
            return;
        }

        // 连接服务器
        NetworkManager.Instance.Connect(serverHost, serverPort);
    }

    private void OnApplicationQuit()
    {
        // 清理玩家数据
        PlayerData.Instance?.Clear();
    }
} 