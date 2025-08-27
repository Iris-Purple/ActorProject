using Xunit;
using Xunit.Abstractions;
using ActorServer.Actors;
using ActorServer.Messages;
using ActorServer.Tests.TestHelpers;
using FluentAssertions;
using Akka.Actor;
using ActorServer.Zone;
using Akka.TestKit;

namespace ActorServer.Tests.Actors;

[Collection("ActorTests")]
public class ChatTests : AkkaTestKitBase
{
    public ChatTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// 테스트 1: SendChat 메시지를 받으면 클라이언트로 ChatToClient 메시지 전송
    /// </summary>
    [Fact]
    public void PlayerActor_Should_Send_ChatToClient_When_Receiving_SendChat()
    {
        using var scope = Test();

        // Arrange - 준비
        const long testPlayerId = 1001;
        const string testMessage = "Hello World!";
        
        // 1. TestProbe 생성 (클라이언트 연결 시뮬레이션)
        var clientProbe = CreateTestProbe("clientProbe");
        scope.Log("Created client TestProbe");

        // 2. Mock ClientActor 생성
        var mockClient = Sys.ActorOf(
            Props.Create(() => new MockClientActor(clientProbe)),
            $"mock-client-{testPlayerId}"
        );
        scope.Log("Created Mock ClientActor");

        // 3. PlayerActor 생성
        var playerActor = Sys.ActorOf(
            Props.Create(() => new PlayerActor(testPlayerId)),
            $"test-player-{testPlayerId}"
        );
        scope.LogInfo($"Created PlayerActor with ID: {testPlayerId}");

        // Act - 실행
        scope.LogSeparator();
        
        // 4. 클라이언트 연결 설정
        playerActor.Tell(new SetClientConnection(mockClient));
        scope.Log("Sent SetClientConnection to PlayerActor");
        
        // Actor 처리 대기
        scope.WaitForActors();

        // 5. SendChat 메시지 전송
        var sendChatMsg = new ChatMessage(Message: testMessage);
        
        playerActor.Tell(sendChatMsg);
        scope.LogSuccess($"Sent SendChat message: '{testMessage}'");

        // Assert - 검증
        scope.LogSeparator();
        
        // 6. 클라이언트가 ChatToClient 메시지를 받았는지 확인
        var receivedMsg = clientProbe.ExpectMsg<ChatMessage>(TimeSpan.FromSeconds(1));

        // 7. 메시지 내용 검증
        receivedMsg.Should().NotBeNull();
        receivedMsg.Message.Should().Be(testMessage);
        
        scope.LogSuccess($"Verified - Message: '{receivedMsg.Message}'");
    }

    /// <summary>
    /// 테스트 2: 클라이언트 연결이 없을 때 SendChat 메시지 처리
    /// </summary>
    [Fact]
    public void PlayerActor_Should_Handle_SendChat_Without_Client_Connection()
    {
        using var scope = Test();

        // Arrange
        const long testPlayerId = 1002;
        const string testMessage = "Test without client";

        // 1. PlayerActor 생성 (클라이언트 연결 없이)
        var playerActor = Sys.ActorOf(
            Props.Create(() => new PlayerActor(testPlayerId)),
            $"test-player-no-client-{testPlayerId}"
        );
        scope.LogInfo($"Created PlayerActor without client connection");

        // Act
        scope.LogSeparator();
        
        // 2. SendChat 메시지 전송
        var sendChatMsg = new ChatMessage(Message: testMessage);
        
        playerActor.Tell(sendChatMsg);
        scope.Log($"Sent SendChat message without client: '{testMessage}'");

        // Assert
        scope.LogSeparator();
        
        // 3. 예외가 발생하지 않고 정상 처리되는지 확인
        // (클라이언트가 없어도 에러 없이 처리되어야 함)
        ExpectNoMsg(TimeSpan.FromMilliseconds(500));
        scope.LogSuccess("No errors occurred - handled gracefully without client");
    }
}