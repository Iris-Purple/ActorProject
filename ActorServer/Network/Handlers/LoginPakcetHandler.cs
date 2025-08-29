using ActorServer.Actors;
using ActorServer.Messages;
using ActorServer.Network.Protocol;
using Akka.Actor;
using Common.Database;

namespace ActorServer.Network.Handlers;

/// <summary>
/// 로그인 패킷 핸들러
/// </summary>
public class LoginPacketHandler : IPacketHandler
{
    private readonly AccountDatabase _accountDb = AccountDatabase.Instance;

    public async Task HandlePacket(Packet packet, ClientConnectionContext context, ActorSelection worldActor)
    {
        if (packet is not LoginPacket loginPacket)
            return;

        // 1. 토큰 검증 (여기서만!)
        var isValid = await _accountDb.ValidateTokenAsync(loginPacket.PlayerId, loginPacket.Token);
        if (!isValid)
        {
            context.SendPacket(new LoginResponsePacket
            {
                Success = false,
                Message = "Invalid or expired token"
            });
            return;
        }

        // 2. PlayerDatabase에 상태 초기화
        var playerDb = PlayerDatabase.Instance;
        playerDb.GetOrCreatePlayerId(loginPacket.PlayerId);  // player_states 테이블 insert

        // 3. 검증 완료 후 WorldActor에는 PlayerId만 전달 (토큰 불필요)
        context.PlayerId = loginPacket.PlayerId;

        worldActor.Tell(new EnterWorld(
            PlayerId: loginPacket.PlayerId,
            ClientConnection: context.Self  // ClientConnectionActor 참조 전달
        ));

        // 4. 성공 응답
        context.SendPacket(new LoginResponsePacket
        {
            Success = true,
            Message = $"Logged in as PlayerId: {loginPacket.PlayerId}",
            PlayerId = context.PlayerId
        });
    }
}