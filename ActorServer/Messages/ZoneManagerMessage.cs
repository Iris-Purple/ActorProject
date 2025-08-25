using Akka.Actor;

namespace ActorServer.Messages;

/// <summary>
/// PlayerActor에 ZoneManager 참조 설정
/// </summary>
public record SetZoneManager(IActorRef ZoneManager);

/// <summary>
/// ZoneManager에 Player 등록
/// </summary>
public record RegisterPlayer(
    long PlayerId, 
    IActorRef PlayerActor,
    string InitialZone = "town"
);

/// <summary>
/// ZoneManager에서 Player 제거
/// </summary>
public record UnregisterPlayer(long PlayerId);

/// <summary>
/// Player의 Zone 메시지 기본 클래스
/// </summary>
public abstract record PlayerZoneMessage(long PlayerId, string ZoneId);

/// <summary>
/// Zone 내 이동 메시지
/// </summary>
public record PlayerMoveInZone(long PlayerId, string ZoneId, Position NewPosition) 
    : PlayerZoneMessage(PlayerId, ZoneId);

/// <summary>
/// Zone 내 채팅 메시지
/// </summary>
public record PlayerChatInZone(long PlayerId, string ZoneId, string Message) 
    : PlayerZoneMessage(PlayerId, ZoneId);

/// <summary>
/// Zone 내 액션 메시지
/// </summary>
public record PlayerActionInZone(long PlayerId, string ZoneId, string Action, object? Data = null) 
    : PlayerZoneMessage(PlayerId, ZoneId);

/// <summary>
/// Zone 메시지 처리 결과
/// </summary>
public record ZoneMessageResult(
    bool Success,
    string? ErrorMessage = null,
    object? Data = null
);
