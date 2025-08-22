using ActorServer.Network.Protocol;
using Akka.Actor;
using Akka.IO;

namespace ActorServer.Network.Handlers;

/// <summary>
/// 패킷 핸들러 인터페이스
/// </summary>
public interface IPacketHandler
{
    Task HandlePacket(Packet packet, ClientConnectionContext context);
}

/// <summary>
/// 클라이언트 연결 컨텍스트
/// </summary>
public class ClientConnectionContext
{
    public IActorRef Connection { get; }
    public IActorRef WorldActor { get; }
    public IActorRef Self { get; }
    public long PlayerId { get; set;}

    public ClientConnectionContext(IActorRef connection, IActorRef worldActor, IActorRef self)
    {
        Connection = connection;
        WorldActor = worldActor;
        Self = self;
    }
    public void SendPacket<T>(T packet) where T : Packet
    {
        var bytes = PacketSerializer.SerializeToBytes(packet);
        Connection.Tell(Tcp.Write.Create(bytes), Self);
    }
    public void TellWorldActor(object message) => WorldActor.Tell(message, Self);
}