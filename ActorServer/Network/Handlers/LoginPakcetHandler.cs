using ActorServer.Messages;
using ActorServer.Network.Protocol;
using Common.Database;

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

        var isValid = await _accountDb.ValidateTokenAsync(loginPacket.PlayerId, loginPacket.Token);
        if (!isValid)
        {
            context.SendPacket(new LoginResponsePacket
            {
                Success = false,
                Message = "Invalid or expired token. Please login through AuthServer first."
            });

            Console.WriteLine($"[LoginHandler] Token validation failed for {loginPacket.PlayerId}");
            return;
        }

        var accountInfo = await _accountDb.GetAccountByPlayerIdAsync(loginPacket.PlayerId);
        if (accountInfo == null)
        {
            context.SendPacket(new LoginResponsePacket
            {
                Success = false,
                Message = "Account not found"
            });
            return;
        }

        context.PlayerId = loginPacket.PlayerId;
        // WorldActor에 로그인 요청
        context.TellWorldActor(new PlayerLoginRequest(loginPacket.PlayerId));
        context.TellWorldActor(new RegisterClientConnection(context.PlayerId, context.Self));

        // 로그인 성공 응답
        context.SendPacket(new LoginResponsePacket
        {
            Success = true,
            Message = $"Logged in as {loginPacket.PlayerId}",
            PlayerId = context.PlayerId
        });

        // 추가 안내 메시지
        context.SendPacket(new SystemMessagePacket
        {
            Message = "Type /help for available commands",
            Level = "info"
        });

        Console.WriteLine($"[LoginHandler] PlayerId: {loginPacket.PlayerId} logged in successfully");
    }
}