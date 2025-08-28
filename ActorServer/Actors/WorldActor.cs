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
        Receive<PlayerMove>(HandlePlayerMove);
    }

    private void HandleEnterWorld(EnterWorld msg)
    {
        try
        {
            var playerId = msg.PlayerId;
            if (players.ContainsKey(playerId))
            {
                Console.WriteLine($"[World] Player (ID:{playerId}) reconnecting...");
                return;
            }
            var actorName = $"player-{playerId}";
            var playerActor = Context.ActorOf(
                Props.Create<PlayerActor>(playerId), actorName);

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
    private void HandlePlayerMove(PlayerMove msg)
    {
        if (!players.TryGetValue(msg.PlayerId, out var playerActor))
        {
            Console.WriteLine($"[World] ERROR: Player {msg.PlayerId} not found for move command");
            // Sender가 있으면 에러 전달 (NetworkActor 등)
            if (Sender != ActorRefs.NoSender && Sender != ActorRefs.Nobody)
            {
                Sender.Tell(new ErrorMessage(
                    Type: ERROR_MSG_TYPE.PLAYER_MOVE_ERROR,
                    Reason: $"Player {msg.PlayerId} not registered"
                ));
            }
            return;
        }

        // 항상 WorldActor가 관리하는 PlayerActor 참조를 사용
        var moveWithActor = new PlayerMove(
            PlayerActor: playerActor,  // WorldActor가 찾은 Actor
            PlayerId: msg.PlayerId,
            X: msg.X,
            Y: msg.Y
        );

        // 3. ZoneActor로 전달
        zoneActor.Tell(moveWithActor);
        Console.WriteLine($"[World] Move command forwarded - Player:{msg.PlayerId} to ({msg.X:F1}, {msg.Y:F1})");
    }
}
