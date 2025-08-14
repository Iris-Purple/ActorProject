using Akka.Actor;
using ActorServer.Messages;

namespace ActorServer.Actors;

public class ZoneActor : ReceiveActor
{
    private readonly ZoneInfo _zoneInfo;
    private readonly Dictionary<IActorRef, PlayerInfo> _playersInZone = new();

    public ZoneActor(ZoneInfo zoneInfo)
    {
        _zoneInfo = zoneInfo;

        Receive<AddPlayerToZone>(HandleAddPlayer);
        Receive<PlayerMovement>(HandlePlayerMovement);
        Receive<RemovePlayerFromZone>(HandleRemovePlayer);
        Receive<GetZoneStatus>(HandleGetZoneStatus);

        Receive<ChatMessage>(HandleChatMessage);
    }

    private void HandleChatMessage(ChatMessage msg)
    {
        Console.WriteLine($"[Zone-{_zoneInfo.Name}] {msg.PlayerName}: {msg.Message}");
        var broadcast = new ChatBroadcast(msg.PlayerName, msg.Message, DateTime.Now);
        foreach (var player in _playersInZone.Keys)
        {
            player.Tell(broadcast);
        }
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
            _zoneInfo.SpawnPoint // Zone의 스폰 포인트에서 시작
        );

        _playersInZone[msg.PlayerActor] = playerInfo;
        // 플레이어에게 Zone 정보 전달
        msg.PlayerActor.Tell(new SetZone(Self));
        msg.PlayerActor.Tell(new ZoneEntered(_zoneInfo));

        Console.WriteLine($"[Zone-{_zoneInfo.Name}] Player {msg.PlayerName} entered. ({_playersInZone.Count} players)");

        // 현재 Zone의 다른 플레이어 정보 전송
        msg.PlayerActor.Tell(new CurrentPlayersInZone(_playersInZone.Values));

        // 다른 플레이어들에게 새 플레이어 입장 알림
        BroadcastToOthers(msg.PlayerActor, new PlayerJoinedZone(playerInfo));
    }

    private void HandlePlayerMovement(PlayerMovement msg)
    {
        if (_playersInZone.TryGetValue(msg.PlayerActor, out var playerInfo))
        {
            var updatedInfo = playerInfo with { Position = msg.NewPosition };
            _playersInZone[msg.PlayerActor] = updatedInfo;

            Console.WriteLine($"[Zone-{_zoneInfo.Name}] {updatedInfo.Name} moved to ({msg.NewPosition.X}, {msg.NewPosition.Y})");

            // Zone 경계 체크 (옵션)
            if (!IsWithinZoneBoundary(msg.NewPosition))
            {
                msg.PlayerActor.Tell(new OutOfBoundWarning(_zoneInfo.ZoneId));
            }

            // 다른 모든 플레이어에게 위치 업데이트 브로드캐스트
            BroadcastToOthers(msg.PlayerActor,
                new PlayerPositionUpdate(updatedInfo.Name, msg.NewPosition));
        }
    }

    private void HandleRemovePlayer(RemovePlayerFromZone msg)
    {
        if (_playersInZone.TryGetValue(msg.PlayerActor, out var playerInfo))
        {
            _playersInZone.Remove(msg.PlayerActor);
            Console.WriteLine($"[Zone-{_zoneInfo.Name}] Player {playerInfo.Name} left. ({_playersInZone.Count} players)");

            // 다른 모든 플레이어에게 퇴장 알림
            BroadcastToOthers(msg.PlayerActor, new PlayerLeftZone(playerInfo.Name));
        }
    }

    private void HandleGetZoneStatus(GetZoneStatus msg)
    {
        Sender.Tell(new ZoneStatus
        {
            ZoneInfo = _zoneInfo,
            PlayerCount = _playersInZone.Count,
            Players = _playersInZone.Values.Select(p => p.Name).ToList()
        });
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

    private bool IsWithinZoneBoundary(Position pos)
    {
        // 간단한 경계 체크 (Zone 중심에서 ±500 범위)
        return Math.Abs(pos.X - _zoneInfo.SpawnPoint.X) < 500 &&
               Math.Abs(pos.Y - _zoneInfo.SpawnPoint.Y) < 500;
    }
}
