using Akka.Actor;

namespace ActorServer.Messages;

public record EnterWorld(
    long PlayerId,
    IActorRef? ClientConnection = null  // 클라이언트 연결 Actor 참조
);
public record PlayerDisconnected(long PlayerId);
public record ClientDisconnected(long PlayerId);
