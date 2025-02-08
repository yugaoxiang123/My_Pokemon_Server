using System.Data;
using Npgsql;
using Dapper;
using Microsoft.Extensions.Configuration;
using MyPokemon.Models;
using MyPokemon.Utils;
namespace MyPokemon.Services;
using MoveDirection = MyPokemon.Protocol.MoveDirection;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(IConfiguration config)
    {
        _connectionString = config["Database:ConnectionString"] ?? 
            throw new Exception("数据库连接字符串未配置");
    }

    private IDbConnection CreateConnection()
        => new NpgsqlConnection(_connectionString);

    public async Task<User?> GetUserByEmail(string email)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<User>(@"
            SELECT 
                id,  -- 让 Dapper 自动处理 UUID
                player_name as PlayerName,
                email as Email,
                password_hash as PasswordHash,
                is_email_verified as IsEmailVerified,
                verification_code as VerificationCode,
                verification_code_expires_at as VerificationCodeExpiresAt,
                created_at as CreatedAt,
                last_login_at as LastLoginAt,
                last_position_x as LastPositionX,
                last_position_y as LastPositionY,
                last_direction as LastDirection
            FROM users 
            WHERE email = @Email",
            new { Email = email }
        );
    }

    public async Task<User?> GetUserByPlayerName(string playerName)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<User>(@"
            SELECT 
                id,
                player_name as PlayerName,
                email as Email,
                password_hash as PasswordHash,
                is_email_verified as IsEmailVerified,
                verification_code as VerificationCode,
                verification_code_expires_at as VerificationCodeExpiresAt,
                created_at as CreatedAt,
                last_login_at as LastLoginAt,
                last_position_x as LastPositionX,
                last_position_y as LastPositionY,
                last_direction as LastDirection
            FROM users 
            WHERE player_name = @PlayerName",
            new { PlayerName = playerName }
        );
    }

    public async Task<bool> CreateUser(User user)
    {
        try 
        {
            using var conn = CreateConnection();
            var result = await conn.ExecuteAsync(@"
                INSERT INTO users (
                    id, 
                    player_name, 
                    email, 
                    password_hash, 
                    is_email_verified,
                    verification_code, 
                    verification_code_expires_at,
                    created_at,
                    last_login_at,
                    last_position_x,
                    last_position_y,
                    last_direction
                ) VALUES (
                    @Id::uuid,
                    @PlayerName,
                    @Email,
                    @PasswordHash,
                    @IsEmailVerified,
                    @VerificationCode,
                    @VerificationCodeExpiresAt,
                    @CreatedAt,
                    @LastLoginAt,
                    @LastPositionX,
                    @LastPositionY,
                    @LastDirection
                )",
                user);
            
            ServerLogger.LogNetwork($"创建用户成功 - Email: {user.Email}");
            return result > 0;
        }
        catch (Exception e)
        {
            ServerLogger.LogError($"创建用户失败: {e.Message}");
            throw;
        }
    }

    public async Task<bool> UpdateEmailVerification(string email)
    {
        using var conn = CreateConnection();
        var result = await conn.ExecuteAsync(
            "UPDATE users SET is_email_verified = true WHERE email = @Email",
            new { Email = email }
        );
        return result > 0;
    }

    public async Task<bool> UpdateLastLogin(string email)
    {
        using var conn = CreateConnection();
        var result = await conn.ExecuteAsync(
            "UPDATE users SET last_login_at = CURRENT_TIMESTAMP WHERE email = @Email",
            new { Email = email }
        );
        return result > 0;
    }

    public async Task<bool> UpdateVerificationCode(string email, string code, DateTime expiresAt)
    {
        using var conn = CreateConnection();
        var result = await conn.ExecuteAsync(@"
            UPDATE users 
            SET verification_code = @Code, verification_code_expires_at = @ExpiresAt
            WHERE email = @Email",
            new { Email = email, Code = code, ExpiresAt = expiresAt }
        );
        return result > 0;
    }

    public async Task<bool> UpdateLastPosition(string playerId, float x, float y, MoveDirection direction)
    {
        using var conn = CreateConnection();
        var result = await conn.ExecuteAsync(@"
            UPDATE users 
            SET last_position_x = @X, 
                last_position_y = @Y, 
                last_direction = @Direction
            WHERE player_name = @PlayerId",
            new { 
                PlayerId = playerId, 
                X = x, 
                Y = y, 
                Direction = (int)direction
            }
        );
        return result > 0;
    }
} 