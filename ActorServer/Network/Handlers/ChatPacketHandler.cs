using ActorServer.Messages;
using ActorServer.Network.Protocol;

namespace ActorServer.Network.Handlers;

/// <summary>
/// 채팅 패킷 핸들러
/// </summary>
public class ChatPacketHandler : IPacketHandler
{
    public Task HandlePacket(Packet packet, ClientConnectionContext context)
    {
        if (packet is not SayPacket sayPacket)
            return Task.CompletedTask;

        if (context.PlayerName == null)
        {
            context.SendPacket(new ErrorMessagePacket { Error = "Not logged in" });
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(sayPacket.Message))
        {
            context.SendPacket(new ErrorMessagePacket { Error = "Usage: /say <message>" });
            return Task.CompletedTask;
        }

        // WorldActor에 채팅 메시지 전달
        context.TellWorldActor(new PlayerCommand(context.PlayerName,
            new ChatMessage(context.PlayerName, sayPacket.Message)));

        Console.WriteLine($"[ChatHandler] {context.PlayerName}: {sayPacket.Message}");

        return Task.CompletedTask;
    }
}