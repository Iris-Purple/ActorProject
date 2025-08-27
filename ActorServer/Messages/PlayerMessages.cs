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

/// <summary>
/// Zone 퇴장 알림
/// </summary>
public record ZoneExited(
    string ZoneId,
    string Reason = "normal"  // "normal", "kicked", "timeout" 등
);

/// <summary>
/// 검증된 채팅 전송 확인 (ZoneActor → PlayerActor)
/// </summary>
public record SendChat(
    string Message,
    DateTime Timestamp,
    bool IsSystem = false
);

/// <summary>
/// 채팅 거부됨
/// </summary>
public record ChatRejected(
    string Message,
    string Reason  // "spam", "filtered", "muted" 등
);

/// <summary>
/// 다른 플레이어 위치 업데이트 (ZoneActor → PlayerActor)
/// </summary>
// PlayerPositionUpdate는 기존 ZoneMessages.cs에 있음

/// <summary>
/// 다른 플레이어 상태 변경
/// </summary>
public record OtherPlayerStateChanged(
    long PlayerId,
    string State,  // "idle", "moving", "combat" 등
    object? StateData = null
);

/// <summary>
/// 클라이언트 연결 설정 (WorldActor → PlayerActor)
/// </summary>
// SetClientConnection은 기존 GameMessages.cs에 있음

/// <summary>
/// 클라이언트 연결 끊김
/// </summary>
public record ClientDisconnected(
    string Reason = "unknown"
);

/// <summary>
/// 클라이언트 재연결
/// </summary>
public record ClientReconnected(
    IActorRef ClientActor
);

/// <summary>
/// 상태 저장 명령 (WorldActor/ZoneActor → PlayerActor)
/// </summary>
public record SaveState(
    bool Immediate = false,  // true면 즉시 저장
    string? Reason = null    // "shutdown", "periodic", "manual" 등
);

/// <summary>
/// 상태 저장 완료 응답 (PlayerActor → 요청자)
/// </summary>
public record SaveStateComplete(
    long PlayerId,
    DateTime SavedAt = default
)
{
    public DateTime SavedAt { get; init; } = 
        SavedAt == default ? DateTime.UtcNow : SavedAt;
}

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

// ============================================
// PlayerActor 발신 메시지 (Outgoing)
// ============================================

/// <summary>
/// 클라이언트로 채팅 전송 (기존 ChatToClient 유지)
/// </summary>
// ChatToClient는 기존 GameMessages.cs에 있음

/// <summary>
/// 클라이언트로 시스템 알림
/// </summary>
public record SystemNotification(
    string Message,
    string Level = "info",  // "info", "warning", "error", "success"
    bool ShowPopup = false
);

/// <summary>
/// 클라이언트 UI 업데이트
/// </summary>
public record UpdateClientUI(
    string UIElement,  // "health", "position", "zone" 등
    object Data
);

/// <summary>
/// 위치 동기화 요청
/// </summary>
public record RequestPositionSync(
    long PlayerId,
    Position CurrentPosition
);

/// <summary>
/// 상태 변경 알림
/// </summary>
public record PlayerStateChanged(
    long PlayerId,
    string NewState,
    object? StateData = null
);

// ============================================
// 테스트용 메시지
// ============================================

/// <summary>
/// 테스트: Null 명령
/// </summary>
// TestNullCommand는 기존 TestMessage.cs에 있음

/// <summary>
/// 테스트: 크래시 시뮬레이션
/// </summary>
// SimulateCrash는 기존 TestMessage.cs에 있음

/// <summary>
/// 테스트: 플레이어 상태 덤프
/// </summary>
public record DumpPlayerState();

/// <summary>
/// 테스트: 강제 위치 설정
/// </summary>
public record ForceSetPosition(
    Position NewPosition,
    bool SkipValidation = true
);

// === 채팅 메시지 ===
public record ChatMessage(long PlayerId, string Message);
public record ChatBroadcast(long PlayerId, string Message, DateTime Timestamp);


public record PlayerInfo(IActorRef Actor, long PlayerId, Position Position);


public record SetClientConnection(IActorRef ClientActor);

public record ChatToClient(string From, string Message);