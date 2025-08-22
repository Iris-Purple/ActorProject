using Akka.Actor;
using ActorServer.Messages;

namespace ActorServer.Actors;

/// <summary>
/// Zone 관리를 담당하는 Actor
/// 플레이어의 Zone 간 이동을 처리
/// </summary>
public class ZoneManager : ReceiveActor
{
    private readonly Dictionary<string, IActorRef> _zones = new();
    private readonly Dictionary<long, string> _playerZoneMap = new(); // playerId -> zoneId
    private readonly Dictionary<string, ZoneInfo> _zoneInfos = new();

    public ZoneManager()
    {
        // 기본 Zone들 생성
        InitializeZones();

        Receive<GetZoneInfo>(HandleGetZoneInfo);
        Receive<ChangeZoneRequest>(HandleChangeZoneRequest);
        Receive<GetAllZones>(HandleGetAllZones);
    }

    private void InitializeZones()
    {
        // 시작 마을
        CreateZone("town", new ZoneInfo
        {
            ZoneId = "town",
            Name = "Starting Town",
            Type = ZoneType.SafeZone,
            MinLevel = 1,
            MaxLevel = 10,
            SpawnPoint = new Position(0, 0)
        });

        // 초보자 사냥터
        CreateZone("forest", new ZoneInfo
        {
            ZoneId = "forest",
            Name = "Beginner Forest",
            Type = ZoneType.Field,
            MinLevel = 1,
            MaxLevel = 15,
            SpawnPoint = new Position(100, 100)
        });

        // 던전
        CreateZone("dungeon-1", new ZoneInfo
        {
            ZoneId = "dungeon-1",
            Name = "Dark Cave",
            Type = ZoneType.Dungeon,
            MinLevel = 10,
            MaxLevel = 20,
            SpawnPoint = new Position(200, 200),
            MaxPlayers = 5 // 던전은 최대 5명
        });

        Console.WriteLine($"[ZoneManager] Initialized {_zones.Count} zones");
    }

    private void CreateZone(string zoneId, ZoneInfo info)
    {
        // ZoneActor 생성 (이제 ZoneActor가 ZoneInfo를 받음)
        var zoneActor = Context.ActorOf(
            Props.Create(() => new ZoneActor(info)),
            $"zone-{zoneId}"
        );

        _zones[zoneId] = zoneActor;
        _zoneInfos[zoneId] = info;
    }

    private void HandleGetZoneInfo(GetZoneInfo msg)
    {
        if (_zoneInfos.TryGetValue(msg.ZoneId, out var info))
        {
            Sender.Tell(new ZoneInfoResponse(info));
        }
        else
        {
            Sender.Tell(new ZoneNotFound(msg.ZoneId));
        }
    }

    private void HandleChangeZoneRequest(ChangeZoneRequest msg)
    {
        var playerActor = msg.PlayerActor;
        var playerId = msg.PlayerId;
        var targetZoneId = msg.TargetZoneId;

        // 대상 Zone이 존재하는지 확인
        if (!_zones.ContainsKey(targetZoneId))
        {
            playerActor.Tell(new ChangeZoneResponse(false, "Zone not found"));
            return;
        }

        // 현재 Zone에서 제거
        if (_playerZoneMap.TryGetValue(playerId, out var currentZoneId))
        {
            if (_zones.TryGetValue(currentZoneId, out var currentZone))
            {
                currentZone.Tell(new RemovePlayerFromZone(playerActor, playerId));
            }
        }

        // 새 Zone에 추가
        var newZone = _zones[targetZoneId];
        newZone.Tell(new AddPlayerToZone(playerActor, playerId));

        // 플레이어-Zone 매핑 업데이트
        _playerZoneMap[playerId] = targetZoneId;
        // 성공 응답
        playerActor.Tell(new ChangeZoneResponse(true, targetZoneId));

        Console.WriteLine($"[ZoneManager] Player {playerId} moved from {currentZoneId ?? "nowhere"} to {targetZoneId}");
    }

    private void HandleGetAllZones(GetAllZones msg)
    {
        var zoneList = _zoneInfos.Values;
        Sender.Tell(new AllZonesResponse(zoneList));
    }
}
