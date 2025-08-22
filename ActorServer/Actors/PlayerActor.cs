using Akka.Actor;
using ActorServer.Messages;
using ActorServer.Exceptions;
using Common.Database;

namespace ActorServer.Actors;

public class PlayerActor : ReceiveActor
{
    // 플레이어 식별자
    private readonly long playerId;          // 고유 ID (Primary Key)
    private string playerName;

    // 플레이어 상태
    private Position currentPosition = new Position(0, 0);  // 기본값으로 초기화
    private IActorRef? currentZone;
    private IActorRef? clientConnection;
    private string currentZoneId = "town";

    // 다른 플레이어들의 정보 저장 (ID 기반)
    private Dictionary<long, (string name, Position position)> otherPlayers = new();

    // 에러 처리를 위한 상태
    private int errorCount = 0;
    private DateTime lastErrorTime = DateTime.Now;
    private readonly PlayerDatabase _db = PlayerDatabase.Instance;

    public PlayerActor(long playerId, string playerName)
    {
        this.playerId = playerId;
        this.playerName = playerName;

        LoadFromDatabase();
        Console.WriteLine($"[PlayerActor] Creating actor for {playerName} (ID:{playerId})");

        // ===== 일반 게임 메시지 핸들러 =====
        Receive<MoveCommand>(HandleMove);
        Receive<SetZone>(HandleSetZone);
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
            Console.WriteLine($"[PlayerActor] Load player: {playerName} (ID:{playerId})");
        }
        else
        {
            // new player
            currentPosition = new Position(0, 0);
            currentZoneId = "town";
            Console.WriteLine($"[PlayerActor] New player: {playerName} (ID:{playerId})");
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

            var oldPosition = currentPosition;
            currentPosition = cmd.NewPosition;

            Console.WriteLine($"[Player-{playerId}:{playerName}] Moving from ({oldPosition.X:F1}, {oldPosition.Y:F1}) to ({currentPosition.X:F1}, {currentPosition.Y:F1})");
            SaveToDatabase();

            currentZone?.Tell(new PlayerMovement(Self, currentPosition));
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

    private void HandleSetZone(SetZone msg)
    {
        try
        {
            currentZone = msg.ZoneActor;
            Console.WriteLine($"[Player-{playerId}:{playerName}] Zone actor set");
        }
        catch (Exception ex)
        {
            LogError("SetZone", ex);
            throw new TemporaryGameException($"Failed to set zone: {ex.Message}", ex);
        }
    }

    private void HandleChangeZoneResponse(ChangeZoneResponse msg)
    {
        if (msg.Success)
        {
            currentZoneId = msg.Message;
            Console.WriteLine($"[Player-{playerId}:{playerName}] Successfully changed zone to: {currentZoneId}");

            SaveToDatabase();
            // 이전 Zone의 플레이어 목록 클리어
            otherPlayers.Clear();
            // 클라이언트에게 알림
            clientConnection?.Tell(msg);
        }
        else
        {
            Console.WriteLine($"[Player-{playerId}:{playerName}] Failed to change zone: {msg.Message}");
            clientConnection?.Tell(msg);
        }
    }

    private void HandleZoneEntered(ZoneEntered msg)
    {
        // 새 Zone에 진입했을 때
        currentZoneId = msg.ZoneInfo.ZoneId;
        currentPosition = msg.ZoneInfo.SpawnPoint;

        Console.WriteLine($"[Player-{playerId}:{playerName}] Entered zone: {msg.ZoneInfo.Name}");
        Console.WriteLine($"[Player-{playerId}:{playerName}] Zone type: {msg.ZoneInfo.Type}");
        Console.WriteLine($"[Player-{playerId}:{playerName}] Spawned at ({currentPosition.X}, {currentPosition.Y})");

        // 클라이언트에게 Zone 정보 전달
        clientConnection?.Tell(new ChatToClient("System",
            $"Entered {msg.ZoneInfo.Name} at ({currentPosition.X}, {currentPosition.Y})"));
    }

    private void HandleZoneFull(ZoneFull msg)
    {
        Console.WriteLine($"[Player-{playerId}:{playerName}] Cannot enter zone {msg.ZoneId}: Zone is full!");
        clientConnection?.Tell(new ChatToClient("System", $"Zone {msg.ZoneId} is full!"));
    }

    private void HandleOutOfBoundWarning(OutOfBoundWarning msg)
    {
        Console.WriteLine($"[Player-{playerId}:{playerName}] WARNING: Out of zone {msg.ZoneId} boundaries!");
        clientConnection?.Tell(new ChatToClient("System", "Warning: Out of zone boundaries!"));
    }

    private void HandleZoneInfo(CurrentPlayersInZone msg)
    {
        Console.WriteLine($"[Player-{playerId}:{playerName}] Received zone info. Players in zone:");

        otherPlayers.Clear();
        foreach (var player in msg.Players)
        {
            if (player.Name != playerName)
            {
                // ID 조회 또는 생성
                var otherId = PlayerIdManager.Instance.GetOrCreatePlayerId(player.Name);
                otherPlayers[otherId] = (player.Name, player.Position);
                Console.WriteLine($" - {player.Name} (ID:{otherId}) at ({player.Position.X}, {player.Position.Y})");
            }
        }
    }

    #endregion

    #region 다른 플레이어 관련 핸들러

    private void HandleOtherPlayerMove(PlayerPositionUpdate msg)
    {
        // 플레이어 이름으로 ID 조회
        var otherPlayerId = PlayerIdManager.Instance.GetPlayerId(msg.PlayerName);
        if (!otherPlayerId.HasValue) return;

        otherPlayers[otherPlayerId.Value] = (msg.PlayerName, msg.NewPosition);
        Console.WriteLine($"[Player-{playerId}:{playerName}] Player {msg.PlayerName} (ID:{otherPlayerId.Value}) moved to ({msg.NewPosition.X}, {msg.NewPosition.Y})");
    }

    private void HandlePlayerJoined(PlayerJoinedZone msg)
    {
        // 플레이어 이름으로 ID 조회 또는 생성
        var joinedPlayerId = PlayerIdManager.Instance.GetOrCreatePlayerId(msg.Player.Name);

        otherPlayers[joinedPlayerId] = (msg.Player.Name, msg.Player.Position);
        Console.WriteLine($"[Player-{playerId}:{playerName}] New player joined: {msg.Player.Name} (ID:{joinedPlayerId})");

        clientConnection?.Tell(new ChatToClient("System", $"{msg.Player.Name} joined the zone"));
    }

    private void HandlePlayerLeft(PlayerLeftZone msg)
    {
        // 플레이어 이름으로 ID 찾아서 제거
        var leftPlayerId = PlayerIdManager.Instance.GetPlayerId(msg.PlayerName);
        if (!leftPlayerId.HasValue) return;

        if (otherPlayers.Remove(leftPlayerId.Value))
        {
            Console.WriteLine($"[Player-{playerId}:{playerName}] Player {msg.PlayerName} (ID:{leftPlayerId.Value}) left the zone");
            clientConnection?.Tell(new ChatToClient("System", $"{msg.PlayerName} left the zone"));
        }
    }

    #endregion

    #region 채팅 관련 핸들러

    private void HandleChatBroadcast(ChatBroadcast msg)
    {
        if (msg.PlayerName == playerName)
        {
            Console.WriteLine($"[Player-{playerId}:{playerName}] You said: {msg.Message}");
        }
        else
        {
            Console.WriteLine($"[Player-{playerId}:{playerName}] {msg.PlayerName} says: {msg.Message}");
        }

        // 클라이언트에게 채팅 전달
        clientConnection?.Tell(new ChatToClient(msg.PlayerName, msg.Message));
    }

    private void HandleSendChat(ChatMessage msg)
    {
        // Zone에 채팅 메시지 전달
        currentZone?.Tell(msg);
    }

    #endregion

    #region 클라이언트 연결 관련

    private void HandleSetClientConnection(SetClientConnection msg)
    {
        clientConnection = msg.ClientActor;
        Console.WriteLine($"[Player-{playerId}:{playerName}] Client connection established");

        // 연결 성공 메시지
        clientConnection.Tell(new ChatToClient("System", $"Connected to player {playerName}"));
    }

    #endregion

    #region 테스트 핸들러

    private void HandleTestNullCommand(TestNullCommand msg)
    {
        Console.WriteLine($"[Player-{playerId}:{playerName}] Received TestNullCommand");
        throw new ArgumentNullException("command", "Test: Received null command");
    }

    private void HandleSimulateCrash(SimulateCrash msg)
    {
        Console.WriteLine($"[Player-{playerId}:{playerName}] Simulating crash: {msg.Reason}");
        throw new TemporaryGameException($"Simulated crash: {msg.Reason}");
    }

    private void HandleSimulateOutOfMemory(SimulateOutOfMemory msg)
    {
        Console.WriteLine($"[Player-{playerId}:{playerName}] Simulating out of memory");
        throw new CriticalGameException("Simulated out of memory");
    }

    #endregion

    #region 유틸리티 메서드

    private void LogError(string operation, Exception ex)
    {
        errorCount++;
        var timeSinceLastError = DateTime.Now - lastErrorTime;
        lastErrorTime = DateTime.Now;

        Console.WriteLine($"[Player-{playerId}:{playerName}] ERROR in {operation}: {ex.GetType().Name} - {ex.Message}");
        Console.WriteLine($"  Error count: {errorCount}, Time since last error: {timeSinceLastError.TotalSeconds:F1}s");
    }

    #endregion

    #region Actor 라이프사이클

    protected override void PreStart()
    {
        Console.WriteLine($"[Player-{playerId}:{playerName}] Actor starting...");
        base.PreStart();
    }

    protected override void PostStop()
    {
        Console.WriteLine($"[Player-{playerId}:{playerName}] Actor stopped. Total errors: {errorCount}");
        clientConnection?.Tell(new ChatToClient("System", "Player actor stopped"));
        currentZone?.Tell(new RemovePlayerFromZone(Self, this.playerId));
        base.PostStop();
    }

    protected override void PreRestart(Exception reason, object message)
    {
        Console.WriteLine($"[Player-{playerId}:{playerName}] PRE-RESTART due to: {reason.GetType().Name} - {reason.Message}");
        Console.WriteLine($"  Message that caused error: {message?.GetType().Name ?? "null"}");

        base.PreRestart(reason, message);
    }

    protected override void PostRestart(Exception reason)
    {
        Console.WriteLine($"[Player-{playerId}:{playerName}] POST-RESTART completed");

        LoadFromDatabase();
        base.PostRestart(reason);
    }

    #endregion

    private void SaveToDatabase()
    {
        _db.SavePlayer(playerId, playerName, currentPosition.X, currentPosition.Y, currentZoneId);
    }
}