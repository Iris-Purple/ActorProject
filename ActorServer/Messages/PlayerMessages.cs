using Akka.Actor;
using ActorServer.Messages;
using ActorServer.Zone;
using ActorServer.Network.Protocol;

namespace ActorServer.Messages;

// ============================================
// PlayerActor 수신 메시지 (Incoming)
// ============================================

/// <summary>
/// Zone 변경 완료 알림 (ZoneActor → PlayerActor)
/// </summary>
public record ZoneChanged(
    ZoneId NewZoneId,
    Position SpawnPosition
);

public record ZoneFull(ZoneId ZoneId);

public record ChatMessage(string Message);

public record PlayerInfo(long PlayerId, Position Position);

public record SetClientConnection(IActorRef ClientActor);
public record SendPacketToClient<T>(T Packet) where T : Packet;
