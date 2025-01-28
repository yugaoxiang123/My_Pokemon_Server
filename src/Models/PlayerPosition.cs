public class PlayerPosition
{
    public required string PlayerId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public int Direction { get; set; }
    public DateTime LastUpdateTime { get; set; }
} 