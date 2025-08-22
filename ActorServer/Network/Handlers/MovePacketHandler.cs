using ActorServer.Messages;
using ActorServer.Network.Protocol;
using Akka.Actor;

namespace ActorServer.Network.Handlers;

/// <summary>
/// 이동 패킷 핸들러
/// </summary>
public class MovePacketHandler : IPacketHandler
{
    public Task HandlePacket(Packet packet, ClientConnectionContext context)
    {
        if (packet is not MovePacket movePacket)
            return Task.CompletedTask;

        if (context.PlayerName == null)
        {
            context.SendPacket(new ErrorMessagePacket { Error = "Not logged in" });
            return Task.CompletedTask;
        }

        // WorldActor에 이동 명령 전달
        context.TellWorldActor(new PlayerCommand(context.PlayerName,
            new MoveCommand(new Position(movePacket.X, movePacket.Y))));

        // 이동 확인 응답
        context.SendPacket(new MoveNotificationPacket
        {
            PlayerName = context.PlayerName,
            X = movePacket.X,
            Y = movePacket.Y,
            IsSelf = true
        });

        Console.WriteLine($"[MoveHandler] {context.PlayerName} moving to ({movePacket.X}, {movePacket.Y})");

        return Task.CompletedTask;
    }
}