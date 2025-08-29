using Xunit;
using Xunit.Abstractions;
using ActorServer.Network.Handlers;
using ActorServer.Network.Protocol;
using ActorServer.Messages;
using ActorServer.Tests.TestHelpers;
using FluentAssertions;
using Akka.Actor;
using Akka.TestKit;
using ActorServer.Zone;

namespace ActorServer.Tests.Handlers;

[Collection("ActorTests")]
public class MovePacketHandlerTests : AkkaTestKitBase
{
    public MovePacketHandlerTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// 테스트 1: 로그인한 플레이어의 이동 요청 처리
    /// </summary>
    [Fact]
    public async Task MoveHandler_Should_Forward_Move_Request_To_WorldActor()
    {
        using var scope = Test();

        // Arrange
        var handler = new MovePacketHandler();
        var worldProbe = CreateTestProbe("worldProbe");

        // TestProbe를 world라는 이름으로 등록
        Sys.ActorOf(Props.Create(() => new ForwardActor(worldProbe)), "world");

        // 로그인된 상태의 Context 생성
        var context = new TestClientContext(CreateTestProbe().Ref)
        {
            PlayerId = 1001  // 로그인된 상태
        };

        var movePacket = new MovePacket
        {
            X = 100.5f,
            Y = 200.5f
        };

        scope.LogInfo($"Testing move for Player {context.PlayerId} to ({movePacket.X}, {movePacket.Y})");

        // Act
        var worldSelection = Sys.ActorSelection("/user/world");
        scope.Log("World actor resolved successfully");

        await handler.HandlePacket(movePacket, context, worldSelection);

        // Assert
        // WorldActor에 PlayerMove 메시지 전송 확인
        var moveMsg = worldProbe.ExpectMsg<PlayerMove>(TimeSpan.FromSeconds(2));
        
        moveMsg.Should().NotBeNull();
        moveMsg.PlayerId.Should().Be(1001);
        moveMsg.X.Should().Be(100.5f);
        moveMsg.Y.Should().Be(200.5f);
        moveMsg.PlayerActor.Should().BeNull();  // Handler에서는 null로 전송
        
        // 에러 메시지 없음 확인
        context.SentPackets.Should().BeEmpty();

        scope.LogSuccess($"Move request forwarded successfully");
    }

    /// <summary>
    /// 테스트 2: 로그인하지 않은 플레이어의 이동 요청 차단
    /// </summary>
    [Fact]
    public async Task MoveHandler_Should_Reject_Move_Without_Login()
    {
        using var scope = Test();

        // Arrange
        var handler = new MovePacketHandler();
        var worldProbe = CreateTestProbe("worldProbe");

        Sys.ActorOf(Props.Create(() => new ForwardActor(worldProbe)), "world");

        // 로그인하지 않은 Context (PlayerId = 0)
        var context = new TestClientContext(CreateTestProbe().Ref)
        {
            PlayerId = 0  // 미로그인 상태
        };

        var movePacket = new MovePacket
        {
            X = 50.0f,
            Y = 75.0f
        };

        scope.LogInfo("Testing move without login");

        // Act
        var worldSelection = Sys.ActorSelection("/user/world");
        await handler.HandlePacket(movePacket, context, worldSelection);

        // Assert
        // WorldActor에 메시지 전송 안됨
        worldProbe.ExpectNoMsg(TimeSpan.FromMilliseconds(500));

        // 에러 메시지 전송됨
        context.SentPackets.Should().HaveCount(1);
        var errorPacket = context.SentPackets[0] as ErrorMessagePacket;
        errorPacket.Should().NotBeNull();
        errorPacket!.Error.Should().Be("Not logged in");
        errorPacket.Details.Should().Contain("login first");

        scope.LogSuccess("Move correctly rejected without login");
    }

    /// <summary>
    /// 테스트 3: 잘못된 패킷 타입 처리
    /// </summary>
    [Fact]
    public async Task MoveHandler_Should_Ignore_Wrong_Packet_Type()
    {
        using var scope = Test();

        // Arrange
        var handler = new MovePacketHandler();
        var worldProbe = CreateTestProbe("worldProbe");

        Sys.ActorOf(Props.Create(() => new ForwardActor(worldProbe)), "world");

        var context = new TestClientContext(CreateTestProbe().Ref)
        {
            PlayerId = 1001
        };

        // 잘못된 패킷 타입 (LoginPacket)
        var wrongPacket = new LoginPacket
        {
            PlayerId = 1001,
            Token = "test_token"
        };

        scope.LogInfo("Testing with wrong packet type");

        // Act
        var worldSelection = Sys.ActorSelection("/user/world");
        await handler.HandlePacket(wrongPacket, context, worldSelection);

        // Assert
        // 아무 일도 일어나지 않음
        worldProbe.ExpectNoMsg(TimeSpan.FromMilliseconds(500));
        context.SentPackets.Should().BeEmpty();

        scope.LogSuccess("Wrong packet type correctly ignored");
    }

    // === Helper Classes ===

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