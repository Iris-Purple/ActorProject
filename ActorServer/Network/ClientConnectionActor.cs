using Akka.Actor;
using Akka.IO;
using System.Text;
using ActorServer.Messages;
using ActorServer.Actors;
using ActorServer.Network.Protocol;
using ActorServer.Network.Handlers; // 추가: 핸들러 네임스페이스

namespace ActorServer.Network
{
    public class ClientConnectionActor : ReceiveActor
    {
        private readonly IActorRef _connection;
        private readonly IActorRef _worldActor;
        private string? _playerName;
        private StringBuilder _receiveBuffer = new();
        
        // 추가: 패킷 핸들러 매니저와 컨텍스트
        private readonly ClientConnectionContext _context;
        private readonly PacketHandlerManager _handlerManager;
        
        public ClientConnectionActor(IActorRef connection, IActorRef worldActor)
        {
            _connection = connection;
            _worldActor = worldActor;
            
            // 추가: 컨텍스트와 핸들러 매니저 초기화
            _context = new ClientConnectionContext(connection, worldActor, Self);
            _handlerManager = new PacketHandlerManager(_context);

            // 웰컴 메시지
            SendPacket(new SystemMessagePacket 
            { 
                Message = "Welcome! Please login with: /login <name>",
                Level = "info"
            });
            
            // TCP 메시지 수신
            Receive<Tcp.Received>(data =>
            {
                var message = Encoding.UTF8.GetString(data.Data.ToArray());
                Console.WriteLine($"[Client] Raw received: {message}");
                
                ProcessJsonPackets(message);
            });
            
            // 연결 종료
            Receive<Tcp.ConnectionClosed>(closed =>
            {
                Console.WriteLine($"[Client] Connection closed");
                if (_context.PlayerName != null)
                {
                    _worldActor.Tell(new PlayerDisconnect(_context.PlayerName));
                }
                Context.Stop(Self);
            });
            
            // Actor 간 메시지 처리
            Receive<ChatToClient>(HandleChatToClient);
            Receive<LoginFailed>(HandleLoginFailed);
            Receive<ChangeZoneResponse>(HandleChangeZoneResponse);
        }

        /// <summary>
        /// JSON 패킷들을 처리
        /// </summary>
        private void ProcessJsonPackets(string data)
        {
            _receiveBuffer.Append(data);
            var fullData = _receiveBuffer.ToString();
            
            // \n으로 구분된 각 JSON 패킷 처리
            var lines = fullData.Split('\n');
            
            for (int i = 0; i < lines.Length - 1; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                {
                    ProcessSingleJsonPacket(lines[i].Trim());
                }
            }
            
            // 마지막 부분이 완전한 패킷이 아니면 버퍼에 유지
            _receiveBuffer.Clear();
            if (!string.IsNullOrWhiteSpace(lines[^1]) && !lines[^1].Contains('}'))
            {
                _receiveBuffer.Append(lines[^1]);
            }
        }

        /// <summary>
        /// 단일 JSON 패킷 처리
        /// </summary>
        private async void ProcessSingleJsonPacket(string json)
        {
            try
            {
                Console.WriteLine($"[Client] Processing JSON packet: {json}");
                
                var packet = PacketSerializer.Deserialize(json);
                if (packet == null)
                {
                    SendPacket(new ErrorMessagePacket 
                    { 
                        Error = "Invalid packet format",
                        Details = "Failed to deserialize JSON"
                    });
                    return;
                }
                
                // 변경: 핸들러 매니저에 위임
                await _handlerManager.HandlePacket(packet);
                
                // 컨텍스트 동기화
                _playerName = _context.PlayerName;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Client] Error processing packet: {ex.Message}");
                SendPacket(new ErrorMessagePacket 
                { 
                    Error = "Error processing packet",
                    Details = ex.Message
                });
            }
        }

        // === Actor 메시지 핸들러 ===
        
        private void HandleChatToClient(ChatToClient msg)
        {
            var packet = new ChatMessagePacket
            {
                PlayerName = msg.From,
                Message = msg.Message,
                IsSelf = (msg.From == _playerName)
            };
            SendPacket(packet);
        }
        
        private void HandleLoginFailed(LoginFailed msg)
        {
            var packet = new LoginResponsePacket
            {
                Success = false,
                Message = $"Login failed: {msg.Reason}"
            };
            SendPacket(packet);
        }
        
        private void HandleChangeZoneResponse(ChangeZoneResponse msg)
        {
            var packet = new ZoneChangeResponsePacket
            {
                Success = msg.Success,
                ZoneName = msg.Success ? msg.Message : "",
                Message = msg.Success 
                    ? $"Successfully moved to zone: {msg.Message}"
                    : $"Failed to change zone: {msg.Message}"
            };
            SendPacket(packet);
        }

        // === 헬퍼 메서드 ===
        
        private void SendPacket<T>(T packet) where T : Packet
        {
            var bytes = PacketSerializer.SerializeToBytes(packet);
            _connection.Tell(Tcp.Write.Create(bytes));
        }
        
        // === Actor 라이프사이클 ===
        
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