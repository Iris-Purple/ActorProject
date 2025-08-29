using ActorServer.Messages;
using ActorServer.Network.Protocol;
using Akka.Actor;

namespace ActorServer.Network.Handlers;

/// <summary>
/// 이동 패킷 핸들러
/// </summary>
public class MovePacketHandler : IPacketHandler
{
    public async Task HandlePacket(Packet packet, ClientConnectionContext context, ActorSelection worldActor)
    {
        if (packet is not MovePacket movePacket)
            return;
    }
}