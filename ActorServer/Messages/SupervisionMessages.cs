using Akka.Actor;

namespace ActorServer.Messages;

// === Actor 복구 관련 ===
public record PlayerReconnecting(string PlayerName);
public record PlayerReconnected(string PlayerName, Position Position);
public record PlayerRecovered(string PlayerName, string ZoneId, Position Position);
