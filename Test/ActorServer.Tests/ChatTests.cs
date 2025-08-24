using Akka.Actor;
using Akka.TestKit.Xunit2;
using ActorServer.Actors;
using ActorServer.Messages;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Common.Database;
using Microsoft.Data.Sqlite;

namespace ActorServer.Tests.Integration;

public class ChatTests : TestKit
{
    private readonly ITestOutputHelper _output;
    private static long _testPlayerId = 3000;

    public ChatTests(ITestOutputHelper output)
    {
        _output = output;
        _testPlayerId += 10;
        
        // InitializeTestPlayers() 제거 - 각 테스트에서 필요시 생성
    }
    
    // InitializeTestPlayers 메서드 완전 제거

    [Fact]
    public void Two_Players_In_Same_Zone_Should_See_Each_Others_Chat()
    {
        // Arrange
        var worldActor = Sys.ActorOf(Props.Create<WorldActor>(), "chat-world");

        var aliceProbe = CreateTestProbe("alice");
        var bobProbe = CreateTestProbe("bob");
        
        var aliceId = _testPlayerId + 1;
        var bobId = _testPlayerId + 2;

        // 테스트 내에서 필요시 생성 (try-catch로 보호)
        try
        {
            var db = PlayerDatabase.Instance;
            db.GetOrCreatePlayerId(aliceId);
            db.GetOrCreatePlayerId(bobId);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 8)
        {
            _output.WriteLine("[TEST] Ignoring readonly DB error in test");
        }

        // Alice 로그인
        worldActor.Tell(new PlayerEnterWorld(aliceId));
        worldActor.Tell(new RegisterClientConnection(aliceId, aliceProbe));

        // Bob 로그인
        worldActor.Tell(new PlayerEnterWorld(bobId));
        worldActor.Tell(new RegisterClientConnection(bobId, bobProbe));

        // 초기 메시지 처리
        ExpectNoMsg(TimeSpan.FromSeconds(1));

        // Act - Alice가 채팅
        _output.WriteLine($"Alice (ID:{aliceId}) sending chat message...");
        worldActor.Tell(new PlayerCommand(aliceId,
            new ChatMessage(aliceId, "Hello Bob!")));

        // Assert - Bob이 메시지를 받아야 함
        bobProbe.FishForMessage(
            msg => msg is ChatToClient chat &&
                   chat.Message.Contains("Hello Bob"),
            TimeSpan.FromSeconds(2));

        _output.WriteLine("✅ Chat delivered successfully");
    }
    
    [Fact]
    public void Player_Should_Receive_Own_Chat_Message()
    {
        // Arrange
        var worldActor = Sys.ActorOf(Props.Create<WorldActor>(), "self-chat-world");
        var aliceProbe = CreateTestProbe("alice-self");
        var aliceId = _testPlayerId + 3;
        
        // 플레이어 초기화 (try-catch로 보호)
        try
        {
            var db = PlayerDatabase.Instance;
            db.GetOrCreatePlayerId(aliceId);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 8)
        {
            _output.WriteLine("[TEST] Ignoring readonly DB error in test");
        }
        
        // Alice 로그인
        worldActor.Tell(new PlayerEnterWorld(aliceId));
        worldActor.Tell(new RegisterClientConnection(aliceId, aliceProbe));
        
        ExpectNoMsg(TimeSpan.FromSeconds(1));
        
        // Act - Alice가 채팅
        _output.WriteLine($"Alice (ID:{aliceId}) sending message...");
        worldActor.Tell(new PlayerCommand(aliceId,
            new ChatMessage(aliceId, "Testing self message")));
        
        // Assert - Alice도 자신의 메시지를 받아야 함
        aliceProbe.FishForMessage(
            msg => msg is ChatToClient chat &&
                   chat.Message.Contains("Testing self message"),
            TimeSpan.FromSeconds(2),
            "Player should receive their own chat message");
        
        _output.WriteLine("✅ Self chat received successfully");
    }
    
    [Fact]
    public void Players_In_Different_Zones_Should_Not_See_Each_Others_Chat()
    {
        // Arrange
        var worldActor = Sys.ActorOf(Props.Create<WorldActor>(), "zone-chat-world");
        
        var aliceProbe = CreateTestProbe("alice-zone");
        var bobProbe = CreateTestProbe("bob-zone");
        var aliceId = _testPlayerId + 4;
        var bobId = _testPlayerId + 5;
        
        // 플레이어 초기화 (try-catch로 보호)
        try
        {
            var db = PlayerDatabase.Instance;
            db.GetOrCreatePlayerId(aliceId);
            db.GetOrCreatePlayerId(bobId);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 8)
        {
            _output.WriteLine("[TEST] Ignoring readonly DB error in test");
        }
        
        // Alice와 Bob 로그인
        worldActor.Tell(new PlayerEnterWorld(aliceId));
        worldActor.Tell(new RegisterClientConnection(aliceId, aliceProbe));
        
        worldActor.Tell(new PlayerEnterWorld(bobId));
        worldActor.Tell(new RegisterClientConnection(bobId, bobProbe));
        
        ExpectNoMsg(TimeSpan.FromSeconds(1));
        
        // Bob을 다른 Zone으로 이동
        _output.WriteLine($"Moving Bob (ID:{bobId}) to forest zone...");
        worldActor.Tell(new RequestZoneChange(bobId, "forest"));
        
        ExpectNoMsg(TimeSpan.FromSeconds(1));
        
        // Act - Alice가 town에서 채팅
        _output.WriteLine($"Alice (ID:{aliceId}) sending chat in town...");
        worldActor.Tell(new PlayerCommand(aliceId,
            new ChatMessage(aliceId, "Hello from town!")));
        
        // Alice는 자신의 메시지를 받아야 함 (수정: FishForMessage 사용)
        aliceProbe.FishForMessage<ChatToClient>(
            msg => msg.Message.Contains("Hello from town"),
            TimeSpan.FromSeconds(2),
            "Alice should receive her own message");
        
        // Bob은 메시지를 받지 않아야 함
        bobProbe.ExpectNoMsg(TimeSpan.FromSeconds(1));
        
        _output.WriteLine("✅ Zone isolation working correctly");
    }
    
