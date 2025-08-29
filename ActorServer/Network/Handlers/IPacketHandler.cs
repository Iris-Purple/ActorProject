using ActorServer.Network.Protocol;
using Akka.Actor;
using Akka.IO;

namespace ActorServer.Network.Handlers;

/// <summary>
/// 패킷 핸들러 인터페이스
/// </summary>
public interface IPacketHandler
{
    Task HandlePacket(Packet packet, ClientConnectionContext context, ActorSelection worldActor);
}

/// <summary>
/// 클라이언트 연결 컨텍스트
/// </summary>
public class ClientConnectionContext
{
    public IActorRef Connection { get; }
    public IActorRef Self { get; }
    public long PlayerId { get; set; }
    // 추가: ActorContext 참조 (ActorSelection 사용을 위해)
    public IActorContext ActorContext { get; }


    public ClientConnectionContext(IActorRef connection, IActorRef self, IActorContext actorContext)
    {
        Connection = connection;
        Self = self;
        ActorContext = actorContext;
    }
    public virtual void SendPacket<T>(T packet) where T : Packet
    {
        var bytes = PacketSerializer.SerializeToBytes(packet);
        Connection.Tell(Tcp.Write.Create(bytes), Self);
    }
}

public interface IClientContext
{
    IActorRef Connection { get; }
    IActorRef Self { get; }
    long PlayerId { get; set; }
    IActorContext ActorContext { get; }
    void SendPacket<T>(T packet) where T : Packet;
}