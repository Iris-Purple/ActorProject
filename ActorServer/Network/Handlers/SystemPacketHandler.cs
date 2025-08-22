using ActorServer.Network.Protocol;
using Akka.IO;

namespace ActorServer.Network.Handlers;

/// <summary>
/// 시스템 명령 패킷 핸들러 (Status, Help, Quit)
/// </summary>
public class SystemPacketHandler : IPacketHandler
{
    public Task HandlePacket(Packet packet, ClientConnectionContext context)
    {
        switch (packet)
        {
            case StatusPacket:
                HandleStatus(context);
                break;
                
            case HelpPacket:
                HandleHelp(context);
                break;
                
            case QuitPacket:
                HandleQuit(context);
                break;
        }
        return Task.CompletedTask;
    }
    
    private void HandleStatus(ClientConnectionContext context)
    {
        if (context.PlayerName == null)
        {
            context.SendPacket(new ErrorMessagePacket { Error = "Not logged in" });
            return;
        }
        
        // 상태 정보 응답
        context.SendPacket(new StatusInfoPacket
        {
            PlayerName = context.PlayerName,
            CurrentZone = "Unknown", // TODO: 실제 Zone 정보 가져오기
            Position = new StatusInfoPacket.PositionInfo { X = 0, Y = 0 }
        });
        
        context.SendPacket(new SystemMessagePacket
        {
            Message = "Use /zone <name> to change zones\nUse /move <x> <y> to move",
            Level = "info"
        });
        
        Console.WriteLine($"[SystemHandler] Status requested by {context.PlayerName}");
    }
    
    private void HandleHelp(ClientConnectionContext context)
    {
        // 도움말 정보 응답
        var helpPacket = new HelpInfoPacket
        {
            Commands = new List<HelpInfoPacket.CommandInfo>
            {
                new() { Command = "/login <name>", Description = "Login with your name" },
                new() { Command = "/move <x> <y>", Description = "Move to position" },
                new() { Command = "/say <message>", Description = "Send chat message" },
                new() { Command = "/zone <name>", Description = "Change zone (town/forest/dungeon-1)" },
                new() { Command = "/status", Description = "Show your status" },
                new() { Command = "/help", Description = "Show this help" },
                new() { Command = "/quit", Description = "Disconnect" }
            }
        };
        
        context.SendPacket(helpPacket);
        Console.WriteLine($"[SystemHandler] Help requested");
    }
    
    private void HandleQuit(ClientConnectionContext context)
    {
        context.SendPacket(new SystemMessagePacket 
        { 
            Message = "Goodbye!",
            Level = "info"
        });
        
        // TCP 연결 종료
        context.Connection.Tell(Tcp.Close.Instance, context.Self);
        
        Console.WriteLine($"[SystemHandler] {context.PlayerName ?? "Unknown"} disconnecting");
    }
}