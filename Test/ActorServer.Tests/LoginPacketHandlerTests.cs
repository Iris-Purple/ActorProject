using Xunit;
using Xunit.Abstractions;
using ActorServer.Network.Handlers;
using ActorServer.Network.Protocol;
using ActorServer.Messages;
using ActorServer.Tests.TestHelpers;
using FluentAssertions;
using Akka.Actor;
using Akka.TestKit;
using Common.Database;

namespace ActorServer.Tests.Handlers;

[Collection("ActorTests")]
public class LoginPacketHandlerTests : AkkaTestKitBase
{
    public LoginPacketHandlerTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task LoginHandler_Should_Success_With_Valid_Token()
    {
        using var scope = Test();

        // Arrange
        var handler = new LoginPacketHandler();
        var clientProbe = CreateTestProbe("clientProbe");
        var worldProbe = CreateTestProbe("worldProbe");
        
        // TestProbe를 world라는 이름으로 등록
        Sys.ActorOf(Props.Create(() => new ForwardActor(worldProbe)), "world");
        
        // TestClientContext 생성
        var context = new TestClientContext(clientProbe.Ref);
        
        // AccountDatabase에 테스트 계정 생성
        var accountDb = AccountDatabase.Instance;
        var loginResult = await accountDb.ProcessLoginAsync("test_player_1");
        loginResult.Success.Should().BeTrue();
        
        var loginPacket = new LoginPacket
        {
            PlayerId = loginResult.PlayerId,
            Token = loginResult.Token!
        };
        
        scope.LogInfo($"Testing login for PlayerId: {loginResult.PlayerId}");

        // Act
        var worldSelection = Sys.ActorSelection("/user/world");
        await handler.HandlePacket(loginPacket, context, worldSelection);

        // Assert
        // 1. WorldActor에 EnterWorld 메시지 전송 확인
        var enterWorldMsg = worldProbe.ExpectMsg<EnterWorld>(TimeSpan.FromSeconds(1));
        enterWorldMsg.PlayerId.Should().Be(loginResult.PlayerId);
        enterWorldMsg.ClientConnection.Should().Be(clientProbe.Ref);
        
        // 2. 클라이언트에 성공 응답 확인
        context.SentPackets.Should().HaveCount(1);
        var response = context.SentPackets[0] as LoginResponsePacket;
        response.Should().NotBeNull();
        response!.Success.Should().BeTrue();
        response.PlayerId.Should().Be(loginResult.PlayerId);
        
        // 3. Context PlayerId 설정 확인
        context.PlayerId.Should().Be(loginResult.PlayerId);
        
        scope.LogSuccess($"Login successful for PlayerId: {loginResult.PlayerId}");
    }

    [Fact]
    public async Task LoginHandler_Should_Fail_With_Invalid_Token()
    {
        using var scope = Test();

        // Arrange
        var handler = new LoginPacketHandler();
        var clientProbe = CreateTestProbe("clientProbe");
        var worldProbe = CreateTestProbe("worldProbe");
        
        Sys.ActorOf(Props.Create(() => new ForwardActor(worldProbe)), "world");
        
        var context = new TestClientContext(clientProbe.Ref);
        
        var loginPacket = new LoginPacket
        {
            PlayerId = 9999,
            Token = "invalid_token_12345"
        };
        
        scope.LogInfo("Testing login with invalid token");

        // Act
        var worldSelection = Sys.ActorSelection("/user/world");
        await handler.HandlePacket(loginPacket, context, worldSelection);

        // Assert
        // 1. WorldActor에 메시지 전송 안됨
        worldProbe.ExpectNoMsg(TimeSpan.FromMilliseconds(500));
        
        // 2. 클라이언트에 실패 응답
        context.SentPackets.Should().HaveCount(1);
        var response = context.SentPackets[0] as LoginResponsePacket;
        response.Should().NotBeNull();
        response!.Success.Should().BeFalse();
        response.Message.Should().Contain("Invalid or expired token");
        
        // 3. Context PlayerId 설정 안됨
        context.PlayerId.Should().Be(0);
        
        scope.LogSuccess("Login correctly failed with invalid token");
    }

    private class ForwardActor : ReceiveActor
    {
        public ForwardActor(TestProbe probe)
        {
            ReceiveAny(msg => probe.Forward(msg));
        }
    }

    private class TestClientContext : ClientConnectionContext
    {
        public List<Packet> SentPackets { get; } = new();
        
        public TestClientContext(IActorRef clientProbe) 
            : base(clientProbe, clientProbe, null!)
        {
        }
        
        public override void SendPacket<T>(T packet)
        {
            SentPackets.Add(packet);
            Console.WriteLine($"[TestContext] Packet sent: {packet.GetType().Name}");
        }
    }
}