using ActorServer.Messages;
using ActorServer.Network.Protocol;

namespace ActorServer.Network.Handlers;

/// <summary>
/// Zone 변경 패킷 핸들러
/// </summary>
public class ZonePacketHandler : IPacketHandler
{
    public Task HandlePacket(Packet packet, ClientConnectionContext context)
    {
        if (packet is not ZonePacket zonePacket)
            return Task.CompletedTask;
            
        if (string.IsNullOrWhiteSpace(zonePacket.ZoneName))
        {
            context.SendPacket(new ErrorMessagePacket 
            { 
                Error = "Usage: /zone <zone_name>",
                Details = "Available zones: town, forest, dungeon-1"
            });
            return Task.CompletedTask;
        }
        
        var targetZone = zonePacket.ZoneName.Trim();
        
        // WorldActor에 Zone 변경 요청
        context.TellWorldActor(new RequestZoneChange(context.PlayerId, targetZone));
        
        // 요청 확인 메시지
        context.SendPacket(new SystemMessagePacket
        {
            Message = $"Requesting move to zone: {targetZone}",
            Level = "info"
        });
        
        Console.WriteLine($"[ZoneHandler] requesting zone change to {targetZone}");
        return Task.CompletedTask;
    }
}