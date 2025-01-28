using UnityEngine;

public class GameManager : MonoBehaviour
{
    public string serverHost = "localhost";
    public int serverPort = 5000;
    
    private void Start()
    {
        // 连接服务器
        NetworkManager.Instance.Connect(serverHost, serverPort);
    }
} 