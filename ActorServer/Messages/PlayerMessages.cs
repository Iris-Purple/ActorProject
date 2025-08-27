using Akka.Actor;
using ActorServer.Messages;
using ActorServer.Zone;

namespace ActorServer.Messages;

// ============================================
// PlayerActor 수신 메시지 (Incoming)
// ============================================

/// <summary>
/// Zone 변경 완료 알림 (ZoneActor → PlayerActor)
/// </summary>
public record ZoneChanged(
    ZoneId NewZoneId,
    Position SpawnPosition,
    int PlayerCount = 0
);

public record ZoneFull(ZoneId ZoneId);

public record ChatMessage(string Message);

/// <summary>
/// 플레이어 정보 조회 요청
/// </summary>
public record GetPlayerInfo();

/// <summary>
/// 플레이어 정보 응답 (PlayerActor → 요청자)
/// </summary>
public record PlayerInfoResponse(
    long PlayerId,
    Position Position,
    ZoneId ZoneId,
    bool IsOnline,
    DateTime? LastActive = null
);

public record PlayerInfo(IActorRef Actor, long PlayerId, Position Position);

public record SetClientConnection(IActorRef ClientActor);
