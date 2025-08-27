using ActorServer.Zone;
using Akka.Actor;

namespace ActorServer.Messages;

// === Actor 복구 관련 ===
public record PlayerReconnecting(long PlayerId);
public record PlayerReconnected(long PlayerId, Position Position);
public record PlayerRecovered(long PlayerId, string ZoneId, Position Position);
