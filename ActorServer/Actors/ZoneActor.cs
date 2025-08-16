using Akka.Actor;
using ActorServer.Messages;

namespace ActorServer.Actors;

public class ZoneActor : ReceiveActor
{
    private readonly ZoneInfo _zoneInfo;
    
    private readonly Dictionary<IActorRef, PlayerInfo> _playersInZone = new();
    private readonly Dictionary<IActorRef, long> _actorToPlayerId = new(); // Actor → ID 매핑

    public ZoneActor(ZoneInfo zoneInfo)
    {
        _zoneInfo = zoneInfo;

        Receive<AddPlayerToZone>(HandleAddPlayer);
        Receive<RemovePlayerFromZone>(HandleRemovePlayer);
        Receive<PlayerMovement>(HandlePlayerMovement);
        Receive<GetZoneStatus>(HandleGetZoneStatus);
        Receive<ChatMessage>(HandleChatMessage);
    }

    private void HandleAddPlayer(AddPlayerToZone msg)
    {
        // Zone 최대 인원 체크
        if (_zoneInfo.MaxPlayers > 0 && _playersInZone.Count >= _zoneInfo.MaxPlayers)
        {
            msg.PlayerActor.Tell(new ZoneFull(_zoneInfo.ZoneId));
            return;
        }

        var playerInfo = new PlayerInfo(
            msg.PlayerActor,
            msg.PlayerName,
            _zoneInfo.SpawnPoint
        );

        // IActorRef를 key로 저장
        _playersInZone[msg.PlayerActor] = playerInfo;
        _actorToPlayerId[msg.PlayerActor] = msg.PlayerId;
        
        // 플레이어에게 Zone 정보 전달
        msg.PlayerActor.Tell(new SetZone(Self));
        msg.PlayerActor.Tell(new ZoneEntered(_zoneInfo));

        Console.WriteLine($"[Zone-{_zoneInfo.Name}] Player {msg.PlayerName} (ID:{msg.PlayerId}) entered. ({_playersInZone.Count} players)");

        // 현재 Zone의 다른 플레이어 정보 전송
        msg.PlayerActor.Tell(new CurrentPlayersInZone(_playersInZone.Values));

        // 다른 플레이어들에게 새 플레이어 입장 알림
        BroadcastToOthers(msg.PlayerActor, new PlayerJoinedZone(playerInfo));
    }

    private void HandleRemovePlayer(RemovePlayerFromZone msg)
    {
        if (_playersInZone.TryGetValue(msg.PlayerActor, out var playerInfo))
        {
            _playersInZone.Remove(msg.PlayerActor);
            _actorToPlayerId.Remove(msg.PlayerActor);
            
            Console.WriteLine($"[Zone-{_zoneInfo.Name}] Player {playerInfo.Name} (ID:{msg.PlayerId}) left. ({_playersInZone.Count} players)");

            // 다른 모든 플레이어에게 퇴장 알림
            BroadcastToOthers(msg.PlayerActor, new PlayerLeftZone(playerInfo.Name));
        }
    }

    private void HandlePlayerMovement(PlayerMovement msg)
    {
        if (_playersInZone.TryGetValue(msg.PlayerActor, out var playerInfo))
        {
            var updatedInfo = playerInfo with { Position = msg.NewPosition };
            _playersInZone[msg.PlayerActor] = updatedInfo;

            Console.WriteLine($"[Zone-{_zoneInfo.Name}] {updatedInfo.Name} moved to ({msg.NewPosition.X}, {msg.NewPosition.Y})");

            // Zone 경계 체크
            if (!IsWithinZoneBoundary(msg.NewPosition))
            {
                msg.PlayerActor.Tell(new OutOfBoundWarning(_zoneInfo.ZoneId));
            }

            // 다른 모든 플레이어에게 위치 업데이트 브로드캐스트
            BroadcastToOthers(msg.PlayerActor, 
                new PlayerPositionUpdate(updatedInfo.Name, msg.NewPosition));
        }
    }

    private void HandleChatMessage(ChatMessage msg)
    {
        Console.WriteLine($"[Zone-{_zoneInfo.Name}] {msg.PlayerName}: {msg.Message}");
        var broadcast = new ChatBroadcast(msg.PlayerName, msg.Message, DateTime.Now);
        
        // 모든 플레이어에게 전송
        foreach (var player in _playersInZone.Keys)
        {
            player.Tell(broadcast);
        }
    }

    private void HandleGetZoneStatus(GetZoneStatus msg)
    {
        var playerNames = _playersInZone.Values.Select(p => p.Name).ToList();
        
        Sender.Tell(new ZoneStatus
        {
            ZoneInfo = _zoneInfo,
            PlayerCount = _playersInZone.Count,
            Players = playerNames
        });
    }

    // 브로드캐스트 (IActorRef 기반)
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

    // ID 기반 브로드캐스트 (ID를 알고 있을 때)
    private void BroadcastToOthersWithId(long senderId, object message)
    {
        // senderId에 해당하는 Actor 찾기
        var senderActor = _actorToPlayerId
            .FirstOrDefault(kvp => kvp.Value == senderId).Key;
        
        if (senderActor != null)
        {
            BroadcastToOthers(senderActor, message);
        }
    }

    private bool IsWithinZoneBoundary(Position pos)
    {
        // 간단한 경계 체크 (Zone 중심에서 ±500 범위)
        return Math.Abs(pos.X - _zoneInfo.SpawnPoint.X) < 500 &&
               Math.Abs(pos.Y - _zoneInfo.SpawnPoint.Y) < 500;
    }
}