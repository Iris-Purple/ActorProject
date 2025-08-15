// ActorServer/Actors/PlayerActor.cs
using Akka.Actor;
using ActorServer.Messages;
using ActorServer.Exceptions;

namespace ActorServer.Actors
{
    public class PlayerActor : ReceiveActor
    {
        // 플레이어 상태
        private string playerName;
        private Position currentPosition;
        private IActorRef? currentZone;
        private IActorRef? clientConnection;
        private string currentZoneId = "town";

        // 다른 플레이어들의 위치 저장
        private Dictionary<string, Position> otherPlayers = new();

        // 에러 처리를 위한 상태
        private int errorCount = 0;
        private DateTime lastErrorTime = DateTime.Now;

        public PlayerActor(string name)
        {
            playerName = name;
            currentPosition = new Position(0, 0);

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

            // ===== 복구 관련 메시지 =====
            Receive<PlayerReconnecting>(HandleReconnecting);
            Receive<PlayerReconnected>(HandleReconnected);

            // ===== 테스트용 메시지 =====
            Receive<TestNullCommand>(HandleTestNullCommand);
            Receive<SimulateCrash>(HandleSimulateCrash);
            Receive<SimulateOutOfMemory>(HandleSimulateOutOfMemory);
        }

        #region 이동 관련 핸들러

        private void HandleMove(MoveCommand cmd)
        {
            try
            {
                // 위치 유효성 검증
                ValidatePosition(cmd.NewPosition);

                // 이동 거리 검증
                var distance = CalculateDistance(currentPosition, cmd.NewPosition);
                if (distance > 100)
                {
                    throw new GameLogicException($"Move distance too large: {distance:F2}");
                }

                // 위치 업데이트
                var oldPosition = currentPosition;
                currentPosition = cmd.NewPosition;

                Console.WriteLine($"[{playerName}] Moving from ({oldPosition.X:F1}, {oldPosition.Y:F1}) to ({currentPosition.X:F1}, {currentPosition.Y:F1})");

                // Zone에 알림
                currentZone?.Tell(new PlayerMovement(Self, currentPosition));

                // 클라이언트에게 확인 메시지
                clientConnection?.Tell(new ChatToClient("System", $"Moved to ({currentPosition.X:F1}, {currentPosition.Y:F1})"));
            }
            catch (GameLogicException ex)
            {
                // 게임 로직 에러는 로그만 남기고 계속 진행 (Resume)
                LogError("Move", ex);
                clientConnection?.Tell(new ChatToClient("System", $"Move failed: {ex.Message}"));
            }
            catch (Exception ex)
            {
                // 예상치 못한 에러
                LogError("Move", ex);
                throw new TemporaryGameException($"Failed to process move: {ex.Message}", ex);
            }
        }

        private void ValidatePosition(Position pos)
        {
            // NaN 체크
            if (float.IsNaN(pos.X) || float.IsNaN(pos.Y))
            {
                throw new GameLogicException("Position contains NaN values");
            }

            // Infinity 체크
            if (float.IsInfinity(pos.X) || float.IsInfinity(pos.Y))
            {
                throw new GameLogicException("Position contains Infinity values");
            }

            // 맵 경계 체크
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
                Console.WriteLine($"[{playerName}] Zone actor set");
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
                Console.WriteLine($"[{playerName}] Successfully changed zone to: {currentZoneId}");

                // 이전 Zone의 플레이어 목록 클리어
                otherPlayers.Clear();

                // 클라이언트에게 알림
                clientConnection?.Tell(msg);
            }
            else
            {
                Console.WriteLine($"[{playerName}] Failed to change zone: {msg.Message}");
                clientConnection?.Tell(msg);
            }
        }

        private void HandleZoneEntered(ZoneEntered msg)
        {
            // 새 Zone에 진입했을 때
            currentZoneId = msg.ZoneInfo.ZoneId;
            currentPosition = msg.ZoneInfo.SpawnPoint;

            Console.WriteLine($"[{playerName}] Entered zone: {msg.ZoneInfo.Name}");
            Console.WriteLine($"[{playerName}] Zone type: {msg.ZoneInfo.Type}");
            Console.WriteLine($"[{playerName}] Spawned at ({currentPosition.X}, {currentPosition.Y})");
            Console.WriteLine($"[{playerName}] Level range: {msg.ZoneInfo.MinLevel} - {msg.ZoneInfo.MaxLevel}");

            // 클라이언트에게 Zone 정보 전달
            clientConnection?.Tell(new ChatToClient("System",
                $"Entered {msg.ZoneInfo.Name} at ({currentPosition.X}, {currentPosition.Y})"));
        }

        private void HandleZoneFull(ZoneFull msg)
        {
            Console.WriteLine($"[{playerName}] Cannot enter zone {msg.ZoneId}: Zone is full!");
            clientConnection?.Tell(new ChatToClient("System", $"Zone {msg.ZoneId} is full!"));
        }

        private void HandleOutOfBoundWarning(OutOfBoundWarning msg)
        {
            Console.WriteLine($"[{playerName}] WARNING: You are moving out of zone {msg.ZoneId} boundaries!");
            clientConnection?.Tell(new ChatToClient("System", "Warning: Out of zone boundaries!"));
        }

        private void HandleZoneInfo(CurrentPlayersInZone msg)
        {
            Console.WriteLine($"[{playerName}] Received zone info. Players in zone:");
            foreach (var player in msg.Players)
            {
                if (player.Name != playerName)
                {
                    otherPlayers[player.Name] = player.Position;
                    Console.WriteLine($" - {player.Name} at ({player.Position.X}, {player.Position.Y})");
                }
            }
        }

