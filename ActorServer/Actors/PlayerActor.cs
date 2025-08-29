using Akka.Actor;
using ActorServer.Messages;
using Common.Database;
using ActorServer.Zone;
using ActorServer.Network.Protocol;

namespace ActorServer.Actors;

/// <summary>
/// PlayerActor - 플레이어 상태 관리 및 클라이언트 응답 담당
/// ZoneActor로부터 검증된 명령만 받아 처리
/// </summary>
public class PlayerActor : ReceiveActor
{
    // === 플레이어 식별자 ===
    private readonly long playerId;
    private IActorRef? clientConnection;

    public PlayerActor(long playerId)
    {
        this.playerId = playerId;
        Console.WriteLine($"[PlayerActor-{playerId}] Actor created");

        // ===== 클라이언트 연결 =====
        Receive<SetClientConnection>(HandleSetClientConnection);

        // zone -> player
        Receive<ZoneChanged>(HandleZoneChanged);
        Receive<PlayerMoved>(HandlePlayerMoved);
        // ===== 채팅 관련 메시지 =====
        Receive<ChatMessage>(HandleSendChat);
        // ===== 에러 메시지 =====
        Receive<ErrorMessage>(HandleError);
    }

    /// <summary>
    /// 검증된 채팅 전송 (ZoneActor가 브로드캐스트)
    /// </summary>
    private void HandleSendChat(ChatMessage msg)
    {
        Console.WriteLine($"[PlayerActor-{playerId}] Chat confirmed: {msg.Message}");
        var packet = new ChatMessagePacket
        {
            PlayerName = $"Player_{playerId}",
            Message = msg.Message,
            IsSelf = true
        };
        
        SendToClient(packet);
    }

    private void HandleZoneChanged(ZoneChanged msg)
    {
        Console.WriteLine($"[PlayerActor-{playerId}] ZoneChanged: {msg}");
        var packet = new ZoneChangeResponsePacket
        {
            Success = true,
            ZoneName = msg.NewZoneId.ToString(),
            Message = $"Entered {msg.NewZoneId} at ({msg.SpawnPosition.X}, {msg.SpawnPosition.Y})"
        };
        
        SendToClient(packet);
    }
    private void HandlePlayerMoved(PlayerMoved msg)
    {
        Console.WriteLine($"[PlayerActor-{playerId}] Move confirmed to ({msg.X}, {msg.Y})");
        var packet = new MoveNotificationPacket
        {
            PlayerId = msg.PlayerId,
            X = msg.X,
            Y = msg.Y,
            IsSelf = true
        };
        
        SendToClient(packet);
    }

    private void HandleSetClientConnection(SetClientConnection msg)
    {
        clientConnection = msg.ClientActor;
        Console.WriteLine($"[PlayerActor-{playerId}] Client connection established");
    }

    private void HandleError(ErrorMessage err)
    {
        Console.WriteLine($"ERROR [PlayerActor-{playerId}]: {err}");
        var packet = new ErrorMessagePacket
        {
            Error = err.Type.ToString(),
            Details = err.Reason
        };
        
        SendToClient(packet);
    }

    private void SendToClient<T>(T packet) where T : Packet
    {
        if (clientConnection == null)
        {
            Console.WriteLine($"[PlayerActor-{playerId}] No client connection");
            return;
        }

        // 변경: ClientConnectionActor의 SendPacket 활용
        clientConnection.Tell(new SendPacketToClient<T>(packet));
        Console.WriteLine($"[PlayerActor-{playerId}] Requested {packet.GetType().Name} send");
    }

    protected override void PreStart()
    {
        Console.WriteLine($"[PlayerActor-{playerId}] Starting...");
        base.PreStart();
    }

    protected override void PostStop()
    {
        Console.WriteLine($"[PlayerActor-{playerId}] Stopped.");

        base.PostStop();
    }

    protected override void PreRestart(Exception reason, object message)
    {
        Console.WriteLine($"[PlayerActor-{playerId}] PRE-RESTART: {reason.GetType().Name}");
        Console.WriteLine($"  Caused by message: {message?.GetType().Name ?? "null"}");

        base.PreRestart(reason, message);
    }

    protected override void PostRestart(Exception reason)
    {
        Console.WriteLine($"[PlayerActor-{playerId}] POST-RESTART completed");

        base.PostRestart(reason);
    }
}