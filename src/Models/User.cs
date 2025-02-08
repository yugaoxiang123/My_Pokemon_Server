using MoveDirection = MyPokemon.Protocol.MoveDirection;

namespace MyPokemon.Models;

public class User
{
    public Guid Id { get; set; }
    public required string PlayerName { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public bool IsEmailVerified { get; set; }
    public string? VerificationCode { get; set; }
    public DateTime? VerificationCodeExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public float LastPositionX { get; set; }
    public float LastPositionY { get; set; }
    public MoveDirection LastDirection { get; set; }
} 