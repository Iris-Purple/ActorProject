using Akka.Actor;

namespace ActorServer.Messages
{
    // Actor 복구 관련 메시지
    public record PlayerReconnecting(string PlayerName);
    public record PlayerReconnected(string PlayerName, Position Position);
    public record PlayerRecovered(string PlayerName, string ZoneId, Position Position);
    
    // Zone 상태 관련 메시지
    public record CheckZoneHealth();
    public record ZoneHealthStatus(string ZoneId, bool IsHealthy, int PlayerCount);
    
    // 테스트용 메시지
    public record CrashAndRecover(string Reason);
    public record SimulateCrash(string Reason);
    public record SimulateOutOfMemory();
    public record TestSupervision(string PlayerName, string TestType);
    
    // 에러 응답 메시지
    public record LoginFailed(string PlayerName, string Reason);
    public record CommandFailed(string PlayerName, string Command, string Reason);
    
    // 클라이언트 연결 관리
    public record RegisterClientConnection(string PlayerName, IActorRef ClientActor);
    public record UnregisterClientConnection(string PlayerName);
}