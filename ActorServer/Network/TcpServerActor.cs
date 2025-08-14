using System.Net;
using Akka.Actor;
using Akka.IO;

namespace ActorServer.Network;

public class TcpServerActor : ReceiveActor
{
    private readonly IActorRef _worldActor;
    public TcpServerActor(IActorRef worldActor, int port = 9999)
    {
        _worldActor = worldActor;
        Context.System.Tcp().Tell(new Tcp.Bind(Self, new IPEndPoint(IPAddress.Any, port)));
        Receive<Tcp.Bound>(bound =>
        {
            Console.WriteLine($"[TCP] Server listening on port {port}");
        });
        Receive<Tcp.Connected>(connected =>
        {
            Console.WriteLine($"[TCP] Client connected from {connected.RemoteAddress}");
            var connection = Sender;
            var handler = Context.ActorOf(
                Props.Create(() => new ClientConnectionActor(connection, _worldActor)), $"client-{Guid.NewGuid()}");
            connection.Tell(new Tcp.Register(handler));
        });
        Receive<Tcp.CommandFailed>(failed =>
        {
            Console.WriteLine($"[TCP] Command failed: {failed.Cmd}");
        });
    }
}