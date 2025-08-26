using Akka.Actor;
using ActorServer.Messages;
using ActorServer.Exceptions;

namespace ActorServer.Actors;

/// <summary>
/// 개별 Zone을 관리하는 Actor
/// Zone 내 플레이어 관리, 메시지 브로드캐스트 등
/// </summary>
public class ZoneActor : ReceiveActor
{
    private readonly ZoneInfo _zoneInfo;
    
    // Zone 내 플레이어 관리
    private readonly Dictionary<IActorRef, PlayerInfo> _playersInZone = new();
    private readonly Dictionary<long, IActorRef> _playerIdToActor = new();  // PlayerId -> Actor 매핑
    
    private readonly DateTime _createdTime = DateTime.Now;
    
    // Zone 설정
    private const float ZONE_BOUNDARY = 500f;  // Zone 중심에서의 경계 거리
    private const int BROADCAST_BATCH_SIZE = 50;  // 브로드캐스트 배치 크기

    public ZoneActor(ZoneInfo zoneInfo)
    {
        _zoneInfo = zoneInfo;
        
        RegisterHandlers();
        
        Console.WriteLine($"[Zone-{_zoneInfo.ZoneId}] Created: {_zoneInfo.Name}");
    }

    private void RegisterHandlers()
    {
        // 플레이어 진입/퇴장
        Receive<AddPlayerToZone>(HandleAddPlayer);
        Receive<RemovePlayerFromZone>(HandleRemovePlayer);
        
        // 플레이어 액션
        Receive<PlayerMovement>(HandlePlayerMovement);
        Receive<ChatMessage>(HandleChatMessage);
        
        // Zone 정보 조회
        Receive<GetZoneStatus>(HandleGetZoneStatus);
        Receive<GetPlayersInZone>(HandleGetPlayersInZone);
        
        // 헬스 체크
        Receive<CheckZoneHealth>(HandleCheckZoneHealth);
        
        // 브로드캐스트
        Receive<BroadcastToZone>(HandleBroadcast);
    }

    #region 플레이어 진입/퇴장

    private void HandleAddPlayer(AddPlayerToZone msg)
    {
        Console.WriteLine("****** add player zone: {0}", msg);
        try
        {
            var playerActor = msg.PlayerActor;
            var playerId = msg.PlayerId;
            
            // 이미 Zone에 있는 경우
            if (_playerIdToActor.ContainsKey(playerId))
            {
                Console.WriteLine($"[Zone-{_zoneInfo.ZoneId}] Player {playerId} already in zone");
                return;
            }
            
            // Zone 최대 인원 체크
            if (_zoneInfo.MaxPlayers > 0 && _playersInZone.Count >= _zoneInfo.MaxPlayers)
            {
                playerActor.Tell(new ZoneFull(_zoneInfo.ZoneId));
                Console.WriteLine($"[Zone-{_zoneInfo.ZoneId}] Zone full! ({_playersInZone.Count}/{_zoneInfo.MaxPlayers})");
                return;
            }

            // 플레이어 정보 생성
            var playerInfo = new PlayerInfo(
                playerActor,
                playerId,
                _zoneInfo.SpawnPoint  // 스폰 포인트에서 시작
            );
            
            // 플레이어 추가
            _playersInZone[playerActor] = playerInfo;
            _playerIdToActor[playerId] = playerActor;
            
            // 플레이어에게 Zone 진입 알림
            playerActor.Tell(new ZoneEntered(_zoneInfo));
            
            // 다른 플레이어들에게 새 플레이어 입장 알림
            BroadcastToOthers(playerActor, new PlayerJoinedZone(playerInfo));
            
            Console.WriteLine($"[Zone-{_zoneInfo.ZoneId}] Player {playerId} entered. " +
                            $"({_playersInZone.Count}/{_zoneInfo.MaxPlayers} players)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Zone-{_zoneInfo.ZoneId}] Error adding player: {ex.Message}");
            throw new ZoneException(_zoneInfo.ZoneId, $"Failed to add player: {ex.Message}", ex);
        }
    }

