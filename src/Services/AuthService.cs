using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Distributed;
using MyPokemon.Models;
using MyPokemon.Utils;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace MyPokemon.Services;

public class AuthService
{
    private readonly IDistributedCache _cache;
    private readonly IConfiguration _config;
    private readonly EmailService _emailService;
    private readonly DatabaseService _db;
    private const string USER_KEY = "user:";
    private const string VERIFY_CODE_KEY = "verify:";

    public AuthService(
        IDistributedCache cache, 
        IConfiguration config, 
        EmailService emailService,
        DatabaseService db)
    {
        _cache = cache;
        _config = config;
        _emailService = emailService;
        _db = db;
    }

    // 注册
    public async Task<(bool success, string message)> Register(string email, string password, string playerName)
    {
        try 
        {
            // 检查邮箱是否已注册
            var existingUser = await _db.GetUserByEmail(email);
            if (existingUser is not null)
            {
                return (false, "邮箱已被注册");
            }

            // 检查玩家名称是否已被使用
            var existingName = await _db.GetUserByPlayerName(playerName);
            if (existingName is not null)
            {
                return (false, "玩家名称已被使用");
            }

            // 验证玩家名称格式
            if (!IsValidPlayerName(playerName))
            {
                return (false, "玩家名称不符合要求（只能包含字母、数字和下划线，长度2-20个字符）");
            }

            // 创建临时用户数据
            var verificationCode = GenerateVerificationCode();
            var tempUser = new User
            {
                Id = Guid.NewGuid(),
                PlayerName = playerName,
                Email = email,
                PasswordHash = HashPassword(password),
                IsEmailVerified = false,
                VerificationCode = verificationCode,
                VerificationCodeExpiresAt = DateTime.UtcNow.AddMinutes(15),
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = null,
                LastPositionX = 0,
                LastPositionY = 0,
                LastDirection = 0
            };

            // 保存到 Redis 临时存储
            var json = System.Text.Json.JsonSerializer.Serialize(tempUser);
            await _cache.SetStringAsync(
                VERIFY_CODE_KEY + email,
                json,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
                }
            );

            // 发送验证邮件
            await _emailService.SendEmailAsync(
                email,
                "验证您的邮箱",
                $"您的验证码是: {verificationCode}"
            );

            return (true, "验证码已发送到您的邮箱，请在15分钟内完成验证");
        }
        catch (Exception e)
        {
            ServerLogger.LogError($"注册失败: {e.Message}");
            return (false, "注册失败，请稍后重试");
        }
    }

    // 登录
    public async Task<(bool success, string message, string? token, string? playerName, float positionX, float positionY, int direction)> Login(string email, string password)
    {
        try
        {
            User? user = await _db.GetUserByEmail(email);
            if (user is null)
            {
                return (false, "用户不存在", null, null, 0, 0, 0);
            }

            if (!VerifyPassword(password, user.PasswordHash))
            {
                return (false, "密码错误", null, null, 0, 0, 0);
            }

            if (!user.IsEmailVerified)
            {
                return (false, "请先验证邮箱", null, null, 0, 0, 0);
            }

            // 生成JWT令牌
            var token = GenerateJwtToken(user);

            // 更新最后登录时间
            user.LastLoginAt = DateTime.UtcNow;
            await _db.UpdateLastLogin(email);

            return (true, "登录成功", token, user.PlayerName, user.LastPositionX, user.LastPositionY, user.LastDirection);
        }
        catch (Exception e)
        {
            ServerLogger.LogError($"登录失败: {e.Message}");
            return (false, "登录失败,请稍后重试", null, null, 0, 0, 0);
        }
    }

    // 验证邮箱
    public async Task<(bool success, string message)> VerifyEmail(string email, string code)
    {
        try
        {
            // 添加日志
            ServerLogger.LogNetwork($"开始验证邮箱 - Email: {email}, Code: {code}");
            
            // 从 Redis 获取临时用户数据
            var json = await _cache.GetStringAsync(VERIFY_CODE_KEY + email);
            if (json == null)
            {
                ServerLogger.LogNetwork("验证码不存在或已过期");
                return (false, "验证码已过期，请重新注册");
            }

            // 添加日志
            ServerLogger.LogNetwork($"从Redis获取的数据: {json}");

            User? tempUser = System.Text.Json.JsonSerializer.Deserialize<User>(json);
            if (tempUser == null)
            {
                ServerLogger.LogNetwork("反序列化用户数据失败");
                return (false, "验证失败，请重新注册");
            }

            if (tempUser.VerificationCode != code)
            {
                return (false, "验证码错误");
            }

            if (DateTime.UtcNow > tempUser.VerificationCodeExpiresAt)
            {
                return (false, "验证码已过期，请重新注册");
            }

            // 验证成功，保存到数据库
            tempUser.IsEmailVerified = true;
            await _db.CreateUser(tempUser);

            // 清除 Redis 中的临时数据
            await _cache.RemoveAsync(VERIFY_CODE_KEY + email);

            return (true, "邮箱验证成功，请登录");
        }
        catch (Exception e)
        {
            // 详细记录异常
            ServerLogger.LogError($"邮箱验证失败: {e.Message}\n堆栈跟踪: {e.StackTrace}");
            return (false, "验证失败，请稍后重试");
        }
    }

    // 生成验证码
    private string GenerateVerificationCode()
    {
        return Random.Shared.Next(100000, 999999).ToString();
    }

    // 根据邮箱获取用户
    private async Task<User?> GetUserByEmail(string email)
    {
        var json = await _cache.GetStringAsync(USER_KEY + email);
        return json == null ? null : System.Text.Json.JsonSerializer.Deserialize<User>(json);
    }

    // 密码哈希
    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }

    // 验证密码
    private bool VerifyPassword(string password, string hash)
    {
        return HashPassword(password) == hash;
    }

    // 生成JWT令牌
    private string GenerateJwtToken(User? user)
    {
        if (user is null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:SecretKey"] ?? throw new Exception("JWT密钥未配置"))
        );

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: new[]
            {
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
            },
            expires: DateTime.Now.AddDays(7),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private bool IsValidPlayerName(string playerName)
    {
        // 只允许字母、数字和下划线，长度4-20个字符
        return !string.IsNullOrEmpty(playerName) 
               && playerName.Length >= 2 
               && playerName.Length <= 20
               && System.Text.RegularExpressions.Regex.IsMatch(playerName, @"^[a-zA-Z0-9_]+$");
    }
} 