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
            context.SendPacket(new ErrorMessagePacket
            {
                Error = "Not logged in",
                Details = "Please login first before move"
            });
            return Task.CompletedTask;
        }

        var moveMessage = new PlayerMove(
                    PlayerActor: null!,  // WorldActor가 내부에서 설정
                    PlayerId: context.PlayerId,
                    X: movePacket.X,
                    Y: movePacket.Y);
        worldActor.Tell(moveMessage);
        Console.WriteLine($"[MoveHandler] Player {context.PlayerId} move request to ({movePacket.X:F1}, {movePacket.Y:F1})");

        return Task.CompletedTask;
    }
}