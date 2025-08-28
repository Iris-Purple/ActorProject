using Akka.Actor;
using ActorServer.Zone;

namespace ActorServer.Messages;


// ============================================
// Zone Manager 메시지
// ============================================

public record ChangeZoneRequest(
    IActorRef PlayerActor, 
    long PlayerId,
    ZoneId TargetZoneId
);

public record PlayerMove(
    IActorRef PlayerActor, 
    long PlayerId,
    float X,
    float Y
);

// 추가: 이동 완료 응답
public record PlayerMoved(
    long PlayerId,
    float X,
    float Y
);
