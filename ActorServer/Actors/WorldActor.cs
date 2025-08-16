using ActorServer.Messages;
using Akka.Actor;

namespace ActorServer.Actors;

public class WorldActor : ReceiveActor
{
    private Dictionary<long, IActorRef> players = new();
    private Dictionary<long, IActorRef> clientConnections = new();
    private Dictionary<string, long> nameToIdCache = new();
    private IActorRef zoneManager;
    private readonly PlayerIdManager idManager = PlayerIdManager.Instance!;

    protected override SupervisorStrategy SupervisorStrategy()
    {
        return GameServerStrategies.ForWorldActor();
    }
    public WorldActor()
    {
        zoneManager = Context.ActorOf(Props.Create<ZoneManager>(), "zone-manager");

        Receive<PlayerLoginRequest>(HandlePlayerLogin);
        Receive<PlayerCommand>(HandlePlayerCommand);
        Receive<PlayerDisconnect>(HandlePlayerDisconnect);
        Receive<RequestZoneChange>(HandleZoneChangeRequest);
        Receive<RegisterClientConnection>(HandleRegisterClient);
        Receive<Terminated>(HandleTerminated);
        Receive<TestSupervision>(HandleTestSupervision);
    }
    private void HandlePlayerLogin(PlayerLoginRequest msg)
    {
        try
        {
            var playerId = idManager.GetOrCreatePlayerId(msg.PlayerName);
            if (players.ContainsKey(playerId))
            {
                Console.WriteLine($"[World] Player {msg.PlayerName} already logged in");
                // 기존 연결 종료 후 새로 연결 (재접속 처리)
                Context.Stop(players[playerId]);
                players.Remove(playerId);
            }

            var playerActor = Context.ActorOf(
                Props.Create<PlayerActor>(playerId, msg.PlayerName),
                idManager.GetActorName(playerId));
            Context.Watch(playerActor);

            players[playerId] = playerActor;
            nameToIdCache[msg.PlayerName.ToLower()] = playerId;
            if (clientConnections.TryGetValue(playerId, out var clientActor))
            {
                playerActor.Tell(new SetClientConnection(clientActor));
            }

            zoneManager.Tell(new ChangeZoneRequest(playerActor, playerId, msg.PlayerName, "town"));
            Console.WriteLine($"[World] Player {msg.PlayerName} (ID:{playerId}) logged in");
            Console.WriteLine($"[World] Total online players: {players.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[World] ERROR: Failed to create player {msg.PlayerName}: {ex.Message}");
            var playerId = idManager.GetPlayerId(msg.PlayerName);
            if (playerId.HasValue && clientConnections.TryGetValue(playerId.Value, out var client))
            {
                client.Tell(new LoginFailed(msg.PlayerName, ex.Message));
            }
        }
    }
    private void HandlePlayerCommand(PlayerCommand cmd)
    {
        var playerId = idManager.GetPlayerId(cmd.PlayerName);
        if (!playerId.HasValue)
        {
            Console.WriteLine($"[World] Player {cmd.PlayerName} not registered");
            return;
        }

        if (players.TryGetValue(playerId.Value, out var playerActor))
        {
            try
            {
                if (cmd.Command == null)
                {
                    throw new ArgumentNullException(nameof(cmd.Command),
                        $"Command isnull for player {cmd.PlayerName}");
                }
                playerActor.Tell(cmd.Command);
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
        var playerId = idManager.GetPlayerId(msg.PlayerName);
        if (!playerId.HasValue)
        {
            Console.WriteLine($"[World] Unknown player disconnect: {msg.PlayerName}");
            return;
        }
        if (players.TryGetValue(playerId.Value, out var playerActor))
        {
            Context.Unwatch(playerActor);
            Context.Stop(playerActor);

            players.Remove(playerId.Value);
            clientConnections.Remove(playerId.Value);
            nameToIdCache.Remove(msg.PlayerName.ToLower());

            Console.WriteLine($"[World] Player {msg.PlayerName} (ID:{playerId}) disconnected");
            Console.WriteLine($"[World] Remaining online players: {players.Count}");
        }
    }
    private void HandleRegisterClient(RegisterClientConnection msg)
    {
        var playerId = idManager.GetOrCreatePlayerId(msg.PlayerName);
        clientConnections[playerId] = msg.ClientActor;
        Console.WriteLine($"[World] Client connection registered for {msg.PlayerName}");
        if (players.TryGetValue(playerId, out var playerActor))
        {
            playerActor.Tell(new SetClientConnection(msg.ClientActor));
            Console.WriteLine($"[World] Client connection sent to PlayerActor for {msg.PlayerName}");
        }
    }
    private void HandleTerminated(Terminated terminated)
    {
        var playerEntry = players.FirstOrDefault(x => x.Value.Equals(terminated.ActorRef));
        if (playerEntry.Key == 0)
        {
            return;
        }

        var playerId = playerEntry.Key;
        var playerName = idManager.GetPlayerName(playerId) ?? "Unknown";
        Console.WriteLine($"[World] TERMINATED: Player {playerName} (ID:{playerId}) actor has been terminated");

        if (ShouldRecreatePlayer(playerId))
        {
            Console.WriteLine($"[World] Attempting to recreate player {playerName} (ID:{playerId})...");
            HandlePlayerLogin(new PlayerLoginRequest(playerName));
        }
        else
        {
            players.Remove(playerId);
            clientConnections.Remove(playerId);
            nameToIdCache.Remove(playerName.ToLower());
        }
    }
    private void HandleTestSupervision(TestSupervision test)
    {
        Console.WriteLine($"\n[World] === Starting Supervision Test: {test.TestType} ===");

        var playerId = idManager.GetPlayerId(test.PlayerName);
        if (!playerId.HasValue || !players.TryGetValue(playerId.Value, out var playerActor))
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
    private bool ShouldRecreatePlayer(long playerId)
    {
        return clientConnections.ContainsKey(playerId);
    }
    private void HandleZoneChangeRequest(RequestZoneChange msg)
    {
        var playerId = idManager.GetPlayerId(msg.PlayerName);
        if (!playerId.HasValue)
        {
            Console.WriteLine($"[World] Unknown player for zone change: {msg.PlayerName}");
            return;
        }

        if (players.TryGetValue(playerId.Value, out var playerActor))
        {
            zoneManager.Tell(new ChangeZoneRequest(playerActor, playerId.Value, msg.PlayerName, msg.TargetZoneId));
        }
        else
        {
            Console.WriteLine($"[World] Player {msg.PlayerName} (ID:{playerId}) not found for zone change");
        }
    }
}
