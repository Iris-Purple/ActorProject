
namespace ActorServer.Zone; 

public enum ZoneId
{
    Empty = -1,
    Town = 0,
    Forest = 1,
}

// ============================================
// Zone 정보
// ============================================

public class ZoneData
{
    public ZoneId ZoneId { get; set; }  // 변경: enum 사용
    public string Name { get; set; } = "";
    public Position SpawnPoint { get; set; } = new(0, 0);
    public int MaxPlayers { get; set; } = 100;
}

public record Position(float X, float Y)
{
    public bool IsValid() => !float.IsNaN(X) && !float.IsNaN(Y) && 
                             !float.IsInfinity(X) && !float.IsInfinity(Y);
    
    public float DistanceTo(Position other)
    {
        var dx = other.X - X;
        var dy = other.Y - Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }
}