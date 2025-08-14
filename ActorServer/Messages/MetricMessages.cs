
namespace ActorServer.Messages;

// 메트릭 관련 메시지들
public record PlayerLoggedIn();
public record PlayerLoggedOut(string? ZoneId = null);
public record MessageProcessed();
public record GetMetrics();
