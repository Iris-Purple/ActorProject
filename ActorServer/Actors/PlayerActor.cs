using Akka.Actor;
using ActorServer.Messages;
namespace ActorServer.Actors;

public record PlayerPositionUpdate(string PlayerName, Position NewPosition);
public record PlayerJoinedZone(PlayerInfo Player);

public class PlayerActor : ReceiveActor
{
    private string playerName;
    private Position currentPosition;
    private IActorRef? clientConnection;
    private IActorRef? currentZone; // 현재 속한 Zone
    private string currentZoneId = "town";

    // 다른 플레이어들의 위치 저장
    private Dictionary<string, Position> otherPlayers = new();
    public PlayerActor(string name)
    {
        playerName = name;
        currentPosition = new Position(0, 0);

        Receive<MoveCommand>(HandleMove);
        Receive<CurrentPlayersInZone>(HandleZoneInfo);
        Receive<PlayerPositionUpdate>(HandleOtherPlayerMove);
        Receive<PlayerJoinedZone>(HandlePlayerJoined);
        Receive<PlayerLeftZone>(HandlePlayerLeft);
        Receive<SetZone>(HandleSetZone);

        Receive<ChangeZoneResponse>(HandleChangeZoneResponse);
        Receive<ZoneEntered>(HandleZoneEntered);
        Receive<ZoneFull>(HandleZoneFull);
        Receive<OutOfBoundWarning>(HandleOutOfBoundWarning);

        Receive<ChatBroadcast>(HandleChatBroadcast);
        Receive<ChatMessage>(HandleSendChat);
    }

    private void HandleChangeZoneResponse(ChangeZoneResponse msg)
    {
        if (msg.Success)
        {
            currentZoneId = msg.Message;
            Console.WriteLine($"[{playerName}] Successfully changed zone to: {currentZoneId}");

            // 이전 Zone의 플레이어 목록 클리어
            otherPlayers.Clear();
        }
        else
        {
            Console.WriteLine($"[{playerName}] Failed to change zone: {msg.Message}");
        }
    }
    private void HandleZoneEntered(ZoneEntered msg)
    {
        // 새 Zone에 진입했을 때
        currentZoneId = msg.ZoneInfo.ZoneId;
        currentPosition = msg.ZoneInfo.SpawnPoint;

        Console.WriteLine($"[{playerName}] Entered zone: {msg.ZoneInfo.Name}");
        Console.WriteLine($"[{playerName}] Zone type: {msg.ZoneInfo.Type}");
        Console.WriteLine($"[{playerName}] Spawned at ({currentPosition.X}, {currentPosition.Y})");
        Console.WriteLine($"[{playerName}] Level range: {msg.ZoneInfo.MinLevel} - {msg.ZoneInfo.MaxLevel}");
    }
    private void HandleZoneFull(ZoneFull msg)
    {
        Console.WriteLine($"[{playerName}] Cannot enter zone {msg.ZoneId}: Zone is full!");
    }

    private void HandleOutOfBoundWarning(OutOfBoundWarning msg)
    {
        Console.WriteLine($"[{playerName}] WARNING: You are moving out of zone {msg.ZoneId} boundaries!");
    }

    private void HandleSetZone(SetZone msg)
    {
        currentZone = msg.ZoneActor;
        Console.WriteLine($"[{playerName}] Assigned to zone");
    }
    private void HandleMove(MoveCommand cmd)
    {
        // 위치 업데이트
        currentPosition = cmd.NewPosition;
        Console.WriteLine($"[{playerName}] Moving to ({cmd.NewPosition.X}, {cmd.NewPosition.Y})");
        currentZone?.Tell(new PlayerMovement(Self, currentPosition));
    }

    private void HandleZoneInfo(CurrentPlayersInZone msg)
    {
        Console.WriteLine($"[{playerName}] Received zone info. Players in zone:");
        foreach (var player in msg.Players)
        {
            if (player.Name != playerName)
            {
                Console.WriteLine($" - {player.Name} at ({player.Position.X}, {player.Position.Y})");
            }
        }
    }
    private void HandleOtherPlayerMove(PlayerPositionUpdate msg)
    {
        otherPlayers[msg.PlayerName] = msg.NewPosition;
        Console.WriteLine($"[{playerName}] {msg.PlayerName} moved to ({msg.NewPosition.X}, {msg.NewPosition.Y})");
    }
    private void HandlePlayerJoined(PlayerJoinedZone msg)
    {
        otherPlayers[msg.Player.Name] = msg.Player.Position;
        Console.WriteLine($"[{playerName}] New player joined: {msg.Player.Name}");
    }
    private void HandlePlayerLeft(PlayerLeftZone msg)
    {
        if (otherPlayers.ContainsKey(msg.PlayerName))
        {
            otherPlayers.Remove(msg.PlayerName);
            Console.WriteLine($"[{playerName}] Player {msg.PlayerName} left the zone");
        }
    }
    private void HandleChatBroadcast(ChatBroadcast msg)
    {
        if (msg.PlayerName == playerName)
        {
            Console.WriteLine($"[{playerName}] You said: {msg.Message}");
        }
        else
        {
            Console.WriteLine($"[{playerName}] {msg.PlayerName} says: {msg.Message}");
        }
        clientConnection?.Tell(new ChatToClient(msg.PlayerName, msg.Message));
    }
    private void HandleSendChat(ChatMessage msg)
    {
        currentZone?.Tell(msg);
    }
}
