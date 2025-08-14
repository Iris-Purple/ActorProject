using Akka.Actor;
using Akka.IO;
using ActorServer.Network;
using System.Text;
using ActorServer.Messages;
using System.Diagnostics;
using ActorServer.Actors;

namespace ActorServer.Network;

public class ClientConnectionActor : ReceiveActor
{
    private readonly IActorRef _connection;
    private readonly IActorRef _worldActor;
    private string? _playerName;
    private IActorRef? _playerActor;
    public ClientConnectionActor(IActorRef connection, IActorRef worldActor)
    {
        _connection = connection;
        _worldActor = worldActor;

        SendToClient("Welcome! Please login with: /login <name>");
        Receive<Tcp.Received>(data =>
        {
            var message = Encoding.UTF8.GetString(data.Data.ToArray()).Trim();
            Console.WriteLine($"[Client] Received: {message}");
            ProcessCommand(message);
        });
        Receive<Tcp.ConnectionClosed>(closed =>
        {
            Console.WriteLine($"[Client] Connection closed");
            if (_playerName != null)
            {
                _worldActor.Tell(new PlayerDisconnect(_playerName));
            }
            Context.Stop(Self);
        });
        Receive<Tcp.CommandFailed>(failed =>
        {
            Console.WriteLine($"[Client] Command failed: {failed.Cmd}");
        });
        Receive<ChatToClient>(msg =>
        {
            if (msg.From == _playerName)
                SendToClient($"You: {msg.Message}");
            else
                SendToClient($"[{msg.From}]: {msg.Message}");
        });
    }

    private void ProcessCommand(string message)
    {
        if (message.StartsWith("/login"))
        {
            _playerName = message.Substring(7);
            _worldActor.Tell(new PlayerLoginRequest(_playerName));
            _worldActor.Tell(new RegisterClientConnection(_playerName, Self));
            SendToClient($"Logged in as {_playerName}");
        }
        else if (message == "/help")
        {
            SendToClient("Commands:\n/login <name> - Login\n/move <x> <y> - Move\n/quit - Disconnect");
        }
        else if (message.StartsWith("/move ") && _playerName != null)
        {
            var parts = message.Substring(6).Split(' ');
            if (parts.Length == 2 && float.TryParse(parts[0], out var x) && float.TryParse(parts[1], out var y))
            {
                _worldActor.Tell(new PlayerCommand(_playerName, new MoveCommand(new Position(x, y))));
                SendToClient($"Moving to ({x}, {y})");
            }
            else
            {
                SendToClient("Usage: /move <x> <y>");
            }
        }
        else if (message.StartsWith("/say ") && _playerName != null)
        {
            var chat = message.Substring(5);
            _worldActor.Tell(new PlayerCommand(_playerName,
                new ChatMessage(_playerName, chat)));
            SendToClient($"You: {chat}");
        }
        else if (message.StartsWith("/zone ") && _playerName != null)
        {
            var targetZone = message.Substring(6);
            _worldActor.Tell(new RequestZoneChange(_playerName, targetZone));
            SendToClient($"Moving to zone: {targetZone}");
        }
        else if (message == "/quit")
        {
            SendToClient("Goodbye!");
            _connection.Tell(Tcp.Close.Instance);
        }
        else
        {
            SendToClient($"Unknown command: {message}. Type /help");
        }
    }
    private void SendToClient(string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message + "\n");
        _connection.Tell(Tcp.Write.Create(ByteString.FromBytes(bytes)));
    }
}