using ActorServer.Messages;
using Akka.Actor;

namespace ActorServer.Actors;

public class WorldActor : ReceiveActor
{
    private Dictionary<long, IActorRef> players = new();
    private IActorRef zoneActor;

    protected override SupervisorStrategy SupervisorStrategy()
    {
        return GameServerStrategies.ForWorldActor();
    }
    public WorldActor()
    {
        zoneActor = Context.ActorOf(Props.Create<ZoneActor>(), "zone");

        Receive<EnterWorld>(HandleEnterWorld);
    }

    private void HandleEnterWorld(EnterWorld msg)
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

            zoneActor.Tell(new ChangeZoneRequest(playerActor, playerId, Zone.ZoneId.Town));
            Console.WriteLine($"[World] Player (ID:{playerId}) logged in");
            Console.WriteLine($"[World] Total online players: {players.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[World] ERROR: Failed to create player {msg.PlayerId}: {ex.Message}");
        }
    }
}
