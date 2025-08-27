using Akka.Actor;
using ActorServer.Messages;
using ActorServer.Exceptions;

namespace ActorServer.Actors;

/// <summary>
/// Zone 관리를 담당하는 중앙 Actor
/// 모든 Zone 관련 통신을 중계하고 검증
/// </summary>
public class ZoneActor : ReceiveActor
{
    private readonly Dictionary<string, Zone> _zones = new();

    public ZoneActor()
    {
        InitializeZones();
        RegisterHandlers();
    }
    private void InitializeZones()
    {
        _zones["town"] = new Zone(new ZoneInfo
        {
            ZoneId = "town",
            Name = "Starting Town",
            SpawnPoint = new Position(0, 0),
            MaxPlayers = 100
        });

        _zones["forest"] = new Zone(new ZoneInfo
        {
            ZoneId = "forest",
            Name = "Beginner Forest",
            SpawnPoint = new Position(100, 100),
            MaxPlayers = 100
        });
        Console.WriteLine($"[ZoneManager] Initialized {_zones.Count} zones");
    }


    private void RegisterHandlers()
    {
        // Zone 변경
        Receive<ChangeZoneRequest>(HandleChangeZoneRequest);

        // Player -> Zone 메시지
        Receive<PlayerMoveInZone>(HandlePlayerMoveInZone);
        Receive<PlayerChatInZone>(HandlePlayerChatInZone);
        Receive<PlayerActionInZone>(HandlePlayerActionInZone);

        // Zone -> Player 메시지 중계
        Receive<PlayerPositionUpdate>(HandlePlayerPositionUpdate);
        Receive<ChatBroadcast>(HandleChatBroadcast);
    }


    private void HandleChangeZoneRequest(ChangeZoneRequest msg)
    {
        var playerActor = msg.PlayerActor;
        var playerId = msg.PlayerId;
        var targetZoneId = msg.TargetZoneId;

        try
        {
            // 1. 대상 Zone 존재 확인
            if (!_zones.ContainsKey(targetZoneId))
            {
                playerActor.Tell(new ChangeZoneResponse(false, "Zone not found"));
                Console.WriteLine($"[ZoneManager] Zone change failed - Zone not found: {targetZoneId}");
                return;
            }

            var (currentZone, currentZoneId) = FindPlayerZone(playerId);
            bool isFirstEntry = currentZone == null;
            if (isFirstEntry)
            {
                //
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ZoneManager] Zone change error for player {playerId}: {ex.Message}");
            playerActor.Tell(new ChangeZoneResponse(false, "Internal error"));
        }
    }

    private void HandlePlayerMoveInZone(PlayerMoveInZone msg)
    {
        // 1. Player가 해당 Zone에 있는지 확인
        if (!ValidatePlayerInZone(msg.PlayerId, msg.ZoneId))
        {
            Sender.Tell(new ZoneMessageResult(false, "Player not in specified zone"));
            Console.WriteLine($"[ZoneManager] Move rejected - Player {msg.PlayerId} not in zone {msg.ZoneId}");
            return;
        }

        // 2. 이동 유효성 검사 (치트 방지)
        if (!ValidateMovement(msg.PlayerId, msg.NewPosition))
        {
            Sender.Tell(new ZoneMessageResult(false, "Invalid movement detected"));
            Console.WriteLine($"[ZoneManager] Move rejected - Invalid movement for player {msg.PlayerId}");
            return;
        }

        // 3. Zone Actor 찾기
        if (!_zones.TryGetValue(msg.ZoneId, out var zoneActor))
        {
            Sender.Tell(new ZoneMessageResult(false, "Zone not found"));
            return;
        }

        // 4. Zone에 이동 전달
        if (_playerActors.TryGetValue(msg.PlayerId, out var playerActor))
        {
            zoneActor.Tell(new PlayerMovement(playerActor, msg.NewPosition));
            Sender.Tell(new ZoneMessageResult(true));
        }
    }

