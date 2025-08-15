// ActorServer/Network/ClientConnectionActor.cs
using Akka.Actor;
using Akka.IO;
using System.Text;
using ActorServer.Messages;
using ActorServer.Actors;

namespace ActorServer.Network
{
    public class ClientConnectionActor : ReceiveActor
    {
        private readonly IActorRef _connection;
        private readonly IActorRef _worldActor;
        private string? _playerName;
        
        public ClientConnectionActor(IActorRef connection, IActorRef worldActor)
        {
            _connection = connection;
            _worldActor = worldActor;

            SendToClient("Welcome! Please login with: /login <name>");
            
            // TCP 메시지 수신
            Receive<Tcp.Received>(data =>
            {
                var message = Encoding.UTF8.GetString(data.Data.ToArray()).Trim();
                Console.WriteLine($"[Client] Received: {message}");
                ProcessCommand(message);
            });
            
            // 연결 종료
            Receive<Tcp.ConnectionClosed>(closed =>
            {
                Console.WriteLine($"[Client] Connection closed");
                if (_playerName != null)
                {
                    _worldActor.Tell(new PlayerDisconnect(_playerName));
                }
                Context.Stop(Self);
            });
            
            // 명령 실패
            Receive<Tcp.CommandFailed>(failed =>
            {
                Console.WriteLine($"[Client] Command failed: {failed.Cmd}");
            });
            
            // 채팅 메시지 (서버 -> 클라이언트)
            Receive<ChatToClient>(msg =>
            {
                if (msg.From == _playerName)
                    SendToClient($"You: {msg.Message}");
                else
                    SendToClient($"[{msg.From}]: {msg.Message}");
            });
            
            // ⭐ 추가: 로그인 실패 메시지 처리
            Receive<LoginFailed>(msg =>
            {
                SendToClient($"Login failed: {msg.Reason}");
            });
            
            // ⭐ 추가: Zone 변경 응답
            Receive<ChangeZoneResponse>(msg =>
            {
                if (msg.Success)
                    SendToClient($"Successfully moved to zone: {msg.Message}");
                else
                    SendToClient($"Failed to change zone: {msg.Message}");
            });
        }

        private void ProcessCommand(string message)
        {
            try
            {
                if (message.StartsWith("/login "))
                {
                    HandleLogin(message);
                }
                else if (message == "/help")
                {
                    ShowHelp();
                }
                else if (message.StartsWith("/move ") && _playerName != null)
                {
                    HandleMove(message);
                }
                else if (message.StartsWith("/say ") && _playerName != null)
                {
                    HandleChat(message);
                }
                else if (message.StartsWith("/zone ") && _playerName != null)
                {
                    HandleZoneChange(message);
                }
                else if (message == "/status" && _playerName != null)
                {
                    HandleStatus();
                }
                else if (message == "/quit")
                {
                    HandleQuit();
                }
                else
                {
                    SendToClient($"Unknown command: {message}. Type /help");
                }
            }
            catch (Exception ex)
            {
                SendToClient($"Error processing command: {ex.Message}");
                Console.WriteLine($"[Client] Error: {ex}");
            }
        }

        private void HandleLogin(string message)
        {
            var parts = message.Split(' ', 2);
            if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[1]))
            {
                SendToClient("Usage: /login <name>");
                return;
            }
            
            _playerName = parts[1].Trim();
            
            // WorldActor에 로그인 요청
            _worldActor.Tell(new PlayerLoginRequest(_playerName));
            
            // 클라이언트 연결 등록
            _worldActor.Tell(new RegisterClientConnection(_playerName, Self));
            
            SendToClient($"Logged in as {_playerName}");
            SendToClient("Type /help for available commands");
        }

        private void HandleMove(string message)
        {
            var parts = message.Substring(6).Split(' ');
            if (parts.Length == 2 && 
                float.TryParse(parts[0], out var x) && 
                float.TryParse(parts[1], out var y))
            {
                _worldActor.Tell(new PlayerCommand(_playerName!, new MoveCommand(new Position(x, y))));
                SendToClient($"Moving to ({x}, {y})");
            }
            else
            {
                SendToClient("Usage: /move <x> <y>");
            }
        }

        private void HandleChat(string message)
        {
            var chat = message.Substring(5);
            if (string.IsNullOrWhiteSpace(chat))
            {
                SendToClient("Usage: /say <message>");
                return;
            }
            
            _worldActor.Tell(new PlayerCommand(_playerName!, new ChatMessage(_playerName!, chat)));
        }

        private void HandleZoneChange(string message)
        {
            var parts = message.Split(' ', 2);
            if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[1]))
            {
                SendToClient("Usage: /zone <zone_name>");
                SendToClient("Available zones: town, forest, dungeon-1");
                return;
            }
            
            var targetZone = parts[1].Trim();
            _worldActor.Tell(new RequestZoneChange(_playerName!, targetZone));
            SendToClient($"Requesting move to zone: {targetZone}");
        }

        private void HandleStatus()
        {
            SendToClient($"Player: {_playerName}");
            SendToClient("Use /zone <name> to change zones");
            SendToClient("Use /move <x> <y> to move");
        }

        private void HandleQuit()
        {
            SendToClient("Goodbye!");
            _connection.Tell(Tcp.Close.Instance);
        }

        private void ShowHelp()
        {
            var helpText = @"
=== Available Commands ===
/login <name>    - Login with your name
/move <x> <y>    - Move to position
/say <message>   - Send chat message
/zone <name>     - Change zone (town/forest/dungeon-1)
/status          - Show your status
/help            - Show this help
/quit            - Disconnect
==========================";
            
            SendToClient(helpText);
        }

        private void SendToClient(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message + "\n");
            _connection.Tell(Tcp.Write.Create(ByteString.FromBytes(bytes)));
        }
        
        // Actor 라이프사이클
        protected override void PreStart()
        {
            Console.WriteLine("[ClientConnection] New client connection established");
            base.PreStart();
        }

        protected override void PostStop()
        {
            Console.WriteLine($"[ClientConnection] Client {_playerName ?? "unknown"} disconnected");
            base.PostStop();
        }
    }
}