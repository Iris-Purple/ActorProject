using ActorServer.Network.Protocol;

namespace ActorServer.Network.Handlers;

/// <summary>
/// 패킷 핸들러 매니저 - 패킷 타입별로 적절한 핸들러 호출
/// </summary>
public class PacketHandlerManager
{
    private readonly Dictionary<PacketType, IPacketHandler> _handlers;
    private readonly ClientConnectionContext _context;
    
    public PacketHandlerManager(ClientConnectionContext context)
    {
        _context = context;
        _handlers = new Dictionary<PacketType, IPacketHandler>
        {
            // 각 패킷 타입별 핸들러 등록
            { PacketType.Login, new LoginPacketHandler() },
            { PacketType.Move, new MovePacketHandler() },
            { PacketType.Say, new ChatPacketHandler() },
            { PacketType.Zone, new ZonePacketHandler() },
            { PacketType.Status, new SystemPacketHandler() },
            { PacketType.Help, new SystemPacketHandler() },
            { PacketType.Quit, new SystemPacketHandler() }
        };
    }
    
    /// <summary>
    /// 패킷 처리
    /// </summary>
    public void HandlePacket(Packet packet)
    {
        if (_handlers.TryGetValue(packet.Type, out var handler))
        {
            handler.HandlePacket(packet, _context);
        }
        else
        {
            Console.WriteLine($"[PacketHandlerManager] No handler for packet type: {packet.Type}");
            _context.SendPacket(new ErrorMessagePacket { Error = $"Unknown packet type: {packet.Type}" });
        }
    }
}