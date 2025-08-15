using Akka.Actor;

namespace ActorServer.Messages;

// === 기본 메시지 ===
public record Position(float X, float Y);
public record PlayerInfo(IActorRef Actor, string Name, Position Position);

// === 로그인/연결 관련 ===
public record PlayerLoginRequest(string PlayerName);
public record PlayerDisconnect(string PlayerName);
public record RegisterClientConnection(string PlayerName, IActorRef ClientActor);
public record SetClientConnection(IActorRef ClientActor);

// === 이동 관련 ===
public record MoveCommand(Position NewPosition);
public record PlayerMovement(IActorRef PlayerActor, Position NewPosition);
public record PlayerPositionUpdate(string PlayerName, Position NewPosition);

// === 플레이어 명령 라우팅 ===
public record PlayerCommand(string PlayerName, object? Command);

// === Zone 이동 요청 (WorldActor → ZoneManager) ===
public record RequestZoneChange(string PlayerName, string TargetZoneId);

// === 클라이언트 통신 ===
public record ChatToClient(string From, string Message);
public record LoginFailed(string PlayerName, string Reason);
public record CommandFailed(string PlayerName, string Command, string Reason);