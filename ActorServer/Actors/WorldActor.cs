using ActorServer.Messages;
using Akka.Actor;

namespace ActorServer.Actors;

public class WorldActor : ReceiveActor
{
    private Dictionary<long, IActorRef> players = new();
    private Dictionary<long, IActorRef> clientConnections = new();
    private IActorRef zoneActor;

    protected override SupervisorStrategy SupervisorStrategy()
    {
        return GameServerStrategies.ForWorldActor();
    }
    public WorldActor()
    {
        zoneActor = Context.ActorOf(Props.Create<ZoneActor>(), "zone-manager");

        Receive<PlayerEnterWorld>(HandlePlayerEnterWorld);
        Receive<PlayerCommand>(HandlePlayerCommand);
        Receive<PlayerDisconnect>(HandlePlayerDisconnect);
        Receive<RequestZoneChange>(HandleZoneChangeRequest);
        Receive<RegisterClientConnection>(HandleRegisterClient);
        Receive<Terminated>(HandleTerminated);
        Receive<TestSupervision>(HandleTestSupervision);
    }
    private void HandlePlayerEnterWorld(PlayerEnterWorld msg)
    {
        try
        {
            var playerId = msg.PlayerId;
            if (players.ContainsKey(playerId))
            {
                Console.WriteLine($"[World] Player (ID:{playerId}) reconnecting...");
                // 기존 연결 종료 후 새로 연결 (재접속 처리)
                Context.Stop(players[playerId]);
                players.Remove(playerId);
                Thread.Sleep(100);
            }
            var actorName = $"player-{playerId}";
            var playerActor = Context.ActorOf(
                Props.Create<PlayerActor>(playerId),
                actorName);

            Context.Watch(playerActor);
            players[playerId] = playerActor;

            if (clientConnections.TryGetValue(playerId, out var clientActor))
            {
                playerActor.Tell(new SetClientConnection(clientActor));
            }

            zoneActor.Tell(new ChangeZoneRequest(playerActor, playerId, "town"));
            Console.WriteLine($"[World] Player (ID:{playerId}) logged in");
            Console.WriteLine($"[World] Total online players: {players.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[World] ERROR: Failed to create player {msg.PlayerId}: {ex.Message}");
            if (clientConnections.TryGetValue(msg.PlayerId, out var client))
            {
                client.Tell(new LoginFailed(msg.PlayerId, ex.Message));
            }
        }
    }
    private void HandlePlayerCommand(PlayerCommand cmd)
    {
        if (players.TryGetValue(cmd.PlayerId, out var playerActor))
        {
            playerActor.Tell(cmd.Command);
        }
        else
        {
            Console.WriteLine($"[World] Player ID {cmd.PlayerId} not found");
        }
    }
    private void HandlePlayerDisconnect(PlayerDisconnect msg)
    {
        if (players.TryGetValue(msg.PlayerId, out var playerActor))
        {
            Context.Unwatch(playerActor);
            Context.Stop(playerActor);

            players.Remove(msg.PlayerId);
            clientConnections.Remove(msg.PlayerId);

            Console.WriteLine($"[World] Player ID:{msg.PlayerId} disconnected");
            Console.WriteLine($"[World] Remaining online players: {players.Count}");
        }
    }
    private void HandleRegisterClient(RegisterClientConnection msg)
    {
        clientConnections[msg.PlayerId] = msg.ClientActor;
        Console.WriteLine($"[World] Client connection registered for PlayerId: {msg.PlayerId}");

        if (players.TryGetValue(msg.PlayerId, out var playerActor))
        {
            playerActor.Tell(new SetClientConnection(msg.ClientActor));
        }
    }
    private void HandleTerminated(Terminated terminated)
    {
        var playerEntry = players.FirstOrDefault(x => x.Value.Equals(terminated.ActorRef));
        if (playerEntry.Key == 0) return;

        var playerId = playerEntry.Key;
        Console.WriteLine($"[World] TERMINATED: Player ID:{playerId} actor has been terminated");

        if (ShouldRecreatePlayer(playerId))
        {
            Console.WriteLine($"[World] Attempting to recreate player ID:{playerId}...");
        }
        else
        {
            players.Remove(playerId);
            clientConnections.Remove(playerId);
            // nameToIdCache 관련 코드 삭제
        }
    }
    private void HandleTestSupervision(TestSupervision test)
    {
        Console.WriteLine($"[World] === Starting Supervision Test: {test.TestType} ===");
    }
    private bool ShouldRecreatePlayer(long playerId)
    {
        return clientConnections.ContainsKey(playerId);
    }
    private void HandleZoneChangeRequest(RequestZoneChange msg)
    {
        if (players.TryGetValue(msg.PlayerId, out var playerActor))
        {
            zoneActor.Tell(new ChangeZoneRequest(playerActor, msg.PlayerId, msg.TargetZoneId));
            Console.WriteLine($"[World] Zone change requested - Player:{msg.PlayerId} -> Zone:{msg.TargetZoneId}");
        }
        else
        {
            Console.WriteLine($"[World] Zone change failed - Player {msg.PlayerId} not found");
            
            if (clientConnections.TryGetValue(msg.PlayerId, out var client))
            {
                client.Tell(new CommandFailed(msg.PlayerId, "zone", "Player not found. Please login first."));
            }
        }
    }
}
