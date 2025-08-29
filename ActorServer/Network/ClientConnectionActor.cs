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
        private long _playerId;
        private readonly IActorRef _connection;
        private StringBuilder _receiveBuffer = new();

        // 추가: 패킷 핸들러 매니저와 컨텍스트
        private readonly ClientConnectionContext _context;
        private readonly PacketHandlerManager _handlerManager;

        public ClientConnectionActor(IActorRef connection)
        {
            _connection = connection;

            // 추가: 컨텍스트와 핸들러 매니저 초기화
            _context = new ClientConnectionContext(connection, Self, Context);
            _handlerManager = new PacketHandlerManager(_context);

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
                Context.Stop(Self);
            });
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
                _playerId = _context.PlayerId;
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
            if (_playerId > 0)
            {
                var worldActor = Context.System.ActorSelection("/user/world");
                worldActor.Tell(new ClientDisconnected(_playerId));
                Console.WriteLine($"[ClientConnection] Notified WorldActor about Player {_playerId} disconnection");
            }
            Console.WriteLine($"[ClientConnection] Player ID:{_playerId} disconnected");
            base.PostStop();
        }
    }
}