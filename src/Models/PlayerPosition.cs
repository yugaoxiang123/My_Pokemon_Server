using MoveDirection = MyPokemon.Protocol.MoveDirection;
using MotionStates = MyPokemon.Protocol.MotionStates;

namespace MyPokemon.Models;

public class PlayerPosition
{
    public required string PlayerId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public MoveDirection Direction { get; set; }
    public DateTime LastUpdateTime { get; set; }
    public MotionStates MotionState { get; set; } = MotionStates.Idle;
}