    [Fact]
    public void Multiple_Players_Should_Receive_Broadcast_Message()
    {
        // Arrange
        var worldActor = Sys.ActorOf(Props.Create<WorldActor>(), "broadcast-world");
        
        var aliceProbe = CreateTestProbe("alice-broadcast");
        var bobProbe = CreateTestProbe("bob-broadcast");
        var charlieProbe = CreateTestProbe("charlie-broadcast");
        
        var aliceId = _testPlayerId + 6;
        var bobId = _testPlayerId + 7;
        var charlieId = _testPlayerId + 8;
        
        // 플레이어 초기화 (try-catch로 보호)
        try
        {
            var db = PlayerDatabase.Instance;
            db.GetOrCreatePlayerId(aliceId);
            db.GetOrCreatePlayerId(bobId);
            db.GetOrCreatePlayerId(charlieId);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 8)
        {
            _output.WriteLine("[TEST] Ignoring readonly DB error in test");
        }
        
        // 3명 모두 로그인
        worldActor.Tell(new PlayerEnterWorld(aliceId));
        worldActor.Tell(new RegisterClientConnection(aliceId, aliceProbe));
        
        worldActor.Tell(new PlayerEnterWorld(bobId));
        worldActor.Tell(new RegisterClientConnection(bobId, bobProbe));
        
        worldActor.Tell(new PlayerEnterWorld(charlieId));
        worldActor.Tell(new RegisterClientConnection(charlieId, charlieProbe));
        
        ExpectNoMsg(TimeSpan.FromSeconds(1));
        
        // Act - Alice가 채팅
        _output.WriteLine($"Alice (ID:{aliceId}) broadcasting message...");
        worldActor.Tell(new PlayerCommand(aliceId,
            new ChatMessage(aliceId, "Hello everyone!")));
        
        // Assert - Bob과 Charlie 모두 메시지를 받아야 함
        bobProbe.FishForMessage(
            msg => msg is ChatToClient chat &&
                   chat.Message.Contains("Hello everyone"),
            TimeSpan.FromSeconds(2),
            "Bob should receive broadcast");
            
        charlieProbe.FishForMessage(
            msg => msg is ChatToClient chat &&
                   chat.Message.Contains("Hello everyone"),
            TimeSpan.FromSeconds(2),
            "Charlie should receive broadcast");
        
        _output.WriteLine("✅ Broadcast to multiple players successful");
    }
    
    [Fact]
    public void Chat_Should_Work_After_Player_Reconnect()
    {
        // Arrange
        var worldActor = Sys.ActorOf(Props.Create<WorldActor>(), "reconnect-chat-world");
        
        var aliceProbe1 = CreateTestProbe("alice-reconnect-1");
        var aliceProbe2 = CreateTestProbe("alice-reconnect-2");
        var bobProbe = CreateTestProbe("bob-reconnect");
        
        var aliceId = _testPlayerId + 9;
        var bobId = _testPlayerId + 10;
        
        // 플레이어 초기화 (try-catch로 보호)
        try
        {
            var db = PlayerDatabase.Instance;
            db.GetOrCreatePlayerId(aliceId);
            db.GetOrCreatePlayerId(bobId);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 8)
        {
            _output.WriteLine("[TEST] Ignoring readonly DB error in test");
        }
        
        // 초기 로그인
        worldActor.Tell(new PlayerEnterWorld(aliceId));
        worldActor.Tell(new RegisterClientConnection(aliceId, aliceProbe1));
        
        worldActor.Tell(new PlayerEnterWorld(bobId));
        worldActor.Tell(new RegisterClientConnection(bobId, bobProbe));
        
        ExpectNoMsg(TimeSpan.FromSeconds(1));
        
        // Alice 재접속 시뮬레이션
        _output.WriteLine($"Simulating Alice (ID:{aliceId}) reconnection...");
        worldActor.Tell(new PlayerDisconnect(aliceId));
        ExpectNoMsg(TimeSpan.FromMilliseconds(500));
        
        // Alice 재접속
        worldActor.Tell(new PlayerEnterWorld(aliceId));
        worldActor.Tell(new RegisterClientConnection(aliceId, aliceProbe2));
        
        ExpectNoMsg(TimeSpan.FromSeconds(1));
        
        // Act - 재접속한 Alice가 채팅
        _output.WriteLine($"Reconnected Alice (ID:{aliceId}) sending chat...");
        worldActor.Tell(new PlayerCommand(aliceId,
            new ChatMessage(aliceId, "I'm back!")));
        
        // Assert - Bob이 메시지를 받아야 함
        bobProbe.FishForMessage(
            msg => msg is ChatToClient chat &&
                   chat.Message.Contains("I'm back"),
            TimeSpan.FromSeconds(2),
            "Bob should receive message from reconnected Alice");
        
        _output.WriteLine("✅ Chat works after reconnection");
    }

    protected override void AfterAll()
    {
        Shutdown();
    }
}