using Akka.Actor;

namespace ActorServer.Messages;


// Zone Manager 메시지
public record GetZoneInfo(string ZoneId);
public record ZoneInfoResponse(ZoneInfo Info);
public record ZoneNotFound(string ZoneId);
public record ChangeZoneRequest(IActorRef PlayerActor, string PlayerName, string TargetZoneId);
public record ChangeZoneResponse(bool Success, string Message);
public record GetAllZones();
public record AllZonesResponse(IEnumerable<ZoneInfo> Zones);

// Zone Actor 메시지
public record GetZoneStatus();
public class ZoneInfo
{
    public string ZoneId { get; set; } = "";
    public string Name { get; set; } = "";
    public ZoneType Type { get; set; }
    public int MinLevel { get; set; }
    public int MaxLevel { get; set; }
    public Position SpawnPoint { get; set; } = new(0, 0);
    public int MaxPlayers { get; set; } = 100;
}

public enum ZoneType
{
    SafeZone,
    Field,
    Dungeon,
    PvpZone,
    Raid
}
public record ZoneStatus
{
    public ZoneInfo ZoneInfo { get; set; } = null!;
    public int PlayerCount { get; set; }
    public List<string> Players { get; set; } = new();
}

public record ZoneEntered(ZoneInfo ZoneInfo);
public record ZoneFull(string ZoneId);
public record OutOfBoundWarning(string ZoneId);