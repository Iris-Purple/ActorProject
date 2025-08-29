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
        Receive<ClientDisconnected>(HandleClientDisconnected);

        // called HandleClientDisconnected
        Receive<Terminated>(HandleTerminated);
    }

    private void HandleEnterWorld(EnterWorld msg)
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
    private void HandleClientDisconnected(ClientDisconnected msg)
    {
        var playerId = msg.PlayerId;

        if (players.TryGetValue(playerId, out var playerActor))
        {
            Console.WriteLine($"[World] Client disconnected, stopping Player {playerId}");
            Context.Stop(playerActor);  // PlayerActor 종료 → Terminated 메시지 발생
        }
    }

    private void HandleTerminated(Terminated terminated)
    {
        // terminated.ActorRef == 종료된 playerActor
        var playerEntry = players.FirstOrDefault(kvp => kvp.Value.Equals(terminated.ActorRef));

        if (playerEntry.Value != null)
        {
            var playerId = playerEntry.Key;
            players.Remove(playerId);  // ← Dictionary에서 제거

            // ZoneActor에게도 알림
            zoneActor.Tell(new PlayerDisconnected(playerId));

            Console.WriteLine($"[World] Player {playerId} terminated and cleaned up");
        }
    }
}
