using ActorServer.Messages;
using ActorServer.Network.Protocol;
using Akka.Actor;

namespace ActorServer.Network.Handlers;

/// <summary>
/// Zone 변경 패킷 핸들러
/// </summary>
public class ZonePacketHandler : IPacketHandler
{
    public async Task HandlePacket(Packet packet, ClientConnectionContext context, ActorSelection worldActor)
    {
        if (packet is not ZonePacket zonePacket)
            return;
    }
}