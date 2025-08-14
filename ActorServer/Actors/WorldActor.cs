using ActorServer.Messages;
using Akka.Actor;

namespace ActorServer.Actors;

public class WorldActor : ReceiveActor
{
    private Dictionary<string, IActorRef> players = new();
    private IActorRef zoneManager;
    private IActorRef metricsActor;

    public WorldActor()
    {
        zoneManager = Context.ActorOf(Props.Create<ZoneManager>(), "zone-manager");
        metricsActor = Context.ActorOf(Props.Create<MetricsActor>(), "metrics");

        Receive<PlayerLoginRequest>(HandlePlayerLogin);
        Receive<PlayerCommand>(HandlePlayerCommand);
        Receive<PlayerDisconnect>(HandlePlayerDisconnect);
        Receive<GetMetrics>(msg => metricsActor.Forward(msg));
    }
    private void HandlePlayerLogin(PlayerLoginRequest msg)
    {
        // 플레이어 액터 생성
        var playerActor = Context.ActorOf(
            Props.Create<PlayerActor>(msg.PlayerName),
            $"player-{msg.PlayerName}");
        // 플레이어 목록에 저장
        players[msg.PlayerName] = playerActor;
        zoneManager.Tell(new ChangeZoneRequest(playerActor, msg.PlayerName, "town"));
        metricsActor.Tell(new PlayerLoggedIn());
        Console.WriteLine($"[World] Player {msg.PlayerName} logged in");
    }
    private void HandlePlayerCommand(PlayerCommand cmd)
    {
        if (players.TryGetValue(cmd.PlayerName, out var playerActor))
        {
            playerActor.Tell(cmd.Command);
        }
        else
        {
            Console.WriteLine($"[World] Player {cmd.PlayerName} not found");
        }
    }
    private void HandlePlayerDisconnect(PlayerDisconnect msg)
    {
        if (players.TryGetValue(msg.PlayerName, out var playerActor))
        {
            // Zone 제거는 ZoneManager 처리
            Context.Stop(playerActor);
            players.Remove(msg.PlayerName);
            metricsActor.Tell(new PlayerLoggedOut());
            Console.WriteLine($"[World] Player {msg.PlayerName} disconnected");
        }
    }
    private void HandleZoneChangeRequest(RequestZoneChange msg)
    {
        if (players.TryGetValue(msg.PlayerName, out var playerActor))
        {
            zoneManager.Tell(new ChangeZoneRequest(playerActor, msg.PlayerName, msg.TargetZoneId));
        }
        else
        {
            Console.WriteLine($"[World] Player {msg.PlayerName} not found for zone change");
        }
    }
}

public record RequestZoneChange(string PlayerName, string TargetZoneId);
public record RegisterClientConnection(string PlayerName, IActorRef ClientActor);
public record RelayToClient(string PlayerName, string From, string Message);
public record ChatToClient(string From, string Message);
