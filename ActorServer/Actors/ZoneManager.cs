using Akka.Actor;
using ActorServer.Messages;
using ActorServer.Exceptions;

namespace ActorServer.Actors;

/// <summary>
/// Zone 관리를 담당하는 중앙 Actor
/// 모든 Zone 관련 통신을 중계하고 검증
/// </summary>
public class ZoneManager : ReceiveActor
{
    // Zone 관리
    private readonly Dictionary<string, IActorRef> _zones = new();
    private readonly Dictionary<string, ZoneInfo> _zoneInfos = new();
    
    // Player 관리
    private readonly Dictionary<long, string> _playerZoneMap = new();  // playerId -> zoneId
    private readonly Dictionary<long, IActorRef> _playerActors = new();  // playerId -> playerActor
    
    
    // Zone 설정
    private const int DEFAULT_MAX_PLAYERS = 100;
    private const int DUNGEON_MAX_PLAYERS = 5;

    public ZoneManager()
    {
        // Zone 초기화
        InitializeZones();
        
        // 메시지 핸들러 등록
        RegisterHandlers();
        
        Console.WriteLine($"[ZoneManager] Started with {_zones.Count} zones");
    }

    private void RegisterHandlers()
    {
        // Player 등록/해제
        Receive<RegisterPlayer>(HandleRegisterPlayer);
        Receive<UnregisterPlayer>(HandleUnregisterPlayer);
        
        // Zone 변경
        Receive<ChangeZoneRequest>(HandleChangeZoneRequest);
        
        // Player -> Zone 메시지 (ZoneManager 경유)
        Receive<PlayerMoveInZone>(HandlePlayerMoveInZone);
        Receive<PlayerChatInZone>(HandlePlayerChatInZone);
        Receive<PlayerActionInZone>(HandlePlayerActionInZone);
        
        // Zone 정보 조회
        Receive<GetZoneInfo>(HandleGetZoneInfo);
        Receive<GetAllZones>(HandleGetAllZones);
        
        // Zone -> Player 메시지 중계
        Receive<PlayerPositionUpdate>(HandlePlayerPositionUpdate);
        Receive<ChatBroadcast>(HandleChatBroadcast);
        
        // 헬스 체크
        Receive<CheckZoneHealth>(HandleCheckZoneHealth);
    }

    #region Zone 초기화

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
            SpawnPoint = new Position(0, 0),
            MaxPlayers = DEFAULT_MAX_PLAYERS
        });

        // 초보자 사냥터
        CreateZone("forest", new ZoneInfo
        {
            ZoneId = "forest",
            Name = "Beginner Forest",
            Type = ZoneType.Field,
            MinLevel = 1,
            MaxLevel = 15,
            SpawnPoint = new Position(100, 100),
            MaxPlayers = DEFAULT_MAX_PLAYERS
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
            MaxPlayers = DUNGEON_MAX_PLAYERS
        });
    }

    private void CreateZone(string zoneId, ZoneInfo info)
    {
        try
        {
            var zoneActor = Context.ActorOf(
                Props.Create(() => new ZoneActor(info)),
                $"zone-{zoneId}"
            );

            _zones[zoneId] = zoneActor;
            _zoneInfos[zoneId] = info;
            
            Console.WriteLine($"[ZoneManager] Created zone: {zoneId} ({info.Name})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ZoneManager] Failed to create zone {zoneId}: {ex.Message}");
            throw new ZoneException(zoneId, $"Failed to create zone: {ex.Message}", ex);
        }
    }

    #endregion

    #region Player 관리

    private void HandleRegisterPlayer(RegisterPlayer msg)
    {
        try
        {
            var playerId = msg.PlayerId;
            var playerActor = msg.PlayerActor;
            
            // 이미 등록된 경우 처리
            if (_playerActors.ContainsKey(playerId))
            {
                Console.WriteLine($"[ZoneManager] Player {playerId} already registered, updating...");
                _playerActors[playerId] = playerActor;
            }
            else
            {
                _playerActors[playerId] = playerActor;
                Console.WriteLine($"[ZoneManager] Registered new player {playerId}");
            }
            
            // PlayerActor에 ZoneManager 참조 전달
            playerActor.Tell(new SetZoneManager(Self));
            
            // 초기 Zone 설정 (있는 경우)
            if (!string.IsNullOrEmpty(msg.InitialZone))
            {
                Self.Tell(new ChangeZoneRequest(playerActor, playerId, msg.InitialZone));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ZoneManager] Failed to register player {msg.PlayerId}: {ex.Message}");
            throw new PlayerDataException(msg.PlayerId.ToString(), "Failed to register player", ex);
        }
    }

    private void HandleUnregisterPlayer(UnregisterPlayer msg)
    {
        var playerId = msg.PlayerId;
        
        // 현재 Zone에서 제거
        if (_playerZoneMap.TryGetValue(playerId, out var currentZoneId))
        {
            if (_zones.TryGetValue(currentZoneId, out var currentZone))
            {
                currentZone.Tell(new RemovePlayerFromZone(_playerActors[playerId], playerId));
            }
            _playerZoneMap.Remove(playerId);
        }
        
        // Player Actor 제거
        _playerActors.Remove(playerId);
        
        Console.WriteLine($"[ZoneManager] Unregistered player {playerId}");
    }

    #endregion

    #region Zone 변경

    private void HandleChangeZoneRequest(ChangeZoneRequest msg)
    {
        var playerActor = msg.PlayerActor;
        var playerId = msg.PlayerId;
        var targetZoneId = msg.TargetZoneId;
        
        try
        {
            // 대상 Zone 존재 확인
            if (!_zones.ContainsKey(targetZoneId))
            {
                playerActor.Tell(new ChangeZoneResponse(false, "Zone not found"));
                Console.WriteLine($"[ZoneManager] Zone change failed - Zone not found: {targetZoneId}");
                return;
            }
            
            // 현재 Zone에서 제거
            if (_playerZoneMap.TryGetValue(playerId, out var currentZoneId))
            {
                if (_zones.TryGetValue(currentZoneId, out var currentZone))
                {
                    currentZone.Tell(new RemovePlayerFromZone(playerActor, playerId));
                    Console.WriteLine($"[ZoneManager] Removing player {playerId} from {currentZoneId}");
                }
            }
            
            // 새 Zone에 추가
            var newZone = _zones[targetZoneId];
            newZone.Tell(new AddPlayerToZone(playerActor, playerId));
            
            // 매핑 업데이트
            _playerZoneMap[playerId] = targetZoneId;
            
            // 성공 응답
            playerActor.Tell(new ChangeZoneResponse(true, targetZoneId));
            
            Console.WriteLine($"[ZoneManager] Player {playerId} moved from {currentZoneId ?? "nowhere"} to {targetZoneId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ZoneManager] Zone change error: {ex.Message}");
            playerActor.Tell(new ChangeZoneResponse(false, "Internal error"));
            throw new ZoneException(targetZoneId, "Failed to change zone", ex);
        }
    }

    #endregion

    #region Player -> Zone 메시지 중계

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

    #endregion

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

    #region Zone 정보 조회

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

    private void HandleGetAllZones(GetAllZones msg)
    {
        var zoneList = _zoneInfos.Values;
        Sender.Tell(new AllZonesResponse(zoneList));
    }

    #endregion

    #region 헬스 체크

    private void HandleCheckZoneHealth(CheckZoneHealth msg)
    {
        foreach (var (zoneId, zoneActor) in _zones)
        {
            zoneActor.Tell(new GetZoneStatus());
        }
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