using Akka.Actor;
using Akka.TestKit.Xunit2;
using ActorServer.Actors;
using ActorServer.Messages;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace ActorServer.Tests.Integration;

public class ChatTests : TestKit
{
    private readonly ITestOutputHelper _output;

    public ChatTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Two_Players_In_Same_Zone_Should_See_Each_Others_Chat()
    {
        // Arrange - 2명의 플레이어 생성
        var worldActor = Sys.ActorOf(Props.Create<WorldActor>(), "chat-world");

        var aliceProbe = CreateTestProbe("alice");
        var bobProbe = CreateTestProbe("bob");

        // Alice 로그인
        worldActor.Tell(new PlayerLoginRequest("Alice"));
        worldActor.Tell(new RegisterClientConnection("Alice", aliceProbe));

        // Bob 로그인
        worldActor.Tell(new PlayerLoginRequest("Bob"));
        worldActor.Tell(new RegisterClientConnection("Bob", bobProbe));

        // 초기 메시지 처리 (간단히)
        ExpectNoMsg(TimeSpan.FromSeconds(1));

        // Act - Alice가 채팅
        _output.WriteLine("Alice sending chat message...");
        worldActor.Tell(new PlayerCommand("Alice",
            new ChatMessage("Alice", "Hello Bob!")));

        // Assert - Bob이 메시지를 받아야 함
        bobProbe.FishForMessage(
            msg => msg is ChatToClient chat &&
                   chat.Message.Contains("Hello Bob"),
            TimeSpan.FromSeconds(2));

        _output.WriteLine("✅ Chat delivered successfully");
    }

    protected override void AfterAll()
    {
        Shutdown();
    }
}