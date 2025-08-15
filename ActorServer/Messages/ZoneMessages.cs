using Akka.Actor;

namespace ActorServer.Messages;


// === Zone 정보 ===
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

// === Zone Manager 메시지 ===
public record GetZoneInfo(string ZoneId);
public record ZoneInfoResponse(ZoneInfo Info);
public record ZoneNotFound(string ZoneId);
public record ChangeZoneRequest(IActorRef PlayerActor, string PlayerName, string TargetZoneId);
public record ChangeZoneResponse(bool Success, string Message);
public record GetAllZones();
public record AllZonesResponse(IEnumerable<ZoneInfo> Zones);

// === Zone Actor 메시지 ===
public record SetZone(IActorRef ZoneActor);
public record GetZoneStatus();
public record ZoneStatus
{
    public ZoneInfo ZoneInfo { get; set; } = null!;
    public int PlayerCount { get; set; }
    public List<string> Players { get; set; } = new();
}

// === Zone 진입/퇴장 ===
public record AddPlayerToZone(IActorRef PlayerActor, string PlayerName);
public record RemovePlayerFromZone(IActorRef PlayerActor);
public record CurrentPlayersInZone(IEnumerable<PlayerInfo> Players);
public record PlayerJoinedZone(PlayerInfo Player);
public record PlayerLeftZone(string PlayerName);

// === Zone 상태 알림 ===
public record ZoneEntered(ZoneInfo ZoneInfo);
public record ZoneFull(string ZoneId);
public record OutOfBoundWarning(string ZoneId);

// === Zone 헬스 체크 ===
public record CheckZoneHealth();
public record ZoneHealthStatus(string ZoneId, bool IsHealthy, int PlayerCount);