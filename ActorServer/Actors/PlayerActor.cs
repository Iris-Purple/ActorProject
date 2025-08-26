using Akka.Actor;
using ActorServer.Messages;
using ActorServer.Exceptions;
using Common.Database;

namespace ActorServer.Actors;

public class PlayerActor : ReceiveActor
{
    // 플레이어 식별자
    private readonly long playerId;          // 고유 ID (Primary Key)

    // 플레이어 상태
    private Position currentPosition = new Position(0, 0);  // 기본값으로 초기화
    private IActorRef? clientConnection;
    private IActorRef? zoneManager;
    private string currentZoneId = "town";

    // 다른 플레이어들의 정보 저장 (ID 기반)
    private Dictionary<long, Position> otherPlayers = new();

    // 에러 처리를 위한 상태
    private int errorCount = 0;
    private DateTime lastErrorTime = DateTime.Now;
    private readonly PlayerDatabase _db = PlayerDatabase.Instance;

    public PlayerActor(long playerId)
    {
        this.playerId = playerId;

        LoadFromDatabase();
        Console.WriteLine($"[PlayerActor] Creating actor for (ID:{playerId})");

        Receive<SetZoneManager>(HandleSetZoneManager);
        Receive<ZoneMessageResult>(HandleZoneMessageResult);

        // ===== 일반 게임 메시지 핸들러 =====
        Receive<MoveCommand>(HandleMove);
        Receive<SetClientConnection>(HandleSetClientConnection);

        // ===== Zone 관련 메시지 =====
        Receive<CurrentPlayersInZone>(HandleZoneInfo);
        Receive<PlayerPositionUpdate>(HandleOtherPlayerMove);
        Receive<PlayerJoinedZone>(HandlePlayerJoined);
        Receive<PlayerLeftZone>(HandlePlayerLeft);
        Receive<ChangeZoneResponse>(HandleChangeZoneResponse);
        Receive<ZoneEntered>(HandleZoneEntered);
        Receive<ZoneFull>(HandleZoneFull);
        Receive<OutOfBoundWarning>(HandleOutOfBoundWarning);

        // ===== 채팅 메시지 =====
        Receive<ChatBroadcast>(HandleChatBroadcast);
        Receive<ChatMessage>(HandleSendChat);

        // ===== 테스트용 메시지 =====
        Receive<TestNullCommand>(HandleTestNullCommand);
        Receive<SimulateCrash>(HandleSimulateCrash);
        Receive<SimulateOutOfMemory>(HandleSimulateOutOfMemory);

        Receive<string>(s => s == "save", _ => SaveToDatabase());
    }
    private void LoadFromDatabase()
    {
        var loadPlayerData = _db.LoadPlayerData(playerId);
        if (loadPlayerData != null)
        {
            currentPosition = new Position(loadPlayerData.X, loadPlayerData.Y);
            currentZoneId = loadPlayerData.ZoneId;
            Console.WriteLine($"[PlayerActor] Load player: (ID:{playerId})");
        }
        else
        {
            // new player
            currentPosition = new Position(0, 0);
            currentZoneId = "town";
            Console.WriteLine($"[PlayerActor] New player: (ID:{playerId})");
        }
    }

    private void HandleSetZoneManager(SetZoneManager msg)
    {
        zoneManager = msg.ZoneManager;
        Console.WriteLine($"[Player-{playerId}] ZoneManager set");

        clientConnection?.Tell(new ChatToClient("System", "Connected to Zone Manager"));
    }
    private void HandleZoneMessageResult(ZoneMessageResult msg)
    {
        if (!msg.Success)
        {
            Console.WriteLine($"[Player-{playerId}] Zone operation failed: {msg.ErrorMessage}");
            clientConnection?.Tell(new ChatToClient("System", $"Error: {msg.ErrorMessage}"));
        }
    }

    #region 이동 관련 핸들러
    private void HandleMove(MoveCommand cmd)
    {
        try
        {
            ValidatePosition(cmd.NewPosition);

            var distance = CalculateDistance(currentPosition, cmd.NewPosition);
            if (distance > 100)
            {
                throw new GameLogicException($"Move distance too large: {distance:F2}");
            }
            if (zoneManager == null)
            {
                Console.WriteLine($"[Player-{playerId}] ZoneManager not set, cannot move");
                clientConnection?.Tell(new ChatToClient("System", "Not connected to zone"));
                return;
            }

            var oldPosition = currentPosition;
            currentPosition = cmd.NewPosition;

            // ZoneManager를 통해 이동
            var moveInZone = new PlayerMoveInZone(playerId, currentZoneId, currentPosition);
            zoneManager.Tell(moveInZone, Self);  // Self로 응답 받기

            Console.WriteLine($"[Player-{playerId}] Moving from ({oldPosition.X:F1}, {oldPosition.Y:F1}) to ({currentPosition.X:F1}, {currentPosition.Y:F1})");
            SaveToDatabase();

            // 클라이언트에 알림
            clientConnection?.Tell(new ChatToClient("System",
                $"Moved to ({currentPosition.X:F1}, {currentPosition.Y:F1})"));
        }
        catch (GameLogicException ex)
        {
            LogError("Move", ex);
            clientConnection?.Tell(new ChatToClient("System", $"Move failed: {ex.Message}"));
        }
        catch (Exception ex)
        {
            LogError("Move", ex);
            throw new TemporaryGameException($"Failed to process move: {ex.Message}", ex);
        }
    }

