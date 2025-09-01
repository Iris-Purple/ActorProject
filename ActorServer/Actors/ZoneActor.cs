using Akka.Actor;
using ActorServer.Messages;
using ActorServer.Zone;
using ActorServer.Exceptions;
using Common.Database;

namespace ActorServer.Actors;

public class ZoneActor : ReceiveActor
{
    private readonly Dictionary<ZoneId, ZoneInfo> _zones = new();
    private readonly PlayerDatabase _playerDb = PlayerDatabase.Instance;
    private const int DEFAULT_MAX_PLAYERS = 100;

    public ZoneActor()
    {
        InitializeZones();
        RegisterHandlers();
    }
    private void RegisterHandlers()
    {
        Receive<ChangeZoneRequest>(HandleChangeZoneRequest);
        Receive<PlayerMove>(HandlePlayerMove);
        Receive<PlayerDisconnected>(HandlePlayerDisconnected);
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
        var (success, message) = targetZone.TryAddPlayer(playerId);

        if (!success)
        {
            Console.WriteLine($"[ZoneActor] Failed to add player to zone: {message}");

            // 실패시 원래 Zone으로 복귀
            if (previousZoneId != null && _zones.TryGetValue(previousZoneId.Value, out var previousZone))
            {
                previousZone.TryAddPlayer(playerId);
                Console.WriteLine($"[ZoneActor] Player {playerId} restored to {previousZoneId}");
            }

            SavePlayerToDb(playerId, targetZone.GetSpawnPoint(), targetZoneId);

            playerActor.Tell(new ErrorMessage(
                Type: ERROR_MSG_TYPE.ZONE_CHANGE_ERROR,
                Reason: $"TryAddPlayer ERROR: {message}"
            ));
            return;
        }

        // 5. 성공 - PlayerActor에 알림
        Console.WriteLine($"[ZoneActor] Player {playerId} successfully moved to {targetZoneId}");
        Console.WriteLine($"[ZoneActor] {targetZoneId} population: {targetZone.PlayerCount}/{targetZone.MaxPlayers}");

        // zone position memory update
        targetZone.UpdatePlayerPosition(playerId, targetZone.GetSpawnPoint());

        // Zone 변경 알림
        playerActor.Tell(new ZoneChanged(
            NewZoneId: targetZoneId,
            SpawnPosition: targetZone.GetSpawnPoint()
        ));

        // 로그
        if (previousZoneId != null)
            Console.WriteLine($"[ZoneActor] Player {playerId}: {previousZoneId} → {targetZoneId}");
        else
            Console.WriteLine($"[ZoneActor] Player {playerId} initial spawn in {targetZoneId}");
    }
    private void HandlePlayerMove(PlayerMove msg)
    {
        var playerActor = msg.PlayerActor;
        var playerId = msg.PlayerId;
        var newPosition = new Position(msg.X, msg.Y);

        Console.WriteLine($"[ZoneActor] Move request - Player:{playerId} to ({newPosition})");
        ZoneId? currentZoneId = null;
        ZoneInfo? currentZone = null;

        foreach (var kvp in _zones)
        {
            if (kvp.Value.HasPlayer(playerId))
            {
                currentZoneId = kvp.Key;
                currentZone = kvp.Value;
                break;
            }
        }
        if (currentZoneId == null || currentZone == null)
        {
            Console.WriteLine($"[ZoneActor] Player {playerId} not found in any zone");
            playerActor.Tell(new ErrorMessage(
                Type: ERROR_MSG_TYPE.PLAYER_MOVE_ERROR,
                Reason: $"ERROR: Not found Zone ({playerId})"));
            return;
        }

        currentZone.UpdatePlayerPosition(playerId, newPosition);
        playerActor.Tell(new PlayerMoved(
            PlayerId: playerId,
            X: newPosition.X,
            Y: newPosition.Y));

        SavePlayerToDb(playerId, newPosition, currentZoneId.Value);
    }
    private void HandlePlayerDisconnected(PlayerDisconnected msg)
    {
        var playerId = msg.PlayerId;
        Console.WriteLine($"[ZoneActor] Processing disconnection for Player {playerId}");

        // 모든 Zone에서 해당 플레이어 찾기
        ZoneId? foundZoneId = null;
        ZoneInfo? foundZone = null;
        foreach (var kvp in _zones)
        {
            if (kvp.Value.HasPlayer(playerId))
            {
                foundZoneId = kvp.Key;
                foundZone = kvp.Value;
                break;
            }
        }
        if (foundZoneId == null || foundZone == null)
        {
            Console.WriteLine($"[ZoneActor] Player {playerId} not found in any zone");
            return;
        }

        // Zone에서 플레이어 제거
        if (foundZone.RemovePlayer(playerId))
        {
            Console.WriteLine($"[ZoneActor] Player {playerId} removed from {foundZoneId}");
            Console.WriteLine($"[ZoneActor] {foundZoneId} population: {foundZone.PlayerCount}/{foundZone.MaxPlayers}");
            // 추가: 다른 플레이어들에게 알림 (향후 구현)
            // BroadcastToZone(foundZoneId, new PlayerLeftZone(playerId));
        }
        else
        {
            Console.WriteLine($"[ZoneActor] Failed to remove Player {playerId} from {foundZoneId}");
        }
    }


    private void SavePlayerToDb(long playerId, Position position, ZoneId zoneId)
    {
        try
        {
            _playerDb.SavePlayer(playerId, position.X, position.Y, (int)zoneId);
            Console.WriteLine($"[ZoneActor] Saved to DB - Player:{playerId} Zone:{zoneId} Pos:({position.X:F1},{position.Y:F1})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ZoneActor] DB save failed for player {playerId}: {ex.Message}");
            // DB 저장 실패해도 게임은 계속 진행
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
