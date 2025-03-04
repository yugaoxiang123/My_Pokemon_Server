using UnityEngine;
using System;
using MyPokemon.Protocol;
using System.Threading.Tasks;

public class AuthManager : MonoBehaviour
{
    public static AuthManager Instance { get; private set; }
    
    // 认证相关事件
    public event Action<RegisterResponse> OnRegisterResponse;
    public event Action<LoginResponse> OnLoginResponse;
    public event Action<VerifyEmailResponse> OnVerifyEmailResponse;
    
    // 保存登录token
    private string authToken;
    public string AuthToken => authToken;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // 订阅网络消息
        NetworkManager.Instance.OnRegisterResponse += HandleRegisterResponse;
        NetworkManager.Instance.OnLoginResponse += HandleLoginResponse;
        NetworkManager.Instance.OnVerifyEmailResponse += HandleVerifyEmailResponse;
    }

    private void HandleRegisterResponse(RegisterResponse response)
    {
        GameLogger.LogAuth($"收到注册响应: {response.Message}");
        OnRegisterResponse?.Invoke(response);
    }

    private void HandleLoginResponse(LoginResponse response)
    {
        GameLogger.LogAuth($"收到登录响应: {response.Message}");
        if (response.Success)
        {
            authToken = response.Token;
        }
        OnLoginResponse?.Invoke(response);
    }

    private void HandleVerifyEmailResponse(VerifyEmailResponse response)
    {
        GameLogger.LogAuth($"收到邮箱验证响应: {response.Message}");
        OnVerifyEmailResponse?.Invoke(response);
    }

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnRegisterResponse -= HandleRegisterResponse;
            NetworkManager.Instance.OnLoginResponse -= HandleLoginResponse;
            NetworkManager.Instance.OnVerifyEmailResponse -= HandleVerifyEmailResponse;
        }
    }

    public void Register(string email, string password, string playerName)
    {
        var request = new RegisterRequest
        {
            Email = email,
            Password = password,
            PlayerName = playerName
        };

        NetworkManager.Instance.SendMapMessage(request);
        GameLogger.LogAuth($"发送注册请求 - Email: {email}");
    }

    public void Login(string email, string password)
    {
        var request = new LoginRequest
        {
            Email = email,
            Password = password
        };

        NetworkManager.Instance.SendMapMessage(request);
        GameLogger.LogAuth($"发送登录请求 - Email: {email}");
    }

    public void VerifyEmail(string email, string code)
    {
        var request = new VerifyEmailRequest
        {
            Email = email,
            Code = code
        };

        NetworkManager.Instance.SendMapMessage(request);
        GameLogger.LogAuth($"发送邮箱验证请求 - Email: {email}");
    }
} 