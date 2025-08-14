using Akka.Actor;

namespace ActorServer.Messages;

public record SetZone(IActorRef ZoneActor);
// 위치 정보
public record Position(float X, float Y);
// 플레이어 정보
public record PlayerInfo(IActorRef Actor, string Name, Position Position);
// 로그인 요청
public record PlayerLoginRequest(string PlayerName);
public record PlayerDisconnect(string PlayerName);
public record PlayerPositionUpdate(string PlayerName, Position NewPosition);
public record PlayerJoinedZone(PlayerInfo Player);
// 이동 명령
public record MoveCommand(Position NewPosition);
// WorldActor 가 플레이어 명령을 라우팅 할 수 있음
public record PlayerCommand(string PlayerName, object Command);

// Zone 관련 메시지
public record AddPlayerToZone(IActorRef PlayerActor, string PlayerName);
public record RemovePlayerFromZone(IActorRef PlayerActor);
public record PlayerMovement(IActorRef PlayerActor, Position NewPosition);

// 클라이언트에게 보낼 메시지
public record CurrentPlayersInZone(IEnumerable<PlayerInfo> Players);
public record PlayerLeftZone(string PlayerName);

// 채팅 메시지 (플레이어가 보내는 것)
public record ChatMessage(string PlayerName, string Message);
public record ChatBroadcast(string PlayerName, string Message, DateTime Timestamp);
