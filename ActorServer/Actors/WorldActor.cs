using ActorServer.Messages;
using Akka.Actor;

namespace ActorServer.Actors;

public class WorldActor : ReceiveActor
{
    private Dictionary<long, IActorRef> players = new();
    private IActorRef zoneActor;

    public WorldActor()
    {
        zoneActor = Context.ActorOf(
            Props.Create<ZoneActor>()
                .WithSupervisorStrategy(CreateZoneSupervisionStrategy()), 
            "zone");

        Receive<EnterWorld>(HandleEnterWorld);
        Receive<PlayerMove>(HandlePlayerMove);
        Receive<ClientDisconnected>(HandleClientDisconnected);

        // called HandleClientDisconnected
        Receive<Terminated>(HandleTerminated);
    }
    protected override SupervisorStrategy SupervisorStrategy()
    {
        return new OneForOneStrategy(
            maxNrOfRetries: 10,          // 최대 10회 재시도
            withinTimeRange: TimeSpan.FromMinutes(1),  // 1분 내에
            localOnlyDecider: exception =>
            {
                // 로깅 추가
                Console.WriteLine($"[WorldActor-Supervisor] Exception caught: {exception.GetType().Name}");
                Console.WriteLine($"[WorldActor-Supervisor] Message: {exception.Message}");

                switch (exception)
                {
                    // PlayerActor 관련 예외들 - 항상 Restart
                    case ActorKilledException _:
                        Console.WriteLine("[WorldActor-Supervisor] ActorKilledException → Stop");
                        return Directive.Stop;

                    case ActorInitializationException _:
                        Console.WriteLine("[WorldActor-Supervisor] ActorInitializationException → Stop");
                        return Directive.Stop;

                    // 기본값: Restart (PlayerActor는 모든 예외에서 재시작)
                    default:
                        Console.WriteLine("[WorldActor-Supervisor] Default exception → Restart");
                        return Directive.Restart;
                }
            });
    }

    // ZoneActor는 예외가 발생해도 계속 진행 (Resume)
    private SupervisorStrategy CreateZoneSupervisionStrategy()
    {
        return new OneForOneStrategy(
            maxNrOfRetries: -1,  // 무제한 재시도
            withinTimeRange: TimeSpan.FromMinutes(1),
            localOnlyDecider: exception =>
            {
                // ZoneActor 예외 처리 로깅
                Console.WriteLine($"[ZoneActor-Supervisor] Exception caught: {exception.GetType().Name}");
                Console.WriteLine($"[ZoneActor-Supervisor] Message: {exception.Message}");

                switch (exception)
                {
                    // ActorKilledException은 Stop (강제 종료 명령)
                    case ActorKilledException _:
                        Console.WriteLine("[ZoneActor-Supervisor] ActorKilledException → Stop");
                        return Directive.Stop;

                    case ActorInitializationException _:
                        Console.WriteLine("[ZoneActor-Supervisor] ActorInitializationException → Stop");
                        return Directive.Stop;

                    // 나머지 모든 예외는 Resume (계속 진행)
                    default:
                        Console.WriteLine("[ZoneActor-Supervisor] Exception handled → Resume");
                        return Directive.Resume;
                }
            });
    }

    private void HandleEnterWorld(EnterWorld msg)
    {
        var playerId = msg.PlayerId;
        if (players.ContainsKey(playerId))
        {
            Console.WriteLine($"[World] Player (ID:{playerId}) reconnecting...");

            // 재접속 시 클라이언트 연결 업데이트
            if (msg.ClientConnection != null)
            {
                players[playerId].Tell(new SetClientConnection(msg.ClientConnection));
                Console.WriteLine($"[World] Updated client connection for Player {playerId}");
            }
            return;
        }

        var actorName = $"player-{playerId}";
        var playerActor = Context.ActorOf(
            Props.Create<PlayerActor>(playerId), actorName);

        Context.Watch(playerActor);
        players[playerId] = playerActor;

        // 클라이언트 연결 설정
        if (msg.ClientConnection != null)
        {
            playerActor.Tell(new SetClientConnection(msg.ClientConnection));
            Console.WriteLine($"[World] Set client connection for new Player {playerId}");
        }

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
