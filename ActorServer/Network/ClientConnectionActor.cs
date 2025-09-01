using Akka.Actor;
using Akka.IO;
using System.Text;
using ActorServer.Messages;
using ActorServer.Network.Protocol;
using ActorServer.Network.Handlers;

namespace ActorServer.Network
{
    public class ClientConnectionActor : ReceiveActor
    {
        private long _playerId;
        private readonly IActorRef _connection;
        private StringBuilder _receiveBuffer = new();

        // 패킷 핸들러 매니저와 컨텍스트
        private readonly ClientConnectionContext _context;
        private readonly PacketHandlerManager _handlerManager;

        public ClientConnectionActor(IActorRef connection)
        {
            _connection = connection;

            // 컨텍스트와 핸들러 매니저 초기화
            _context = new ClientConnectionContext(connection, Self, Context);
            _handlerManager = new PacketHandlerManager(_context);

            // ===== 중요: 메시지 핸들러 등록 순서 =====
            // 1. 구체적인 타입 먼저
            // 2. 조건부 핸들러
            // 3. ReceiveAny는 마지막
            
            RegisterHandlers();
        }
        
        private void RegisterHandlers()
        {
            // 1. TCP 메시지 수신 (가장 구체적인 타입)
            Receive<Tcp.Received>(HandleTcpReceived);
            
            // 2. 연결 종료
            Receive<Tcp.ConnectionClosed>(HandleConnectionClosed);
            
            // 3. 커맨드 실패
            Receive<Tcp.CommandFailed>(HandleCommandFailed);
            
            // 4. SendPacketToClient 메시지 처리 (제네릭 타입)
            // 조건부 핸들러로 처리
            Receive<object>(HandleGenericMessage, IsGenericPacketMessage);
            
            // 5. 나머지 모든 메시지 (마지막!)
            ReceiveAny(HandleUnknownMessage);
        }
        
        private void HandleTcpReceived(Tcp.Received data)
        {
            var message = Encoding.UTF8.GetString(data.Data.ToArray());
            Console.WriteLine($"[Client] Raw received: {message}");
            ProcessJsonPackets(message);
        }
        
        private void HandleConnectionClosed(Tcp.ConnectionClosed closed)
        {
            // Tcp.ConnectionClosed의 실제 타입 확인
            var closeReason = closed switch
            {
                Tcp.Aborted _ => "Connection aborted",
                Tcp.ConfirmedClosed _ => "Connection closed gracefully",
                Tcp.Closed _ => "Connection closed",
                Tcp.ErrorClosed error => $"Connection error: {error.Cause}",
                Tcp.PeerClosed _ => "Connection closed by peer",
                _ => $"Connection closed ({closed.GetType().Name})"
            };
            
            Console.WriteLine($"[Client] {closeReason}");
            Context.Stop(Self);
        }
        
        private void HandleCommandFailed(Tcp.CommandFailed failed)
        {
            Console.WriteLine($"[Client] Command failed: {failed.Cmd}");
        }
        
        private bool IsGenericPacketMessage(object msg)
        {
            var msgType = msg.GetType();
            return msgType.IsGenericType && 
                   msgType.GetGenericTypeDefinition() == typeof(SendPacketToClient<>);
        }
        
        private void HandleGenericMessage(object msg)
        {
            var msgType = msg.GetType();
            
            // SendPacketToClient<T> 처리
            if (msgType.IsGenericType && 
                msgType.GetGenericTypeDefinition() == typeof(SendPacketToClient<>))
            {
                // 리플렉션으로 Packet 추출
                var packet = msgType.GetProperty("Packet")?.GetValue(msg) as Packet;
                if (packet != null)
                {
                    SendPacket(packet);
                    Console.WriteLine($"[Client] Sent {packet.GetType().Name} to client");
                }
                else
                {
                    Console.WriteLine($"[Client] Failed to extract packet from {msgType.Name}");
                }
            }
        }
        
        private void HandleUnknownMessage(object msg)
        {
            Console.WriteLine($"[Client] Received unexpected message: {msg.GetType().Name}");
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
            var lastLine = lines[^1];
            
            // 마지막 라인이 완전한 JSON이 아니면 버퍼에 보관
            if (!string.IsNullOrWhiteSpace(lastLine))
            {
                // JSON 끝 문자 '}'가 있는지 확인
                if (!lastLine.TrimEnd().EndsWith("}"))
                {
                    _receiveBuffer.Append(lastLine);
                }
                else
                {
                    // 완전한 JSON이면 처리
                    ProcessSingleJsonPacket(lastLine.Trim());
                }
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
                    Console.WriteLine("[Client] Failed to deserialize packet");
                    SendPacket(new ErrorMessagePacket
                    {
                        Error = "Invalid packet format",
                        Details = "Failed to deserialize JSON"
                    });
                    return;
                }

                Console.WriteLine($"[Client] Packet type: {packet.Type}");

                // 핸들러 매니저에 위임
                await _handlerManager.HandlePacket(packet);

                // 컨텍스트 동기화
                _playerId = _context.PlayerId;
                
                Console.WriteLine($"[Client] Packet processed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Client] Error processing packet: {ex.Message}");
                Console.WriteLine($"[Client] Stack trace: {ex.StackTrace}");
                
                SendPacket(new ErrorMessagePacket
                {
                    Error = "Error processing packet",
                    Details = ex.Message
                });
            }
        }

        // === 헬퍼 메서드 ===
        private void SendPacket<T>(T packet) where T : Packet
        {
            try
            {
                var bytes = PacketSerializer.SerializeToBytes(packet);
                _connection.Tell(Tcp.Write.Create(bytes));
                Console.WriteLine($"[Client] Packet sent: {packet.GetType().Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Client] Failed to send packet: {ex.Message}");
            }
        }

        // === Actor 라이프사이클 ===

        protected override void PreStart()
        {
            Console.WriteLine("[ClientConnection] New client connection established");
            base.PreStart();
        }

        protected override void PostStop()
        {
            if (_playerId > 0)
            {
                var worldActor = Context.System.ActorSelection("/user/world");
                worldActor.Tell(new ClientDisconnected(_playerId));
                Console.WriteLine($"[ClientConnection] Notified WorldActor about Player {_playerId} disconnection");
            }
            
            Console.WriteLine($"[ClientConnection] Connection closed (PlayerId: {_playerId})");
            base.PostStop();
        }
        
        protected override void PreRestart(Exception reason, object message)
        {
            Console.WriteLine($"[ClientConnection] Restarting due to: {reason.Message}");
            base.PreRestart(reason, message);
        }
    }
}