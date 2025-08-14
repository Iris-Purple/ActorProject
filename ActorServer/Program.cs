
using ActorServer.Actors;
using ActorServer.Messages;
using ActorServer.Network;
using Akka.Actor;

class Program
{
    static async Task Main(string[] args)
    {
        var system = ActorSystem.Create("MMOSrever");
        var worldActor = system.ActorOf(Props.Create<WorldActor>(), "world");

        var tcpServer = system.ActorOf(
            Props.Create(() => new TcpServerActor(worldActor, 9999)),
            "tcp-server"
        );

        Console.WriteLine("MMORPG Server started with Zone system...");
        Console.WriteLine("Available zones: town, forest, dungeon-1\n");

        // 테스트 시나리오
        //await RunZoneTestScenario(worldActor);

        Console.WriteLine("\n--- Server running. Press Enter to shutdown ---");
        Console.ReadLine();

        Console.WriteLine("Shutting down server...");
        await system.Terminate();
        Console.WriteLine("Server terminated.");
    }

    static async Task RunZoneTestScenario(IActorRef worldActor)
    {
        Console.WriteLine("=== Zone Movement Test Scenario ===\n");

        // 1. 플레이어 3명 로그인 (모두 town에서 시작)
        Console.WriteLine("--- Phase 1: Players logging in ---");
        worldActor.Tell(new PlayerLoginRequest("Alice"));
        await Task.Delay(500);

        worldActor.Tell(new PlayerLoginRequest("Bob"));
        await Task.Delay(500);

        worldActor.Tell(new PlayerLoginRequest("Charlie"));
        await Task.Delay(1000);

        // 2. Town에서 이동 테스트
        Console.WriteLine("\n--- Phase 2: Movement in Town ---");
        worldActor.Tell(new PlayerCommand("Alice", new MoveCommand(new Position(10, 10))));
        await Task.Delay(500);

        worldActor.Tell(new PlayerCommand("Bob", new MoveCommand(new Position(20, 20))));
        await Task.Delay(1000);

        Console.WriteLine("\n--- Phase 2.5: Chat Test in Town ---");
        worldActor.Tell(new PlayerCommand("Alice", new ChatMessage("Alice", "Hello everyone!")));
        await Task.Delay(500);

        worldActor.Tell(new PlayerCommand("Bob", new ChatMessage("Bob", "Hi Alice!")));
        await Task.Delay(500);

        // 3. Alice가 Forest로 이동
        Console.WriteLine("\n--- Phase 3: Alice moves to Forest ---");
        worldActor.Tell(new RequestZoneChange("Alice", "forest"));
        await Task.Delay(1000);

        // Forest에서 이동
        worldActor.Tell(new PlayerCommand("Alice", new MoveCommand(new Position(110, 110))));
        await Task.Delay(1000);

        // 4. Bob도 Forest로 이동
        Console.WriteLine("\n--- Phase 4: Bob joins Alice in Forest ---");
        worldActor.Tell(new RequestZoneChange("Bob", "forest"));
        await Task.Delay(1000);

        // Bob은 Forest에 있음
        Console.WriteLine("\n--- Phase 4.5: Zone-separated Chat Test ---");
        worldActor.Tell(new PlayerCommand("Bob", new ChatMessage("Bob", "Anyone here in the Forest?")));
        await Task.Delay(500);

        // Chrlie는 Town에 있음
        worldActor.Tell(new PlayerCommand("Charlie", new ChatMessage("Charlie", "Hello from Town!")));
        await Task.Delay(500);
        // Alice도 Forest에 있으니 Bob의 메시지는 보이지만 Charlie는 안 보임
        worldActor.Tell(new PlayerCommand("Alice",
            new ChatMessage("Alice", "Yes Bob, I'm in Forest too!")));
        await Task.Delay(1000);

        Console.WriteLine("\n--- Chat Test Result ---");
        Console.WriteLine("Expected: Bob and Alice see each other's messages (both in Forest)");
        Console.WriteLine("Expected: Charlie doesn't see Forest chat (he's in Town)");
        Console.WriteLine("Expected: Bob/Alice don't see Charlie's Town chat");

        // 두 플레이어가 Forest에서 이동
        worldActor.Tell(new PlayerCommand("Alice", new MoveCommand(new Position(120, 120))));
        worldActor.Tell(new PlayerCommand("Bob", new MoveCommand(new Position(130, 130))));
        await Task.Delay(1000);

        // 5. Charlie는 Dungeon으로 이동
        Console.WriteLine("\n--- Phase 5: Charlie enters Dungeon ---");
        worldActor.Tell(new RequestZoneChange("Charlie", "dungeon-1"));
        await Task.Delay(1000);

        worldActor.Tell(new PlayerCommand("Charlie", new MoveCommand(new Position(210, 210))));
        await Task.Delay(1000);

        // 6. Alice가 Town으로 복귀
        Console.WriteLine("\n--- Phase 6: Alice returns to Town ---");
        worldActor.Tell(new RequestZoneChange("Alice", "town"));
        await Task.Delay(1000);

        // 7. 플레이어 위치 확인을 위한 추가 이동
        Console.WriteLine("\n--- Phase 7: Final movements ---");
        worldActor.Tell(new PlayerCommand("Alice", new MoveCommand(new Position(5, 5))));
        worldActor.Tell(new PlayerCommand("Bob", new MoveCommand(new Position(150, 150))));
        worldActor.Tell(new PlayerCommand("Charlie", new MoveCommand(new Position(220, 220))));
        await Task.Delay(1000);

        Console.WriteLine("\n--- Phase 8: Metrics Test ---");
        worldActor.Tell(new GetMetrics());
        await Task.Delay(1000);

        worldActor.Tell(new PlayerDisconnect("Bob"));
        await Task.Delay(500);

        worldActor.Tell(new GetMetrics());
        await Task.Delay(1000);

        Console.WriteLine("\n=== Test Scenario Complete ===");
    }
}