        #endregion

        #region 다른 플레이어 관련 핸들러

        private void HandleOtherPlayerMove(PlayerPositionUpdate msg)
        {
            otherPlayers[msg.PlayerName] = msg.NewPosition;
            Console.WriteLine($"[{playerName}] {msg.PlayerName} moved to ({msg.NewPosition.X}, {msg.NewPosition.Y})");

            // 옵션: 클라이언트에게 실시간 위치 업데이트 전달
            // clientConnection?.Tell(msg);
        }

        private void HandlePlayerJoined(PlayerJoinedZone msg)
        {
            otherPlayers[msg.Player.Name] = msg.Player.Position;
            Console.WriteLine($"[{playerName}] New player joined: {msg.Player.Name}");

            clientConnection?.Tell(new ChatToClient("System", $"{msg.Player.Name} joined the zone"));
        }

        private void HandlePlayerLeft(PlayerLeftZone msg)
        {
            if (otherPlayers.Remove(msg.PlayerName))
            {
                Console.WriteLine($"[{playerName}] Player {msg.PlayerName} left the zone");
                clientConnection?.Tell(new ChatToClient("System", $"{msg.PlayerName} left the zone"));
            }
        }

        #endregion

        #region 채팅 관련 핸들러

        private void HandleChatBroadcast(ChatBroadcast msg)
        {
            if (msg.PlayerName == playerName)
            {
                Console.WriteLine($"[{playerName}] You said: {msg.Message}");
            }
            else
            {
                Console.WriteLine($"[{playerName}] {msg.PlayerName} says: {msg.Message}");
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
            Console.WriteLine($"[{playerName}] Client connection established");

            // 연결 성공 메시지
            clientConnection.Tell(new ChatToClient("System", $"Connected to player {playerName}"));
        }

        #endregion

        #region 복구 관련 핸들러

        private void HandleReconnecting(PlayerReconnecting msg)
        {
            Console.WriteLine($"[{playerName}] Preparing for reconnection...");
            SavePlayerState();
        }

        private void HandleReconnected(PlayerReconnected msg)
        {
            Console.WriteLine($"[{playerName}] Reconnected successfully");
            RestorePlayerState();
        }

        private void SavePlayerState()
        {
            // 실제로는 데이터베이스나 파일에 저장
            Console.WriteLine($"[{playerName}] Saving state: Zone={currentZoneId}, Pos=({currentPosition.X}, {currentPosition.Y})");
        }

        private void RestorePlayerState()
        {
            // 실제로는 데이터베이스나 파일에서 로드
            Console.WriteLine($"[{playerName}] Restoring state: Zone={currentZoneId}, Pos=({currentPosition.X}, {currentPosition.Y})");
        }

        #endregion

        #region 테스트 핸들러

        private void HandleTestNullCommand(TestNullCommand msg)
        {
            Console.WriteLine($"[{playerName}] Received TestNullCommand");
            throw new ArgumentNullException("command", "Test: Received null command");
        }

        private void HandleSimulateCrash(SimulateCrash msg)
        {
            Console.WriteLine($"[{playerName}] Simulating crash: {msg.Reason}");
            throw new TemporaryGameException($"Simulated crash: {msg.Reason}");
        }

        private void HandleSimulateOutOfMemory(SimulateOutOfMemory msg)
        {
            Console.WriteLine($"[{playerName}] Simulating out of memory");
            throw new CriticalGameException("Simulated out of memory");
        }

        #endregion

        #region 유틸리티 메서드

        private void LogError(string operation, Exception ex)
        {
            errorCount++;
            var timeSinceLastError = DateTime.Now - lastErrorTime;
            lastErrorTime = DateTime.Now;

            Console.WriteLine($"[{playerName}] ERROR in {operation}: {ex.GetType().Name} - {ex.Message}");
            Console.WriteLine($"  Error count: {errorCount}, Time since last error: {timeSinceLastError.TotalSeconds:F1}s");
        }

        #endregion

        #region Actor 라이프사이클

        protected override void PreStart()
        {
            Console.WriteLine($"[{playerName}] Actor starting...");
            base.PreStart();
        }

        protected override void PostStop()
        {
            Console.WriteLine($"[{playerName}] Actor stopped. Total errors: {errorCount}");

            // 클라이언트에게 연결 종료 알림
            clientConnection?.Tell(new ChatToClient("System", "Player actor stopped"));

            // Zone에서 제거
            currentZone?.Tell(new RemovePlayerFromZone(Self));

            base.PostStop();
        }

        protected override void PreRestart(Exception reason, object message)
        {
            Console.WriteLine($"[{playerName}] PRE-RESTART due to: {reason.GetType().Name} - {reason.Message}");
            Console.WriteLine($"  Message that caused error: {message?.GetType().Name ?? "null"}");

            // Zone에 재접속 예정 알림
            currentZone?.Tell(new PlayerReconnecting(playerName));

            // 중요한 상태 저장
            SavePlayerState();

            base.PreRestart(reason, message);
        }

        protected override void PostRestart(Exception reason)
        {
            Console.WriteLine($"[{playerName}] POST-RESTART completed");

            // 상태 복구
            RestorePlayerState();

            // Zone에 복귀 알림
            currentZone?.Tell(new PlayerReconnected(playerName, currentPosition));

            base.PostRestart(reason);
        }

        #endregion
    }

    #region 테스트용 메시지 타입

    public record TestNullCommand();
    public record SimulateCrash(string Reason);
    public record SimulateOutOfMemory();

    #endregion
}