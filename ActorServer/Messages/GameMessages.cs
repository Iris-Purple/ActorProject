using Akka.Actor;

namespace ActorServer.Messages;

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

public record PlayerInfo(IActorRef Actor, long PlayerId, Position Position);

// ============================================
// 로그인/연결 관련
// ============================================

public record PlayerLoginRequest(long PlayerId);
public record PlayerDisconnect(long PlayerId);
public record RegisterClientConnection(long PlayerId, IActorRef ClientActor);
public record SetClientConnection(IActorRef ClientActor);

// ============================================
// 이동 관련 (Actor 내부 명령)
// ============================================

public record MoveCommand(Position NewPosition);  // Player에게 이동 명령

public record PlayerMovement(IActorRef PlayerActor, Position NewPosition);  // Zone에게 이동 알림

// ============================================
// 플레이어 명령 라우팅
// ============================================

public record PlayerCommand(long PlayerId, object? Command);

// ============================================
// Zone 이동 요청 (WorldActor → ZoneManager)
// ============================================

public record RequestZoneChange(long PlayerId, string TargetZoneId);

// ============================================
// 클라이언트 통신
// ============================================

public record ChatToClient(string From, string Message);
public record LoginFailed(long PlayerId, string Reason);
public record CommandFailed(long PlayerId, string Command, string Reason);