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
        using var test = SlowTest();
        // Arrange
        var playerId = 1001L;
        test.LogInfo($"Creating PlayerActor with ID: {playerId}");
        
        // Act
        var playerActor = Sys.ActorOf(
            Props.Create<PlayerActor>(playerId),
            $"test-player-{playerId}"
        );
        
        // Assert
        playerActor.Should().NotBeNull();
        test.LogSuccess("PlayerActor created successfully");
    }
}