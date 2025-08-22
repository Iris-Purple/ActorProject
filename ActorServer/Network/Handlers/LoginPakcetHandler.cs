using ActorServer.Messages;
using ActorServer.Network.Protocol;
using Common.Database;
using Akka.Actor;
using System.Threading.Tasks;

namespace ActorServer.Network.Handlers;

/// <summary>
/// 로그인 패킷 핸들러
/// </summary>
public class LoginPacketHandler : IPacketHandler
{
    private readonly AccountDatabase _accountDb = AccountDatabase.Instance;

    public async Task HandlePacket(Packet packet, ClientConnectionContext context)
    {
        if (packet is not LoginPacket loginPacket)
            return;

        if (string.IsNullOrWhiteSpace(loginPacket.PlayerName))
        {
            context.SendPacket(new LoginResponsePacket
            {
                Success = false,
                Message = "Usage: /login <name>"
            });
            return;
        }

        var isValid = await _accountDb.ValidateTokenAsync(loginPacket.PlayerId, loginPacket.Token);
        if (!isValid)
        {
            context.SendPacket(new LoginResponsePacket
            {
                Success = false,
                Message = "Invalid or expired token. Please login through AuthServer first."
            });

            Console.WriteLine($"[LoginHandler] Token validation failed for {loginPacket.PlayerName}");
            return;
        }

        var playerName = loginPacket.PlayerName.Trim();
        context.PlayerName = playerName;
        context.PlayerId = loginPacket.PlayerId;

        // WorldActor에 로그인 요청
        context.TellWorldActor(new PlayerLoginRequest(loginPacket.PlayerId, playerName));
        context.TellWorldActor(new RegisterClientConnection(playerName, context.Self));

        // 로그인 성공 응답
        context.SendPacket(new LoginResponsePacket
        {
            Success = true,
            Message = $"Logged in as {playerName}",
            PlayerName = playerName
        });

        // 추가 안내 메시지
        context.SendPacket(new SystemMessagePacket
        {
            Message = "Type /help for available commands",
            Level = "info"
        });

        Console.WriteLine($"[LoginHandler] Player {playerName} logged in");
    }
}