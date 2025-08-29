using ActorServer.Messages;
using ActorServer.Network.Protocol;
using Akka.Actor;

namespace ActorServer.Network.Handlers;

/// <summary>
/// 이동 패킷 핸들러
/// </summary>
public class MovePacketHandler : IPacketHandler
{
    public Task HandlePacket(Packet packet, ClientConnectionContext context, ActorSelection worldActor)
    {
        if (packet is not MovePacket movePacket)
            return Task.CompletedTask;

        if (context.PlayerId == 0)
        {
            context.SendPacket(new ErrorMessagePacket { Error = "Not logged in" });
            return Task.CompletedTask;
        }
        // WorldActor에 이동 명령 전달
        /*
        context.TellWorldActor(new PlayerCommand(context.PlayerId,
            new MoveCommand(new Position(movePacket.X, movePacket.Y))));
        */

        // 이동 확인 응답
        context.SendPacket(new MoveNotificationPacket
        {
            PlayerId = context.PlayerId,
            X = movePacket.X,
            Y = movePacket.Y,
            IsSelf = true
        });

        Console.WriteLine($"[MoveHandler] {context.PlayerId} moving to ({movePacket.X}, {movePacket.Y})");

        return Task.CompletedTask;
    }
}