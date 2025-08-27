using Akka.Actor;

namespace ActorServer.Messages;

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
