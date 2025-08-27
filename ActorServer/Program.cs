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
            Props.Create(() => new TcpServerActor(9999)),
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
            Console.WriteLine("0. Exit");
            Console.WriteLine("=================================");
            Console.Write("Select option: ");
            
            var input = Console.ReadLine();
            
            switch (input)
            {
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
}