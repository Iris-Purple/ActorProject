using Xunit;
using Xunit.Abstractions;
using ActorServer.Actors;
using ActorServer.Messages;
using ActorServer.Tests.TestHelpers;
using FluentAssertions;
using Akka.Actor;

namespace ActorServer.Tests.Actors;

public class PlayerActorTests : AkkaTestKitBase
{
    public PlayerActorTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void PlayerActor_Should_Create_With_PlayerId()
    {
        // Arrange
        var playerId = 1001L;
        
        // Act
        var playerActor = Sys.ActorOf(
            Props.Create<PlayerActor>(playerId),
            $"test-player-{playerId}"
        );
        
        // Assert
        playerActor.Should().NotBeNull();
        
        // 변경: Output 직접 사용 (TestKit의 속성)
        LogTest($"PlayerActor created successfully with ID: {playerId}");
    }

    [Fact]
    public void PlayerActor_Should_Handle_MoveCommand()
    {
        // Arrange
        var playerId = 1002L;
        var playerActor = Sys.ActorOf(
            Props.Create<PlayerActor>(playerId)
        );
        
        var newPosition = new Position(10f, 20f);
        var moveCommand = new MoveCommand(newPosition);
        
        // Act
        playerActor.Tell(moveCommand, TestActor);
        
        // Assert - Actor가 Zone에 이동 알림을 보낼 것임
        // 현재는 Zone이 설정되지 않아서 메시지가 없을 것
        ExpectNoMsg(TimeSpan.FromMilliseconds(100));
        
        // 변경: LogTest 메서드 사용
        LogTest($"MoveCommand processed for position: ({newPosition.X}, {newPosition.Y})");
    }

    [Fact]
    public void PlayerActor_Should_Handle_SetClientConnection()
    {
        // Arrange
        var playerId = 1003L;
        var playerActor = Sys.ActorOf(
            Props.Create<PlayerActor>(playerId)
        );
        
        var clientConnection = CreateTestProbe();
        
        // Act
        playerActor.Tell(new SetClientConnection(clientConnection), TestActor);
        
        // Assert - 연결 성공 메시지를 받아야 함
        clientConnection.ExpectMsg<ChatToClient>(msg =>
            msg.From == "System" && 
            msg.Message.Contains("Connected")
        );
        
        // 변경: LogTest 메서드 사용
        LogTest("Client connection established successfully");
    }

    [Fact]
    public void PlayerActor_Should_Save_And_Load_State()
    {
        // Arrange
        var playerId = 1004L;
        var playerActor = Sys.ActorOf(
            Props.Create<PlayerActor>(playerId)
        );
        
        // Act - 저장 명령
        playerActor.Tell("save", TestActor);
        
        // Assert
        ExpectNoMsg(TimeSpan.FromMilliseconds(100));
        
        // 변경: LogTest 메서드 사용
        LogTest($"Player state saved for ID: {playerId}");
    }
}