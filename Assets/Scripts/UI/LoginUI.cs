using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MyPokemon.Protocol;
using System;

public class LoginUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField emailInput;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private TMP_InputField otherInput;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button registerButton;
    [SerializeField] private Button verifyButton;
    [SerializeField] private TextMeshProUGUI messageText;

    private void Start()
    {
        loginButton.onClick.AddListener(OnLoginClick);
        registerButton.onClick.AddListener(OnRegisterClick);
        verifyButton.onClick.AddListener(OnVerifyClick);
        AuthManager.Instance.OnLoginResponse += HandleLoginResponse;
        AuthManager.Instance.OnRegisterResponse += HandleRegisterResponse;
    }

    private void OnVerifyClick()
    {
        string email = emailInput.text;
        string code = otherInput.text;
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(code))
        {
            messageText.text = "请输入邮箱和验证码";
            return;
        }

        AuthManager.Instance.VerifyEmail(email, code);
        messageText.text = "登录中...";
    }

    private void OnLoginClick()
    {
        string email = emailInput.text;
        string password = passwordInput.text;
        
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            messageText.text = "请输入邮箱和密码";
            return;
        }

        AuthManager.Instance.Login(email, password);
        messageText.text = "登录中...";
    }

    private void OnRegisterClick()
    {
        string email = emailInput.text;
        string password = passwordInput.text;
        string playerName = otherInput.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            messageText.text = "请输入邮箱和密码";
            return;
        }

        AuthManager.Instance.Register(email, password,playerName);
        messageText.text = "注册中...";
    }

    private void HandleLoginResponse(LoginResponse response)
    {
        messageText.text = response.Message;
        if (response.Success)
        {
            // 保存玩家数据
            PlayerData.Instance.UpdateFromLoginResponse(response);
            // 切换到游戏场景
            SceneManager.LoadScene("GameScene");
        }
    }

    private void HandleRegisterResponse(RegisterResponse response)
    {
        messageText.text = response.Message;
    }

    private void OnDestroy()
    {
        if (AuthManager.Instance != null)
        {
            AuthManager.Instance.OnLoginResponse -= HandleLoginResponse;
            AuthManager.Instance.OnRegisterResponse -= HandleRegisterResponse;
        }
    }
} 