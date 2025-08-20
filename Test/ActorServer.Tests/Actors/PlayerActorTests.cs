using Akka.Actor;
using Akka.TestKit.Xunit2;
using ActorServer.Actors;
using ActorServer.Messages;
using FluentAssertions;
using Xunit;

namespace ActorServer.Tests.Actors;

public class PlayerActorTests : TestKit
{
    [Fact]
    public void PlayerActor_Should_Move_Successfully_With_Client_Feedback()
    {
        var playerActor = Sys.ActorOf(
            Props.Create(() => new PlayerActor(1001L, "TestPlayer")));
        var clientProbe = CreateTestProbe();

        playerActor.Tell(new SetClientConnection(clientProbe));
        clientProbe.ExpectMsg<ChatToClient>(TimeSpan.FromSeconds(1));

        playerActor.Tell(new MoveCommand(new Position(10, 10)));
        clientProbe.ExpectMsg<ChatToClient>(
            msg => msg.Message.Contains("Moved to"),
            TimeSpan.FromSeconds(1),
            "Player should send movement confirmation to client");
    }
    [Fact]
    public void PlayerActor_Should_Notify_Zone_When_Moving()
    {
        var playerActor = Sys.ActorOf(
            Props.Create(() => new PlayerActor(1002L, "TestPlayer2")));
        var zoneProbe = CreateTestProbe();

        playerActor.Tell(new SetZone(zoneProbe));
        playerActor.Tell(new MoveCommand(new Position(20, 30)));

        // Zone 이동 알림을 받는지 확인
        var movement = zoneProbe.ExpectMsg<PlayerMovement>(TimeSpan.FromSeconds(1));
        movement.NewPosition.X.Should().Be(20);
        movement.NewPosition.Y.Should().Be(30);
    }
    [Fact]
    public void PlayerActor_Should_Reject_Invalid_NaN_Position()
    {
        var playerActor = Sys.ActorOf(
            Props.Create(() => new PlayerActor(1003L, "TestPlayer3")));
        var clientProbe = CreateTestProbe();

        playerActor.Tell(new SetClientConnection(clientProbe));
        clientProbe.ExpectMsg<ChatToClient>(TimeSpan.FromSeconds(1));

        playerActor.Tell(new MoveCommand(new Position(float.NaN, float.NaN)));
        clientProbe.ExpectMsg<ChatToClient>(
            msg => msg.Message.Contains("failed") ||
                   msg.Message.Contains("NaN") ||
                   msg.Message.Contains("invalid"),
            TimeSpan.FromSeconds(1),
            "Should reject NaN cooridnates");
    }
    [Fact]
    public void PlayerActor_Should_Reject_Too_Far_Movement()
    {
        var playerActor = Sys.ActorOf(
            Props.Create(() => new PlayerActor(1004L, "TestPlayer4")));
        var clientProbe = CreateTestProbe();

        playerActor.Tell(new SetClientConnection(clientProbe));
        clientProbe.ExpectMsg<ChatToClient>(TimeSpan.FromSeconds(1));

        playerActor.Tell(new MoveCommand(new Position(150, 150)));
        clientProbe.ExpectMsg<ChatToClient>(
            msg => msg.Message.Contains("too large") ||
                   msg.Message.Contains("failed"),
            TimeSpan.FromSeconds(1),
            "Should reject movements over 100 units");
    }
    protected override void AfterAll()
    {
        Shutdown();
    }
}
