using ActorServer.Messages;
using ActorServer.Network.Protocol;
using Akka.Actor;

namespace ActorServer.Network.Handlers;

/// <summary>
/// 채팅 패킷 핸들러
/// </summary>
public class ChatPacketHandler : IPacketHandler
{
    public Task HandlePacket(Packet packet, ClientConnectionContext context, ActorSelection worldActor)
    {
        if (packet is not SayPacket sayPacket)
            return Task.CompletedTask;

        if (context.PlayerId == 0)
        {
            context.SendPacket(new ErrorMessagePacket { Error = "Not logged in" });
            return Task.CompletedTask;
        }
        /*
        context.TellWorldActor(new PlayerCommand(context.PlayerId,
            new ChatMessage(context.PlayerId, sayPacket.Message)));
        */

        Console.WriteLine($"[ChatHandler] {sayPacket.Message}");

        return Task.CompletedTask;
    }
}