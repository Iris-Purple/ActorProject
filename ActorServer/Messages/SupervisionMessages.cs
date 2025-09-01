namespace ActorServer.Messages;

// 테스트 전용 메시지들 (프로덕션에서는 사용하지 않음)
#if DEBUG

/// <summary>
/// PlayerActor에 예외를 발생시키는 테스트 메시지
/// </summary>
public record CausePlayerException(string Reason);

/// <summary>
/// ZoneActor에 예외를 발생시키는 테스트 메시지
/// </summary>
public record CauseZoneException(string Reason);

/// <summary>
/// Actor 상태 확인용 테스트 메시지
/// </summary>
public record HealthCheck();

/// <summary>
/// Actor 재시작 확인용 응답
/// </summary>
public record HealthCheckResponse(string ActorName, bool IsHealthy);

#endif