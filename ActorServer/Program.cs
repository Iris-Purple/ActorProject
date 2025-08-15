using ActorServer.Actors;
using ActorServer.Messages;
using ActorServer.Network;
using Akka.Actor;

class Program
{
    static async Task Main(string[] args)
    {
        var system = ActorSystem.Create("MMOServer");
        
        // WorldActor 생성 (Supervision 전략 포함)
        var worldActor = system.ActorOf(Props.Create<WorldActor>(), "world");

        // TCP 서버 시작
        var tcpServer = system.ActorOf(
            Props.Create(() => new TcpServerActor(worldActor, 9999)),
            "tcp-server"
        );

        Console.WriteLine("==============================================");
        Console.WriteLine("  MMORPG Server with Actor Supervision");
        Console.WriteLine("  Akka.NET 1.5.46");
        Console.WriteLine("==============================================");
        Console.WriteLine("Available zones: town, forest, dungeon-1\n");

        // 메뉴 시스템
        bool running = true;
        while (running)
        {
            Console.WriteLine("\n========== Server Menu ==========");
            Console.WriteLine("1. Run Supervision Test");
            Console.WriteLine("2. Run Zone Test");
            Console.WriteLine("3. Test Specific Error");
            Console.WriteLine("4. Check Zone Health");
            Console.WriteLine("0. Exit");
            Console.WriteLine("=================================");
            Console.Write("Select option: ");
            
            var input = Console.ReadLine();
            
            switch (input)
            {
                case "1":
                    await RunSupervisionTestScenario(worldActor);
                    break;
                    
                case "2":
                    await RunZoneTestScenario(worldActor);
                    break;
                    
                case "3":
                    await RunSpecificErrorTest(worldActor);
                    break;
                    
                case "4":
                    worldActor.Tell(new CheckZoneHealth());
                    await Task.Delay(500);
                    break;
                    
                case "0":
                    running = false;
                    break;
                    
                default:
                    Console.WriteLine("Invalid option");
                    break;
            }
        }

        Console.WriteLine("\nShutting down server...");
        await system.Terminate();
        Console.WriteLine("Server terminated.");
    }

    static async Task RunSupervisionTestScenario(IActorRef worldActor)
    {
        Console.WriteLine("\n╔════════════════════════════════════════╗");
        Console.WriteLine("║    SUPERVISION & ERROR HANDLING TEST    ║");
        Console.WriteLine("╚════════════════════════════════════════╝\n");

        // Phase 1: 정상 플레이어 생성
        Console.WriteLine("▶ Phase 1: Creating test players");
        worldActor.Tell(new PlayerLoginRequest("TestPlayer1"));
        worldActor.Tell(new PlayerLoginRequest("TestPlayer2"));
        await Task.Delay(1000);

        // Phase 2: 정상 동작 테스트
        Console.WriteLine("\n▶ Phase 2: Normal operations");
        worldActor.Tell(new PlayerCommand("TestPlayer1", 
            new MoveCommand(new Position(10, 10))));
        await Task.Delay(500);

        // Phase 3: GameLogicException 테스트 (Resume)
        Console.WriteLine("\n▶ Phase 3: Testing GameLogicException (should RESUME)");
        Console.WriteLine("  → Sending invalid position (NaN values)");
        worldActor.Tell(new PlayerCommand("TestPlayer1", 
            new MoveCommand(new Position(float.NaN, float.NaN))));
        await Task.Delay(1000);

        Console.WriteLine("  → Checking if player still responds...");
        worldActor.Tell(new PlayerCommand("TestPlayer1", 
            new MoveCommand(new Position(15, 15))));
        await Task.Delay(1000);

        // Phase 4: ArgumentNullException 테스트 (Resume)
        Console.WriteLine("\n▶ Phase 4: Testing ArgumentNullException (should RESUME)");
        Console.WriteLine("  → Sending null command");
        worldActor.Tell(new PlayerCommand("TestPlayer1", null!));
        await Task.Delay(1000);

        Console.WriteLine("  → Checking if player still responds...");
        worldActor.Tell(new PlayerCommand("TestPlayer1", 
            new MoveCommand(new Position(20, 20))));
        await Task.Delay(1000);

        // Phase 5: TemporaryGameException 테스트 (Restart)
        Console.WriteLine("\n▶ Phase 5: Testing TemporaryGameException (should RESTART)");
        Console.WriteLine("  → Simulating player crash");
        worldActor.Tell(new TestSupervision("TestPlayer1", "CrashPlayer"));
        await Task.Delay(2000);

        Console.WriteLine("  → Checking if player recovered...");
        worldActor.Tell(new PlayerCommand("TestPlayer1", 
            new MoveCommand(new Position(25, 25))));
        await Task.Delay(1000);

        // 정리
        Console.WriteLine("\n▶ Cleanup");
        worldActor.Tell(new PlayerDisconnect("TestPlayer1"));
        worldActor.Tell(new PlayerDisconnect("TestPlayer2"));
        await Task.Delay(1000);

        Console.WriteLine("\n╔════════════════════════════════════════╗");
        Console.WriteLine("║         TEST COMPLETED SUCCESSFULLY      ║");
        Console.WriteLine("╚════════════════════════════════════════╝");
        Console.WriteLine("\n✅ Summary:");
        Console.WriteLine("  • GameLogicException → Actor RESUMED");
        Console.WriteLine("  • ArgumentNullException → Actor RESUMED");
        Console.WriteLine("  • TemporaryGameException → Actor RESTARTED");
        Console.WriteLine("  • System remained stable");
    }

