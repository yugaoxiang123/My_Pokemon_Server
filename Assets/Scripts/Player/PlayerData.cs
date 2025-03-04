using UnityEngine;
using MyPokemon.Protocol;

[CreateAssetMenu(fileName = "PlayerData", menuName = "Game/PlayerData")]
public class PlayerData : ScriptableObject
{
    private static PlayerData instance;
    public static PlayerData Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<PlayerData>("PlayerData");
                if (instance == null)
                {
                    GameLogger.LogError("无法加载PlayerData资源，请确保在Resources文件夹中创建PlayerData资源");
                }
            }
            return instance;
        }
    }

    [SerializeField] private string playerName;
    [SerializeField] private float positionX;
    [SerializeField] private float positionY;
    [SerializeField] private MoveDirection direction;
    [SerializeField] private string token;

    public string PlayerName => playerName;
    public float PositionX => positionX;
    public float PositionY => positionY;
    public MoveDirection Direction => direction;
    public string Token => token;

    public void UpdateFromLoginResponse(MyPokemon.Protocol.LoginResponse response)
    {
        playerName = response.PlayerName;
        positionX = response.PositionX;
        positionY = response.PositionY;
        direction = response.Direction;
        token = response.Token;
    }

    public Vector3 GetPosition()
    {
        return new Vector3(positionX, positionY, 0);
    }

    public void Clear()
    {
        playerName = string.Empty;
        positionX = 0;
        positionY = 0;
        direction = MoveDirection.None;
        token = string.Empty;
    }
} 