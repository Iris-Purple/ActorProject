using Akka.Actor;

namespace ActorServer.Messages;

public record EnterWorld(long PlayerId);
public record PlayerDisconnected(long PlayerId);
public record ClientDisconnected(long PlayerId);