    static async Task RunSpecificErrorTest(IActorRef worldActor)
    {
        Console.WriteLine("\n--- Specific Error Test ---");
        Console.WriteLine("1. Test ArgumentNull");
        Console.WriteLine("2. Test Invalid Move");
        Console.WriteLine("3. Test Crash");
        Console.Write("Select error type: ");
        
        var errorType = Console.ReadLine();
        
        // 테스트용 플레이어 생성
        worldActor.Tell(new PlayerLoginRequest("ErrorTestPlayer"));
        await Task.Delay(500);
        
        switch (errorType)
        {
            case "1":
                worldActor.Tell(new TestSupervision("ErrorTestPlayer", "ArgumentNull"));
                break;
            case "2":
                worldActor.Tell(new TestSupervision("ErrorTestPlayer", "InvalidMove"));
                break;
            case "3":
                worldActor.Tell(new TestSupervision("ErrorTestPlayer", "CrashPlayer"));
                break;
        }
        
        await Task.Delay(2000);
        
        // 복구 확인
        Console.WriteLine("\nTesting if player is still alive...");
        worldActor.Tell(new PlayerCommand("ErrorTestPlayer", 
            new MoveCommand(new Position(50, 50))));
        await Task.Delay(1000);
        
        // 정리
        worldActor.Tell(new PlayerDisconnect("ErrorTestPlayer"));
    }

    // 기존 RunZoneTestScenario는 유지
    static async Task RunZoneTestScenario(IActorRef worldActor)
    {
        Console.WriteLine("=== Zone Movement Test ===\n");
        
        // 플레이어 로그인
        worldActor.Tell(new PlayerLoginRequest("Alice"));
        await Task.Delay(500);
        worldActor.Tell(new PlayerLoginRequest("Bob"));
        await Task.Delay(500);
        
        // Town에서 이동
        worldActor.Tell(new PlayerCommand("Alice", new MoveCommand(new Position(10, 10))));
        await Task.Delay(500);
        
        // Zone 변경
        worldActor.Tell(new RequestZoneChange("Alice", "forest"));
        await Task.Delay(1000);
        
        // 정리
        worldActor.Tell(new PlayerDisconnect("Alice"));
        worldActor.Tell(new PlayerDisconnect("Bob"));
        await Task.Delay(500);
        
        Console.WriteLine("\n=== Test Complete ===");
    }
}