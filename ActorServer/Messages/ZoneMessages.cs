using Akka.Actor;
using ActorServer.Zone;

namespace ActorServer.Messages;


// ============================================
// Zone Manager 메시지
// ============================================

public record ChangeZoneRequest(
    IActorRef PlayerActor, 
    long PlayerId,
    ZoneId TargetZoneId
);

// ============================================
// Zone Actor 메시지
// ============================================


public record GetZoneStatus();

public record ZoneStatus
{
    public ZoneData ZoneInfo { get; set; } = null!;
    public int PlayerCount { get; set; }
    public List<long> Players { get; set; } = new();
}


public record GetPlayersInZone(string ZoneId);
public record PlayersInZoneResponse(string ZoneId, List<PlayerInfo> Players);
public record BroadcastToZone(object Message);
public record SystemMessage(string Message);
