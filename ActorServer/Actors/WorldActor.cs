using ActorServer.Messages;
using Akka.Actor;

namespace ActorServer.Actors;

public class WorldActor : ReceiveActor
{
    private Dictionary<string, IActorRef> players = new();
    private Dictionary<string, IActorRef> clientConnections = new();
    private IActorRef zoneManager;
    private IActorRef metricsActor;

    protected override SupervisorStrategy SupervisorStrategy()
    {
        return GameServerStrategies.ForWorldActor();
    }
    public WorldActor()
    {
        zoneManager = Context.ActorOf(Props.Create<ZoneManager>(), "zone-manager");
        metricsActor = Context.ActorOf(Props.Create<MetricsActor>(), "metrics");

        Receive<PlayerLoginRequest>(HandlePlayerLogin);
        Receive<PlayerCommand>(HandlePlayerCommand);
        Receive<PlayerDisconnect>(HandlePlayerDisconnect);
        Receive<RequestZoneChange>(HandleZoneChangeRequest);
        Receive<GetMetrics>(msg => metricsActor.Forward(msg));

        Receive<RegisterClientConnection>(HandleRegisterClient);
        Receive<Terminated>(HandleTerminated);
        Receive<TestSupervision>(HandleTestSupervision);
    }
    private void HandlePlayerLogin(PlayerLoginRequest msg)
    {
        try
        {
            if (players.ContainsKey(msg.PlayerName))
            {
                Console.WriteLine($"[World] Player {msg.PlayerName} already logged in");
                return;
            }
            var playerActor = Context.ActorOf(
                Props.Create<PlayerActor>(msg.PlayerName),
                $"player-{msg.PlayerName}");
            Context.Watch(playerActor);
            // 플레이어 목록에 저장
            players[msg.PlayerName] = playerActor;
            if (clientConnections.TryGetValue(msg.PlayerName, out var clientActor))
            {
                playerActor.Tell(new SetClientConnection(clientActor));
            }

            zoneManager.Tell(new ChangeZoneRequest(playerActor, msg.PlayerName, "town"));
            metricsActor.Tell(new PlayerLoggedIn());
            Console.WriteLine($"[World] Player {msg.PlayerName} logged in");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[World] ERROR: Failed to create player {msg.PlayerName}: {ex.Message}");
            if (clientConnections.TryGetValue(msg.PlayerName, out var client))
            {
                client.Tell(new LoginFailed(msg.PlayerName, ex.Message));
            }
        }
    }
    private void HandlePlayerCommand(PlayerCommand cmd)
    {
        if (players.TryGetValue(cmd.PlayerName, out var playerActor))
        {
            try
            {
                if (cmd.Command == null)
                {
                    throw new ArgumentNullException(nameof(cmd.Command),
                        $"Command isnull for player {cmd.PlayerName}");
                }
                playerActor.Tell(cmd.Command);
                metricsActor.Tell(new MessageProcessed());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[World] ERROR: Failed to process command for {cmd.PlayerName}: {ex.Message}");
                throw;  // SupervisorStrategy가 처리
            }
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
            Context.Unwatch(playerActor);
            // Zone 제거는 ZoneManager 처리
            Context.Stop(playerActor);
            players.Remove(msg.PlayerName);
            clientConnections.Remove(msg.PlayerName);

            metricsActor.Tell(new PlayerLoggedOut());
            Console.WriteLine($"[World] Player {msg.PlayerName} disconnected");
        }
    }
    private void HandleRegisterClient(RegisterClientConnection msg)
    {
        clientConnections[msg.PlayerName] = msg.ClientActor;
        Console.WriteLine($"[World] Client connection registered for {msg.PlayerName}");
        if (players.TryGetValue(msg.PlayerName, out var playerActor))
        {
            playerActor.Tell(new SetClientConnection(msg.ClientActor));
            Console.WriteLine($"[World] Client connection sent to PlayerActor for {msg.PlayerName}");
        }

    }
    private void HandleTerminated(Terminated terminated)
    {
        var playerEntry = players.FirstOrDefault(x => x.Value.Equals(terminated.ActorRef));
        if (!string.IsNullOrEmpty(playerEntry.Key))
        {
            var playerName = playerEntry.Key;
            Console.WriteLine($"[World] TERMINATED: Player {playerName} actor has been terminated");

            if (ShouldRecreatePlayer(playerName))
            {
                Console.WriteLine($"[World] Attempting to recreate player {playerName}...");
                HandlePlayerLogin(new PlayerLoginRequest(playerName));
            }
            else
            {
                players.Remove(playerName);
                clientConnections.Remove(playerName);
            }
        }
    }
    private void HandleTestSupervision(TestSupervision test)
    {
        Console.WriteLine($"\n[World] === Starting Supervision Test: {test.TestType} ===");

        if (!players.TryGetValue(test.PlayerName, out var playerActor))
        {
            Console.WriteLine($"[World] Test failed: Player {test.PlayerName} not found");
            return;
        }

        switch (test.TestType)
        {
            case "ArgumentNull":
                playerActor.Tell(new TestNullCommand());
                break;

            case "InvalidMove":
                playerActor.Tell(new MoveCommand(new Position(float.NaN, float.NaN)));
                break;

            case "CrashPlayer":
                playerActor.Tell(new SimulateCrash("Test crash"));
                break;

            case "MemoryLeak":
                playerActor.Tell(new SimulateOutOfMemory());
                break;

            default:
                Console.WriteLine($"[World] Unknown test type: {test.TestType}");
                break;
        }
    }
    private bool ShouldRecreatePlayer(string playerName)
    {
        return clientConnections.ContainsKey(playerName);
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
public record RelayToClient(string PlayerName, string From, string Message);
public record ChatToClient(string From, string Message);