    private void HandleRemovePlayer(RemovePlayerFromZone msg)
    {
        try
        {
            var playerActor = msg.PlayerActor;
            var playerId = msg.PlayerId;
            
            if (_playersInZone.TryGetValue(playerActor, out var playerInfo))
            {
                // 플레이어 제거
                _playersInZone.Remove(playerActor);
                _playerIdToActor.Remove(playerId);
                
                // 다른 플레이어들에게 퇴장 알림
                BroadcastToOthers(playerActor, new PlayerLeftZone(playerId));
                
                Console.WriteLine($"[Zone-{_zoneInfo.ZoneId}] Player {playerId} left. " +
                                $"({_playersInZone.Count}/{_zoneInfo.MaxPlayers} players)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Zone-{_zoneInfo.ZoneId}] Error removing player: {ex.Message}");
        }
    }
    #endregion

    #region 플레이어 액션 처리

    private void HandlePlayerMovement(PlayerMovement msg)
    {
        try
        {
            var playerActor = msg.PlayerActor;
            var newPosition = msg.NewPosition;

            Console.WriteLine("********** 1: {0}", msg.PlayerActor);
            if (_playersInZone.TryGetValue(playerActor, out var playerInfo))
            {
                // 위치 업데이트
                var updatedInfo = playerInfo with { Position = newPosition };
                _playersInZone[playerActor] = updatedInfo;
                
                // Zone 경계 체크
                if (!IsWithinZoneBoundary(newPosition))
                {
                    playerActor.Tell(new OutOfBoundWarning(_zoneInfo.ZoneId));
                    Console.WriteLine($"[Zone-{_zoneInfo.ZoneId}] Player {updatedInfo.PlayerId} out of bounds!");
                }

                // 근처 플레이어들에게만 위치 업데이트 브로드캐스트 (최적화)
                BroadcastToNearbyPlayers(
                    playerActor,
                    new PlayerPositionUpdate(updatedInfo.PlayerId, newPosition),
                    newPosition,
                    range: 200f  // 200 단위 내 플레이어에게만
                );
            }
            else
            {
                Console.WriteLine($"[Zone-{_zoneInfo.ZoneId}] Movement from unknown player");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Zone-{_zoneInfo.ZoneId}] Error handling movement: {ex.Message}");
            throw new TemporaryGameException($"Failed to process movement: {ex.Message}", ex);
        }
    }

    private void HandleChatMessage(ChatMessage msg)
    {
        try
        {
            var playerId = msg.PlayerId;
            var message = msg.Message;
            
            Console.WriteLine($"[Zone-{_zoneInfo.ZoneId}] Chat from {playerId}: {message}");
            
            // 채팅 브로드캐스트 생성
            var broadcast = new ChatBroadcast(playerId, message, DateTime.Now);
            
            // Zone 타입에 따른 처리
            switch (_zoneInfo.Type)
            {
                case ZoneType.SafeZone:
                    // 안전 지역: 모든 플레이어에게 전송
                    BroadcastToAll(broadcast);
                    break;
                    
                case ZoneType.Field:
                case ZoneType.Dungeon:
                    // 필드/던전: 범위 내 플레이어에게만 전송
                    if (_playerIdToActor.TryGetValue(playerId, out var senderActor) &&
                        _playersInZone.TryGetValue(senderActor, out var senderInfo))
                    {
                        BroadcastToNearbyPlayers(
                            senderActor,
                            broadcast,
                            senderInfo.Position,
                            range: 150f  // 채팅 범위
                        );
                    }
                    break;
                    
                case ZoneType.PvpZone:
                    // PvP 지역: 팀 채팅 등 특별 처리
                    // TODO: 팀 시스템 구현 시 처리
                    BroadcastToAll(broadcast);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Zone-{_zoneInfo.ZoneId}] Error handling chat: {ex.Message}");
        }
    }

    #endregion

    #region Zone 정보 조회

    private void HandleGetZoneStatus(GetZoneStatus msg)
    {
        var playerIds = _playersInZone.Values
            .Select(p => p.PlayerId)
            .ToList();
        
        var status = new ZoneStatus
        {
            ZoneInfo = _zoneInfo,
            PlayerCount = _playersInZone.Count,
            Players = playerIds
        };
        
        Sender.Tell(status);
        
        Console.WriteLine($"[Zone-{_zoneInfo.ZoneId}] Status requested - {_playersInZone.Count} players");
    }

    private void HandleGetPlayersInZone(GetPlayersInZone msg)
    {
        var players = _playersInZone.Values.ToList();
        Sender.Tell(new PlayersInZoneResponse(_zoneInfo.ZoneId, players));
    }

    #endregion

    #region 헬스 체크

    private void HandleCheckZoneHealth(CheckZoneHealth msg)
    {
        var isHealthy = CheckZoneHealth();
        
        var healthStatus = new ZoneHealthStatus(
            _zoneInfo.ZoneId,
            isHealthy,
            _playersInZone.Count
        );
        
        Sender.Tell(healthStatus);
        
        if (!isHealthy)
        {
            Console.WriteLine($"[Zone-{_zoneInfo.ZoneId}] Health check failed!");
        }
    }

    private bool CheckZoneHealth()
    {
        // Zone 상태 체크
        if (_playersInZone.Count > _zoneInfo.MaxPlayers)
        {
            return false;  // 과부하
        }
        
        // 메모리 사용량 체크 등
        // TODO: 추가 헬스 체크 로직
        
        return true;
    }

    #endregion

    #region 브로드캐스트

    private void HandleBroadcast(BroadcastToZone msg)
    {
        BroadcastToAll(msg.Message);
    }

    private void BroadcastToAll(object message)
    {
        // 배치 처리로 성능 최적화
        var players = _playersInZone.Keys.ToList();
        
        for (int i = 0; i < players.Count; i += BROADCAST_BATCH_SIZE)
        {
            var batch = players.Skip(i).Take(BROADCAST_BATCH_SIZE);
            foreach (var player in batch)
            {
                player.Tell(message);
            }
        }
    }

    private void BroadcastToOthers(IActorRef sender, object message)
    {
        foreach (var player in _playersInZone.Keys)
        {
            if (player != sender)
            {
                player.Tell(message);
            }
        }
    }

    private void BroadcastToNearbyPlayers(IActorRef sender, object message, Position center, float range)
    {
        var rangeSq = range * range;  // 제곱 거리로 비교 (최적화)
        
        foreach (var kvp in _playersInZone)
        {
            if (kvp.Key == sender) continue;  // 발신자 제외
            
            var playerInfo = kvp.Value;
            var distanceSq = GetDistanceSquared(center, playerInfo.Position);
            
            if (distanceSq <= rangeSq)
            {
                kvp.Key.Tell(message);
            }
        }
    }

    #endregion

    #region 유틸리티 메서드

    private bool IsWithinZoneBoundary(Position pos)
    {
        // Zone 중심에서 일정 거리 내에 있는지 체크
        var distanceFromSpawn = pos.DistanceTo(_zoneInfo.SpawnPoint);
        return distanceFromSpawn <= ZONE_BOUNDARY;
    }

    private float GetDistanceSquared(Position p1, Position p2)
    {
        var dx = p2.X - p1.X;
        var dy = p2.Y - p1.Y;
        return dx * dx + dy * dy;
    }

    #endregion

    #region Actor 라이프사이클

    protected override void PreStart()
    {
        Console.WriteLine($"[Zone-{_zoneInfo.ZoneId}] Starting Zone Actor - {_zoneInfo.Name}");
        base.PreStart();
    }

    protected override void PostStop()
    {
        Console.WriteLine($"[Zone-{_zoneInfo.ZoneId}] Stopped.");
        base.PostStop();
    }

    protected override void PreRestart(Exception reason, object message)
    {
        Console.WriteLine($"[Zone-{_zoneInfo.ZoneId}] Restarting due to: {reason.Message}");
        
        // 모든 플레이어에게 Zone 재시작 알림
        BroadcastToAll(new SystemMessage("Zone is restarting..."));
        
        base.PreRestart(reason, message);
    }

    protected override void PostRestart(Exception reason)
    {
        Console.WriteLine($"[Zone-{_zoneInfo.ZoneId}] Restarted successfully");
        
        // Zone 상태 복구
        // TODO: 필요시 플레이어 재연결 처리
        
        base.PostRestart(reason);
    }

    protected override SupervisorStrategy SupervisorStrategy()
    {
        return GameServerStrategies.ForZoneActor();
    }

    #endregion
}