    private void ValidatePosition(Position pos)
    {
        if (float.IsNaN(pos.X) || float.IsNaN(pos.Y))
        {
            throw new GameLogicException("Position contains NaN values");
        }

        if (float.IsInfinity(pos.X) || float.IsInfinity(pos.Y))
        {
            throw new GameLogicException("Position contains Infinity values");
        }

        const float MAP_SIZE = 10000f;
        if (Math.Abs(pos.X) > MAP_SIZE || Math.Abs(pos.Y) > MAP_SIZE)
        {
            throw new GameLogicException($"Position out of map bounds: ({pos.X}, {pos.Y})");
        }
    }

    private float CalculateDistance(Position from, Position to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    #endregion

    #region Zone 관련 핸들러

    private void HandleChangeZoneResponse(ChangeZoneResponse msg)
    {
        if (msg.Success)
        {
            currentZoneId = msg.Message;
            Console.WriteLine($"[Player-{playerId}] Successfully changed zone to: {currentZoneId}");

            SaveToDatabase();
            // 이전 Zone의 플레이어 목록 클리어
            otherPlayers.Clear();
            // 클라이언트에게 알림
            clientConnection?.Tell(msg);
        }
        else
        {
            Console.WriteLine($"[Player-{playerId}] Failed to change zone: {msg.Message}");
            clientConnection?.Tell(msg);
        }
    }

    private void HandleZoneEntered(ZoneEntered msg)
    {
        currentZoneId = msg.ZoneInfo.ZoneId;
        currentPosition = msg.ZoneInfo.SpawnPoint;
        otherPlayers.Clear();  // 이전 Zone의 플레이어 정보 클리어

        Console.WriteLine($"[Player-{playerId}] Entered zone: {msg.ZoneInfo.Name}");
        Console.WriteLine($"[Player-{playerId}] Zone type: {msg.ZoneInfo.Type}");
        Console.WriteLine($"[Player-{playerId}] Spawned at ({currentPosition.X}, {currentPosition.Y})");

        SaveToDatabase();
        // 클라이언트에게 Zone 정보 전달
        clientConnection?.Tell(new ChatToClient("System",
            $"Entered {msg.ZoneInfo.Name} at ({currentPosition.X}, {currentPosition.Y})"));
    }

    private void HandleZoneFull(ZoneFull msg)
    {
        Console.WriteLine($"[Player-{playerId}] Cannot enter zone {msg.ZoneId}: Zone is full!");
        clientConnection?.Tell(new ChatToClient("System", $"Zone {msg.ZoneId} is full!"));
    }

    private void HandleOutOfBoundWarning(OutOfBoundWarning msg)
    {
        Console.WriteLine($"[Player-{playerId}] WARNING: Out of zone {msg.ZoneId} boundaries!");
        clientConnection?.Tell(new ChatToClient("System", "Warning: Out of zone boundaries!"));
    }

    private void HandleZoneInfo(CurrentPlayersInZone msg)
    {
        Console.WriteLine($"[Player-{playerId}] Received zone info. Players in zone:");

        otherPlayers.Clear();
        foreach (var otherPlayer in msg.Players)
        {
            if (playerId != otherPlayer.PlayerId)
            {
                otherPlayers[otherPlayer.PlayerId] = otherPlayer.Position;
                Console.WriteLine($" - {playerId} (ID:{otherPlayer.PlayerId}) at ({otherPlayer.Position.X}, {otherPlayer.Position.Y})");
            }
        }
    }

    #endregion

    #region 다른 플레이어 관련 핸들러

    private void HandleOtherPlayerMove(PlayerPositionUpdate msg)
    {
        otherPlayers[msg.PlayerId] = msg.NewPosition;
        Console.WriteLine($"[Player-{playerId}] Player (ID:{msg.PlayerId}) moved to ({msg.NewPosition.X}, {msg.NewPosition.Y})");
    }

    private void HandlePlayerJoined(PlayerJoinedZone msg)
    {
        var joinedPlayerId = msg.Player.PlayerId;
        otherPlayers[joinedPlayerId] = msg.Player.Position;
        Console.WriteLine($"[Player-{playerId}] New player joined: (ID:{joinedPlayerId})");

        clientConnection?.Tell(new ChatToClient("System", $"ID:{joinedPlayerId} joined the zone"));
    }

    private void HandlePlayerLeft(PlayerLeftZone msg)
    {
        // 플레이어 이름으로 ID 찾아서 제거
        var leftPlayerId = msg.PlayerId;
        if (otherPlayers.Remove(leftPlayerId))
        {
            Console.WriteLine($"[Player-{playerId}] Player (ID:{leftPlayerId}) left the zone");
            clientConnection?.Tell(new ChatToClient("System", $"ID:{leftPlayerId} left the zone"));
        }
    }

    #endregion

    #region 채팅 관련 핸들러

    private void HandleChatBroadcast(ChatBroadcast msg)
    {
        if (msg.PlayerId == playerId)
        {
            Console.WriteLine($"[Player-{playerId}] You said: {msg.Message}");
            clientConnection?.Tell(new ChatToClient("You", msg.Message));
        }
        else
        {
            Console.WriteLine($"[Player-{playerId}] {msg.PlayerId} says: {msg.Message}");
            clientConnection?.Tell(new ChatToClient(msg.PlayerId.ToString(), msg.Message));
        }
    }

    private void HandleSendChat(ChatMessage msg)
    {
        if (zoneManager == null)
        {
            Console.WriteLine($"[Player-{playerId}] ZoneManager not set, cannot chat");
            clientConnection?.Tell(new ChatToClient("System", "Not connected to zone"));
            return;
        }

        var chatInZone = new PlayerChatInZone(playerId, currentZoneId, msg.Message);
        zoneManager.Tell(chatInZone);

        Console.WriteLine($"[Player-{playerId}] Sending chat: {msg.Message}");
    }

    #endregion

    #region 클라이언트 연결 관련

    private void HandleSetClientConnection(SetClientConnection msg)
    {
        clientConnection = msg.ClientActor;
        Console.WriteLine($"[Player-{playerId}] Client connection established");

        // 연결 성공 메시지
        clientConnection.Tell(new ChatToClient("System", $"Connected to player "));
    }

    #endregion

    #region 테스트 핸들러

    private void HandleTestNullCommand(TestNullCommand msg)
    {
        Console.WriteLine($"[Player-{playerId}] Received TestNullCommand");
        throw new ArgumentNullException("command", "Test: Received null command");
    }

    private void HandleSimulateCrash(SimulateCrash msg)
    {
        Console.WriteLine($"[Player-{playerId}] Simulating crash: {msg.Reason}");
        throw new TemporaryGameException($"Simulated crash: {msg.Reason}");
    }

    private void HandleSimulateOutOfMemory(SimulateOutOfMemory msg)
    {
        Console.WriteLine($"[Player-{playerId}] Simulating out of memory");
        throw new CriticalGameException("Simulated out of memory");
    }

    #endregion

    #region 유틸리티 메서드

    private void LogError(string operation, Exception ex)
    {
        errorCount++;
        var timeSinceLastError = DateTime.Now - lastErrorTime;
        lastErrorTime = DateTime.Now;

        Console.WriteLine($"[Player-{playerId}] ERROR in {operation}: {ex.GetType().Name} - {ex.Message}");
        Console.WriteLine($"  Error count: {errorCount}, Time since last error: {timeSinceLastError.TotalSeconds:F1}s");
    }

    #endregion

    #region Actor 라이프사이클

    protected override void PreStart()
    {
        Console.WriteLine($"[Player-{playerId}] Actor starting...");
        base.PreStart();
    }

    protected override void PostStop()
    {
        Console.WriteLine($"[Player-{playerId}] Actor stopped. Total errors: {errorCount}");
        // ZoneManager에 연결 해제 알림
        zoneManager?.Tell(new UnregisterPlayer(playerId));
        // 클라이언트 연결 종료 알림
        clientConnection?.Tell(new ChatToClient("System", "Player actor stopped"));
        
        base.PostStop();
    }

    protected override void PreRestart(Exception reason, object message)
    {
        Console.WriteLine($"[Player-{playerId}] PRE-RESTART due to: {reason.GetType().Name} - {reason.Message}");
        Console.WriteLine($"  Message that caused error: {message?.GetType().Name ?? "null"}");

        base.PreRestart(reason, message);
    }

    protected override void PostRestart(Exception reason)
    {
        Console.WriteLine($"[Player-{playerId}] POST-RESTART completed");

        LoadFromDatabase();
        base.PostRestart(reason);
    }

    #endregion

    private void SaveToDatabase()
    {
        _db.SavePlayer(playerId, currentPosition.X, currentPosition.Y, currentZoneId);
    }
}