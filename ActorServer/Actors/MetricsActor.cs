using Akka.Actor;
using ActorServer.Messages;

namespace ActorServer.Actors;

public class MetricsActor : ReceiveActor
{
    private int _totalPlayers = 0;
    private int _totalMessages = 0;
    private DateTime _startTime;
    public MetricsActor()
    {
        _startTime = DateTime.Now;
        Receive<PlayerLoggedIn>(msg =>
        {
            _totalPlayers++;
            Console.WriteLine($"[Metrics] Player logged in. Total: {_totalPlayers}");
        });
        Receive<PlayerLoggedOut>(msg =>
        {
            _totalPlayers--;
            Console.WriteLine($"[Metrics] Player logged out. Total: {_totalPlayers}");
        });
        Receive<MessageProcessed>(msg =>
        {
            _totalMessages++;
        });
        Receive<GetMetrics>(msg =>
        {
            var uptime = DateTime.Now - _startTime;
            Console.WriteLine($"\n=== Server Metrics ===");
            Console.WriteLine($"Uptime: {uptime:hh\\:mm\\:ss}");
            Console.WriteLine($"Total Players: {_totalPlayers}");
            Console.WriteLine($"Messages Processed: {_totalMessages}");

            Console.WriteLine($"\n--- Players per Zone ---");
            Console.WriteLine($"===================\n");
        });
    }
}