    private void HandlePlayerChatInZone(PlayerChatInZone msg)
    {
        // 1. Player가 해당 Zone에 있는지 확인
        if (!ValidatePlayerInZone(msg.PlayerId, msg.ZoneId))
        {
            Console.WriteLine($"[ZoneManager] Chat rejected - Player {msg.PlayerId} not in zone {msg.ZoneId}");
            return;
        }

        // 2. 채팅 필터링 (욕설, 스팸 등)
        if (!ValidateChatMessage(msg.Message))
        {
            Sender?.Tell(new ZoneMessageResult(false, "Message filtered"));
            return;
        }

        // 3. Zone에 채팅 전달
        if (_zones.TryGetValue(msg.ZoneId, out var zoneActor))
        {
            zoneActor.Tell(new ChatMessage(msg.PlayerId, msg.Message));

            Console.WriteLine($"[ZoneManager] Chat from player {msg.PlayerId} in {msg.ZoneId}: {msg.Message}");
        }
    }

    private void HandlePlayerActionInZone(PlayerActionInZone msg)
    {
        // Player 액션 처리 (스킬 사용, 아이템 사용 등)
        if (!ValidatePlayerInZone(msg.PlayerId, msg.ZoneId))
        {
            Sender.Tell(new ZoneMessageResult(false, "Player not in zone"));
            return;
        }

        // Zone에 액션 전달
        if (_zones.TryGetValue(msg.ZoneId, out var zoneActor))
        {
            // 액션에 따른 처리
            Console.WriteLine($"[ZoneManager] Player {msg.PlayerId} action '{msg.Action}' in {msg.ZoneId}");

            // TODO: 액션별 처리 로직
            Sender.Tell(new ZoneMessageResult(true, Data: msg.Data));
        }
    }

    // 변경: 플레이어 Actor 찾기 헬퍼 메서드 추가
    private IActorRef? GetPlayerActor(long playerId)
    {
        // Zone에서 플레이어 찾기
        if (_playerZoneMap.TryGetValue(playerId, out var zoneId))
        {
            if (_zones.TryGetValue(zoneId, out var zone))
            {
                var playerInfo = zone.GetPlayer(playerId);
                return playerInfo?.Actor;
            }
        }
        return null;
    }

    // 추가: 플레이어가 어느 Zone에 있는지 찾기
    private (Zone? zone, string? zoneId) FindPlayerZone(long playerId)
    {
        foreach (var kvp in _zones)
        {
            var player = kvp.Value.GetPlayer(playerId);
            if (player != null)
            {
                return (kvp.Value, kvp.Key);
            }
        }
        return (null, null);
    }
    
    // 추가: 플레이어 Actor 찾기
    private IActorRef? FindPlayerActor(long playerId)
    {
        var (zone, _) = FindPlayerZone(playerId);
        return zone?.GetPlayerActor(playerId);
    }


    #region Zone -> Player 메시지 중계

    private void HandlePlayerPositionUpdate(PlayerPositionUpdate msg)
    {
        // Zone에서 온 위치 업데이트를 관련 플레이어들에게 전달
        // (필요시 구현)
    }

    private void HandleChatBroadcast(ChatBroadcast msg)
    {
        // Zone에서 온 채팅 브로드캐스트를 관련 플레이어들에게 전달
        // (필요시 구현)
    }

    #endregion


    #region 검증 메서드
    private bool ValidatePlayerInZone(long playerId, string zoneId)
    {
        return _playerZoneMap.TryGetValue(playerId, out var currentZone)
               && currentZone == zoneId;
    }

    private bool ValidateMovement(long playerId, Position newPosition)
    {
        // 이동 속도 체크
        // TODO: 이전 위치와 비교하여 속도 계산

        // 맵 경계 체크
        const float MAP_BOUNDARY = 10000f;
        if (Math.Abs(newPosition.X) > MAP_BOUNDARY || Math.Abs(newPosition.Y) > MAP_BOUNDARY)
        {
            return false;
        }

        // 유효한 좌표인지 체크
        if (!newPosition.IsValid())
        {
            return false;
        }

        return true;
    }

    private bool ValidateChatMessage(string message)
    {
        // 빈 메시지 체크
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        // 메시지 길이 체크
        if (message.Length > 500)
        {
            return false;
        }

        // TODO: 욕설 필터링
        // TODO: 스팸 체크

        return true;
    }

    #endregion

    #region Actor 라이프사이클

    protected override void PreStart()
    {
        Console.WriteLine("[ZoneManager] Starting...");
        base.PreStart();
    }

    protected override SupervisorStrategy SupervisorStrategy()
    {
        return GameServerStrategies.ForZoneManager();
    }

    #endregion
}