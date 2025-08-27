using Akka.Actor;
using ActorServer.Messages;
using Common.Database;
using ActorServer.Zone;

namespace ActorServer.Actors;

/// <summary>
/// PlayerActor - 플레이어 상태 관리 및 클라이언트 응답 담당
/// ZoneActor로부터 검증된 명령만 받아 처리
/// </summary>
public class PlayerActor : ReceiveActor
{
    // === 플레이어 식별자 ===
    private readonly long playerId;

    // === 플레이어 상태 ===
    private Position currentPosition = new Position(0, 0);
    private ZoneId currentZoneId = ZoneId.Empty;
    private IActorRef? clientConnection;

    // === 데이터베이스 ===
    private readonly PlayerDatabase _db = PlayerDatabase.Instance;

    public PlayerActor(long playerId)
    {
        this.playerId = playerId;
        
        LoadFromDatabase();
        Console.WriteLine($"[PlayerActor-{playerId}] Actor created, Zone: {currentZoneId}");

        // ===== 클라이언트 연결 =====
        Receive<SetClientConnection>(HandleSetClientConnection);

        // ===== 채팅 관련 메시지 =====
        Receive<SendChat>(HandleSendChat);
        Receive<ChatBroadcast>(HandleChatBroadcast);
        
        Receive<GetPlayerInfo>(HandleGetPlayerInfo);

        // ===== 에러 메시지 =====
        Receive<ErrorMessage>(HandleError);
    }

    /// <summary>
    /// 검증된 채팅 전송 (ZoneActor가 브로드캐스트)
    /// </summary>
    private void HandleSendChat(SendChat msg)
    {
        Console.WriteLine($"[PlayerActor-{playerId}] Chat confirmed: {msg.Message}");
        clientConnection?.Tell(new ChatToClient("You", msg.Message));
    }

    private void HandleChatBroadcast(ChatBroadcast msg)
    {
        if (msg.PlayerId != playerId) // 다른 플레이어 채팅만
        {
            Console.WriteLine($"[PlayerActor-{playerId}] Received chat from {msg.PlayerId}");
            clientConnection?.Tell(new ChatToClient($"Player-{msg.PlayerId}", msg.Message));
        }
    }

    /// <summary>
    /// 플레이어 정보 조회
    /// </summary>
    private void HandleGetPlayerInfo(GetPlayerInfo msg)
    {
        var info = new PlayerInfoResponse(
            PlayerId: playerId,
            Position: currentPosition,
            ZoneId: currentZoneId,
            IsOnline: clientConnection != null
        );
        
        Sender.Tell(info);
    }

    private void HandleSetClientConnection(SetClientConnection msg)
    {
        clientConnection = msg.ClientActor;
        Console.WriteLine($"[PlayerActor-{playerId}] Client connection established");
        
        // 연결 시 현재 상태 전송
        clientConnection.Tell(new ChatToClient("System", 
            $"Connected as Player-{playerId} in {currentZoneId} Position: ({currentPosition.X:F1}, {currentPosition.Y:F1})"));
    }

    private void LoadFromDatabase()
    {
        var data = _db.LoadPlayerData(playerId);
        if (data != null)
        {
            currentPosition = new Position(data.X, data.Y);
            currentZoneId = (ZoneId)data.ZoneId;
            Console.WriteLine($"[PlayerActor-{playerId}] Loaded from DB - Zone: {currentZoneId}");
        }
        else
        {
            // 신규 플레이어
            currentPosition = new Position(0, 0);
            currentZoneId = 0;
            Console.WriteLine($"[PlayerActor-{playerId}] New player initialized");
        }
    }
    private void HandleError(ErrorMessage err)
    {
        Console.WriteLine($"ERROR [PlayerActor-{playerId}]: {err}");
        clientConnection?.Tell(err);
    }

    private void SaveToDatabase()
    {
        _db.SavePlayer(playerId, currentPosition.X, currentPosition.Y, (int)currentZoneId);
        Console.WriteLine($"[PlayerActor-{playerId}] Saved to database");
    }


    protected override void PreStart()
    {
        Console.WriteLine($"[PlayerActor-{playerId}] Starting...");
        base.PreStart();
    }

    protected override void PostStop()
    {
        // 종료 시 상태 저장
        SaveToDatabase();
        
        Console.WriteLine($"[PlayerActor-{playerId}] Stopped.");
        
        // 클라이언트에 연결 종료 알림
        clientConnection?.Tell(new ChatToClient("System", "Player disconnected"));
        
        base.PostStop();
    }

    protected override void PreRestart(Exception reason, object message)
    {
        Console.WriteLine($"[PlayerActor-{playerId}] PRE-RESTART: {reason.GetType().Name}");
        Console.WriteLine($"  Caused by message: {message?.GetType().Name ?? "null"}");
        
        // 재시작 전 상태 저장
        SaveToDatabase();
        
        base.PreRestart(reason, message);
    }

    protected override void PostRestart(Exception reason)
    {
        Console.WriteLine($"[PlayerActor-{playerId}] POST-RESTART completed");
        
        // 재시작 후 상태 복구
        LoadFromDatabase();
        
        // 클라이언트에 재연결 알림
        clientConnection?.Tell(new ChatToClient("System", "Connection restored"));
        
        base.PostRestart(reason);
    }
}