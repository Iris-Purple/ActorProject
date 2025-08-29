using ActorServer.Messages;
using ActorServer.Network.Protocol;
using ActorServer.Zone;
using Akka.Actor;

namespace ActorServer.Network.Handlers;

/// <summary>
/// Zone 변경 패킷 핸들러
/// </summary>
public class ChangeZonePacketHandler : IPacketHandler
{
    public Task HandlePacket(Packet packet, ClientConnectionContext context, ActorSelection worldActor)
    {
        if (packet is not ZonePacket zonePacket)
            return Task.CompletedTask;

        if (context.PlayerId == 0)
        {
            context.SendPacket(new ErrorMessagePacket
            {
                Error = "Not logged in",
                Details = "Please login first before changing zones"
            });
            return Task.CompletedTask;
        }

        var msg = new ChangeZoneRequest(
            PlayerActor: null!,  // WorldActor가 내부에서 설정
            PlayerId: context.PlayerId,
            TargetZoneId: (ZoneId)zonePacket.ZoneId
        );
        worldActor.Tell(msg);
        Console.WriteLine($"[ChangeZoneHandler] Player {context.PlayerId} ChangeZoneId: {(ZoneId)zonePacket.ZoneId}");

        return Task.CompletedTask;
    }
}