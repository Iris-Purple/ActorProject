using Xunit;
using Xunit.Abstractions;
using ActorServer.Actors;
using ActorServer.Messages;
using ActorServer.Tests.TestHelpers;
using FluentAssertions;
using Akka.Actor;

namespace ActorServer.Tests.Actors;

[Collection("ActorTests")]
public class PlayerActorTests : AkkaTestKitBase
{
    public PlayerActorTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void PlayerActor_Should_Create_With_PlayerId()
    {
        using var test = Test();
        // Arrange
        var playerId = 1001L;
        // Act
        var playerActor = Sys.ActorOf(
            Props.Create<PlayerActor>(playerId),
            $"test-player-{playerId}"
        );

        // Assert
        playerActor.Should().NotBeNull();
        test.LogSuccess("PlayerActor created successfully");
    }
    [Fact]
    public void PlayerActor_Should_Move_Successfully_With_Client_Feedback()
    {
        using var test = SlowTest();
        // Arrange
        var playerId = 1001L;
        // Act
        var playerActor = Sys.ActorOf(
            Props.Create<PlayerActor>(playerId),
            $"test-player-{playerId}"
        );
        var clientProbe = CreateTestProbe();

        playerActor.Tell(new SetClientConnection(clientProbe));
        clientProbe.ExpectMsg<ChatToClient>(TimeSpan.FromMilliseconds(100));

        playerActor.Tell(new MoveCommand(new Position(10, 10)));
        clientProbe.ExpectMsg<ChatToClient>(
            msg => msg.Message.Contains("Moved to"),
            TimeSpan.FromMilliseconds(100),
            "Player should send movement confirmation to client");

        test.LogSuccess("PlayerActor move successfully");
    }
    [Fact]
    public void PlayerActor_Should_Reject_Invalid_NaN_Position()
    {
        using var test = SlowTest();
        // Arrange
        var playerId = 1001L;
        // Act
        var playerActor = Sys.ActorOf(
            Props.Create<PlayerActor>(playerId),
            $"test-player-{playerId}"
        );
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

        test.LogSuccess("PlayerActor invalid Move Position");
    }
}