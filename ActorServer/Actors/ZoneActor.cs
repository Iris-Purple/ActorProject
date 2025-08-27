using Akka.Actor;
using ActorServer.Messages;
using ActorServer.Zone;
using ActorServer.Exceptions;

namespace ActorServer.Actors;

public class ZoneActor : ReceiveActor
{
    private readonly Dictionary<ZoneId, ZoneInfo> _zones = new();

    private const int DEFAULT_MAX_PLAYERS = 100;

    public ZoneActor()
    {
        InitializeZones();
        RegisterHandlers();
    }
    private void RegisterHandlers()
    {
        // Zone 변경
        Receive<ChangeZoneRequest>(HandleChangeZoneRequest);
    }

    private void InitializeZones()
    {
        _zones[ZoneId.Town] = new ZoneInfo(new ZoneData
        {
            ZoneId = ZoneId.Town,
            Name = "Starting Town",
            SpawnPoint = new Position(0, 0),
            MaxPlayers = DEFAULT_MAX_PLAYERS
        });


        _zones[ZoneId.Forest] = new ZoneInfo(new ZoneData
        {
            ZoneId = ZoneId.Forest,
            Name = "Dark Forest",
            SpawnPoint = new Position(100, 100),
            MaxPlayers = 50
        });

        Console.WriteLine($"[ZoneActor] Initialized {_zones.Count} zones");
        foreach (var zone in _zones)
        {
            Console.WriteLine($"  - {zone.Key}: {zone.Value.Name} (Max: {zone.Value.MaxPlayers})");
        }
    }

    private void HandleChangeZoneRequest(ChangeZoneRequest msg)
    {
        var playerId = msg.PlayerId;
        var playerActor = msg.PlayerActor;
        var targetZoneId = msg.TargetZoneId;

        Console.WriteLine($"[ZoneActor] Zone change request - Player:{playerId} → Zone:{targetZoneId}");
        try
        {
            // 1. 대상 Zone 존재 확인
            if (!_zones.TryGetValue(targetZoneId, out var targetZone))
            {
                Console.WriteLine($"[ZoneActor] Zone not found: {targetZoneId}");

                playerActor.Tell(new ErrorMessage(
                    Type: ERROR_MSG_TYPE.ZONE_CHANGE_ERROR,
                    Reason: $"ERROR: Zone not found ({targetZoneId})"));

                return;
            }

            // 2. Zone 수용 가능 여부 확인
            if (targetZone.IsFull)
            {
                Console.WriteLine($"[ZoneActor] Zone {targetZoneId} is full ({targetZone.PlayerCount}/{targetZone.MaxPlayers})");

                playerActor.Tell(new ErrorMessage(
                    Type: ERROR_MSG_TYPE.ZONE_CHANGE_ERROR,
                    Reason: $"ERROR: {targetZoneId} is Full"
                ));

                playerActor.Tell(new ZoneFull(targetZoneId));
                return;
            }

            // 3. 현재 Zone에서 제거 (있다면)
            ZoneId? previousZoneId = null;
            foreach (var kvp in _zones)
            {
                if (kvp.Value.HasPlayer(playerId))
                {
                    previousZoneId = kvp.Key;
                    kvp.Value.RemovePlayer(playerId);
                    Console.WriteLine($"[ZoneActor] Player {playerId} removed from {previousZoneId}");
                    break;
                }
            }

            // 4. 새 Zone에 추가
            var (success, message) = targetZone.TryAddPlayer(playerId, playerActor);

            if (!success)
            {
                Console.WriteLine($"[ZoneActor] Failed to add player to zone: {message}");

                // 실패시 원래 Zone으로 복귀
                if (previousZoneId != null && _zones.TryGetValue(previousZoneId.Value, out var previousZone))
                {
                    previousZone.TryAddPlayer(playerId, playerActor);
                    Console.WriteLine($"[ZoneActor] Player {playerId} restored to {previousZoneId}");
                }

                playerActor.Tell(new ErrorMessage(
                    Type: ERROR_MSG_TYPE.ZONE_CHANGE_ERROR,
                    Reason: $"TryAddPlayer ERROR: {message}"
                ));
                return;
            }

            // 5. 성공 - PlayerActor에 알림
            Console.WriteLine($"[ZoneActor] Player {playerId} successfully moved to {targetZoneId}");
            Console.WriteLine($"[ZoneActor] {targetZoneId} population: {targetZone.PlayerCount}/{targetZone.MaxPlayers}");

            // Zone 변경 알림
            playerActor.Tell(new ZoneChanged(
                NewZoneId: targetZoneId,
                SpawnPosition: targetZone.GetSpawnPoint(),
                PlayerCount: targetZone.PlayerCount
            ));

            // 로그
            if (previousZoneId != null)
            {
                Console.WriteLine($"[ZoneActor] Player {playerId}: {previousZoneId} → {targetZoneId}");
            }
            else
            {
                Console.WriteLine($"[ZoneActor] Player {playerId} initial spawn in {targetZoneId}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ZoneActor] ERROR in zone change: {ex.Message}");

            playerActor.Tell(new ErrorMessage(
                Type: ERROR_MSG_TYPE.ZONE_CHANGE_ERROR,
                Reason: $"Exception: {ex.Message}"
            ));
            // Supervision에 의해 재시작될 수 있도록 예외 전파
            throw new TemporaryGameException($"Zone change failed for player {playerId}", ex);
        }
    }

    protected override void PreStart()
    {
        Console.WriteLine("[ZoneActor] Starting...");
        Console.WriteLine("[ZoneActor] Zone change only mode - No validation, no broadcast");
        base.PreStart();
    }

    protected override void PostStop()
    {
        Console.WriteLine("[ZoneActor] Stopping...");

        // 종료시 Zone 상태 출력
        foreach (var kvp in _zones)
        {
            Console.WriteLine($"[ZoneActor] Zone {kvp.Key} had {kvp.Value.PlayerCount} players");
        }

        base.PostStop();
    }

    protected override void PreRestart(Exception reason, object message)
    {
        Console.WriteLine($"[ZoneActor] Restarting due to: {reason.Message}");
        base.PreRestart(reason, message);
    }

    protected override void PostRestart(Exception reason)
    {
        Console.WriteLine("[ZoneActor] Restart completed");
        base.PostRestart(reason);
    }
}
