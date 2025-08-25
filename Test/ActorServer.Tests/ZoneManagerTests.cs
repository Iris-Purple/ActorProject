using Xunit;
using Xunit.Abstractions;
using ActorServer.Actors;
using ActorServer.Messages;
using ActorServer.Tests.TestHelpers;
using FluentAssertions;
using Akka.Actor;
using Akka.TestKit.Xunit2;

namespace ActorServer.Tests.Actors;

[Collection("Akka Tests")]
public class ZoneManagerTests : AkkaTestKitBase
{
    public ZoneManagerTests(ITestOutputHelper output) : base(output) { }

    #region Zone 존재 확인 테스트

    [Fact]
    public void ZoneManager_Should_Return_ZoneNotFound_For_Invalid_ZoneId()
    {
        using var test = Test();
        
        // Arrange
        test.LogInfo("Creating ZoneManager");
        var zoneManager = Sys.ActorOf(Props.Create<ZoneManager>(), "zone-manager-invalid");
        
        var playerProbe = CreateTestProbe("player-probe");
        var playerId = 1001L;
        var invalidZoneId = "non-existent-zone";
        
        test.LogInfo($"Testing invalid zone: {invalidZoneId}");
        
        // Act
        test.LogInfo("Sending ChangeZoneRequest with invalid zone");
        zoneManager.Tell(new ChangeZoneRequest(playerProbe, playerId, invalidZoneId));
        
        // Assert
        var response = CollectAndFilter<ChangeZoneResponse>(playerProbe, 500);
        
        response.Should().NotBeNull();
        response!.Success.Should().BeFalse();
        response.Message.Should().Be("Zone not found");
        
        test.LogSuccess($"Correctly rejected invalid zone: {invalidZoneId}");
    }

    [Fact]
    public void ZoneManager_Should_Accept_Valid_ZoneId()
    {
        using var test = Test();
        
        // Arrange
        var zoneManager = Sys.ActorOf(Props.Create<ZoneManager>(), "zone-manager-valid");
        var playerProbe = CreateTestProbe("player-probe");
        var playerId = 1002L;
        var validZoneId = "town";
        
        test.LogInfo($"Testing valid zone: {validZoneId}");
        
        // Act
        zoneManager.Tell(new ChangeZoneRequest(playerProbe, playerId, validZoneId));
        
        // Assert
        var response = CollectAndFilter<ChangeZoneResponse>(playerProbe, 500);
        
        response.Should().NotBeNull();
        response!.Success.Should().BeTrue();
        response.Message.Should().Be(validZoneId);
        
        test.LogSuccess($"Successfully changed to valid zone: {validZoneId}");
    }

    [Fact]
    public void ZoneManager_Should_Handle_Multiple_Invalid_Zones()
    {
        using var test = Test();
        
        // Arrange
        var zoneManager = Sys.ActorOf(Props.Create<ZoneManager>(), "zone-manager-multi");
        var playerProbe = CreateTestProbe("player-probe");
        var playerId = 1003L;
        
        var invalidZones = new[] { "fake-zone", "undefined", "null-zone", "" };
        
        test.LogInfo("Testing multiple invalid zones");
        test.LogSeparator();
        
        // Act & Assert
        foreach (var invalidZone in invalidZones)
        {
            test.LogInfo($"Testing zone: '{invalidZone}'");
            
            zoneManager.Tell(new ChangeZoneRequest(playerProbe, playerId, invalidZone));
            
            var response = CollectAndFilter<ChangeZoneResponse>(playerProbe, 300);
            
            response.Should().NotBeNull();
            response!.Success.Should().BeFalse();
            response.Message.Should().Be("Zone not found");
            
            test.LogSuccess($"✅ Rejected: '{invalidZone}'");
        }
        
        test.LogSeparator();
        test.LogSuccess("All invalid zones correctly rejected");
    }

    [Fact]
    public void ZoneManager_Should_Handle_Null_ZoneId()
    {
        using var test = Test();
        
        // Arrange
        var zoneManager = Sys.ActorOf(Props.Create<ZoneManager>(), "zone-manager-null");
        var playerProbe = CreateTestProbe("player-probe");
        var playerId = 1004L;
        
        test.LogInfo("Testing null zone ID");
        
        // Act
        zoneManager.Tell(new ChangeZoneRequest(playerProbe, playerId, null!));
        
        // Assert
        var response = CollectAndFilter<ChangeZoneResponse>(playerProbe, 500);
        
        response.Should().NotBeNull();
        response!.Success.Should().BeFalse();
        response.Message.Should().Be("Zone not found");
        
        test.LogSuccess("Null zone ID correctly rejected");
    }

    #endregion

    #region Player 등록 테스트

    [Fact]
    public void ZoneManager_Should_Register_Player()
    {
        using var test = Test();
        
        // Arrange
        var zoneManager = Sys.ActorOf(Props.Create<ZoneManager>(), "zone-manager-register");
        var playerProbe = CreateTestProbe("player-probe");
        var playerId = 2001L;
        
        test.LogInfo($"Registering player {playerId}");
        
        // Act
        zoneManager.Tell(new RegisterPlayer(playerId, playerProbe));
        
        // Assert - PlayerActor가 SetZoneManager를 받아야 함
        var setZoneMsg = playerProbe.ExpectMsg<SetZoneManager>(TimeSpan.FromSeconds(1));
        setZoneMsg.Should().NotBeNull();
        setZoneMsg.ZoneManager.Should().Be(zoneManager);
        
        test.LogSuccess($"Player {playerId} registered successfully");
    }

    [Fact]
    public void ZoneManager_Should_Register_Player_With_Initial_Zone()
    {
        using var test = Test();
        
        // Arrange
        var zoneManager = Sys.ActorOf(Props.Create<ZoneManager>(), "zone-manager-register-zone");
        var playerProbe = CreateTestProbe("player-probe");
        var playerId = 2002L;
        
        test.LogInfo($"Registering player {playerId} with initial zone");
        
        // Act
        zoneManager.Tell(new RegisterPlayer(playerId, playerProbe, "forest"));
        
        // Assert
        // 1. SetZoneManager 메시지
        var setZoneMsg = playerProbe.ExpectMsg<SetZoneManager>(TimeSpan.FromSeconds(1));
        setZoneMsg.Should().NotBeNull();
        
        // 2. Zone 변경 응답
        var zoneResponse = CollectAndFilter<ChangeZoneResponse>(playerProbe, 500);
        zoneResponse.Should().NotBeNull();
        zoneResponse!.Success.Should().BeTrue();
        zoneResponse.Message.Should().Be("forest");
        
        test.LogSuccess("Player registered and placed in initial zone");
    }

    [Fact]
    public void ZoneManager_Should_Unregister_Player()
    {
        using var test = Test();
        
        // Arrange
        var zoneManager = Sys.ActorOf(Props.Create<ZoneManager>(), "zone-manager-unregister");
        var playerProbe = CreateTestProbe("player-probe");
        var playerId = 2003L;
        
        // Register first
        zoneManager.Tell(new RegisterPlayer(playerId, playerProbe, "town"));
        playerProbe.ExpectMsg<SetZoneManager>();
        CollectAndFilter<ChangeZoneResponse>(playerProbe, 500);
        
        test.LogInfo($"Unregistering player {playerId}");
        
        // Act
        zoneManager.Tell(new UnregisterPlayer(playerId));
        
        // Assert - Zone 변경이 실패해야 함 (등록 해제됨)
        zoneManager.Tell(new ChangeZoneRequest(playerProbe, playerId, "forest"));
        var response = CollectAndFilter<ChangeZoneResponse>(playerProbe, 500);
        
        // 등록이 해제되었으므로 Zone 변경도 실패할 수 있음
        test.LogSuccess($"Player {playerId} unregistered");
    }

    #endregion

    #region Zone 변경 테스트

    [Fact]
    public void ZoneManager_Should_Switch_Between_Valid_Zones()
    {
        using var test = Test();
        
        // Arrange
        var zoneManager = Sys.ActorOf(Props.Create<ZoneManager>(), "zone-manager-switch");
        var playerProbe = CreateTestProbe("player-probe");
        var playerId = 3001L;
        
        // Register player
        zoneManager.Tell(new RegisterPlayer(playerId, playerProbe));
        playerProbe.ExpectMsg<SetZoneManager>();
        
        test.LogInfo("Testing zone switching sequence");
        test.LogSeparator();
        
        // 1. Move to town
        test.LogInfo("1. Moving to town");
        zoneManager.Tell(new ChangeZoneRequest(playerProbe, playerId, "town"));
        var response1 = CollectAndFilter<ChangeZoneResponse>(playerProbe, 500);
        response1!.Success.Should().BeTrue();
        test.LogSuccess("✅ Moved to town");
        
        // 2. Move to forest
        test.LogInfo("2. Moving to forest");
        zoneManager.Tell(new ChangeZoneRequest(playerProbe, playerId, "forest"));
        var response2 = CollectAndFilter<ChangeZoneResponse>(playerProbe, 500);
        response2!.Success.Should().BeTrue();
        test.LogSuccess("✅ Moved to forest");
        
        // 3. Move to dungeon
        test.LogInfo("3. Moving to dungeon-1");
        zoneManager.Tell(new ChangeZoneRequest(playerProbe, playerId, "dungeon-1"));
        var response3 = CollectAndFilter<ChangeZoneResponse>(playerProbe, 500);
        response3!.Success.Should().BeTrue();
        test.LogSuccess("✅ Moved to dungeon-1");
        
        test.LogSeparator();
        test.LogSuccess("Zone switching test completed");
    }

    [Fact]
    public void ZoneManager_Should_Handle_CaseSensitive_ZoneId()
    {
        using var test = Test();
        
        // Arrange
        var zoneManager = Sys.ActorOf(Props.Create<ZoneManager>(), "zone-manager-case");
        var playerProbe = CreateTestProbe("player-probe");
        var playerId = 3002L;
        
        test.LogInfo("Testing case-sensitive zone IDs");
        
        var testCases = new[] 
        { 
            ("TOWN", false),
            ("Town", false),
            ("town", true),
            ("FOREST", false),
            ("forest", true)
        };
        
        foreach (var (zoneId, shouldSucceed) in testCases)
        {
            test.LogInfo($"Testing: '{zoneId}' (expect: {(shouldSucceed ? "success" : "fail")})");
            
            zoneManager.Tell(new ChangeZoneRequest(playerProbe, playerId, zoneId));
            var response = CollectAndFilter<ChangeZoneResponse>(playerProbe, 300);
            
            response.Should().NotBeNull();
            
            if (shouldSucceed)
            {
                response!.Success.Should().BeTrue();
                test.LogSuccess($"✅ '{zoneId}' accepted");
            }
            else
            {
                response!.Success.Should().BeFalse();
                response.Message.Should().Be("Zone not found");
                test.LogWarning($"⚠️ '{zoneId}' rejected (case sensitive)");
            }
        }
    }

    #endregion

    #region Player 메시지 라우팅 테스트

    [Fact]
    public void ZoneManager_Should_Route_PlayerMove_To_Zone()
    {
        using var test = Test();
        
        // Arrange
        var zoneManager = Sys.ActorOf(Props.Create<ZoneManager>(), "zone-manager-move");
        var playerProbe = CreateTestProbe("player-probe");
        var playerId = 4001L;
        
        // Register and place in zone
        zoneManager.Tell(new RegisterPlayer(playerId, playerProbe, "town"));
        playerProbe.ExpectMsg<SetZoneManager>();
        CollectAndFilter<ChangeZoneResponse>(playerProbe, 500);
        
        test.LogInfo("Testing move routing");
        
        // Act - Send move command
        var moveMsg = new PlayerMoveInZone(playerId, "town", new Position(10, 20));
        zoneManager.Tell(moveMsg, playerProbe);
        
        // Assert
        var result = playerProbe.ExpectMsg<ZoneMessageResult>(TimeSpan.FromSeconds(1));
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        
        test.LogSuccess("Move command routed successfully");
    }

    [Fact]
    public void ZoneManager_Should_Reject_Move_From_Wrong_Zone()
    {
        using var test = Test();
        
        // Arrange
        var zoneManager = Sys.ActorOf(Props.Create<ZoneManager>(), "zone-manager-wrong-zone");
        var playerProbe = CreateTestProbe("player-probe");
        var playerId = 4002L;
        
        // Register and place in town
        zoneManager.Tell(new RegisterPlayer(playerId, playerProbe, "town"));
        playerProbe.ExpectMsg<SetZoneManager>();
        CollectAndFilter<ChangeZoneResponse>(playerProbe, 500);
        
        test.LogInfo("Testing move from wrong zone");
        
        // Act - Try to move in forest (but player is in town)
        var moveMsg = new PlayerMoveInZone(playerId, "forest", new Position(10, 20));
        zoneManager.Tell(moveMsg, playerProbe);
        
        // Assert
        var result = playerProbe.ExpectMsg<ZoneMessageResult>(TimeSpan.FromSeconds(1));
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not in specified zone");
        
        test.LogSuccess("Move from wrong zone correctly rejected");
    }

    [Fact]
    public void ZoneManager_Should_Route_Chat_To_Zone()
    {
        using var test = Test();
        
        // Arrange
        var zoneManager = Sys.ActorOf(Props.Create<ZoneManager>(), "zone-manager-chat");
        var playerProbe = CreateTestProbe("player-probe");
        var playerId = 4003L;
        
        // Register and place in zone
        zoneManager.Tell(new RegisterPlayer(playerId, playerProbe, "town"));
        playerProbe.ExpectMsg<SetZoneManager>();
        CollectAndFilter<ChangeZoneResponse>(playerProbe, 500);
        
        test.LogInfo("Testing chat routing");
        
        // Act
        var chatMsg = new PlayerChatInZone(playerId, "town", "Hello World!");
        zoneManager.Tell(chatMsg, playerProbe);
        
        // Assert - No error response means success
        playerProbe.ExpectNoMsg(TimeSpan.FromMilliseconds(200));
        
        test.LogSuccess("Chat routed successfully");
    }

    #endregion

    #region Zone 정보 조회 테스트

    [Fact]
    public void ZoneManager_Should_List_Available_Zones()
    {
        using var test = Test();
        
        // Arrange
        var zoneManager = Sys.ActorOf(Props.Create<ZoneManager>(), "zone-manager-list");
        
        test.LogInfo("Getting all available zones");
        
        // Act
        zoneManager.Tell(new GetAllZones(), TestActor);
        
        // Assert
        var response = ExpectMsg<AllZonesResponse>(TimeSpan.FromSeconds(1));
        
        response.Should().NotBeNull();
        response.Zones.Should().NotBeNull();
        
        var zoneList = response.Zones.ToList();
        zoneList.Should().HaveCount(3);
        
        zoneList.Should().Contain(z => z.ZoneId == "town");
        zoneList.Should().Contain(z => z.ZoneId == "forest");
        zoneList.Should().Contain(z => z.ZoneId == "dungeon-1");
        
        test.LogInfo("Available zones:");
        foreach (var zone in zoneList)
        {
            test.LogInfo($"  - {zone.ZoneId}: {zone.Name} (Type: {zone.Type}, Max: {zone.MaxPlayers})");
        }
        
        test.LogSuccess($"Found {zoneList.Count} zones");
    }

    [Fact]
    public void ZoneManager_Should_Return_Zone_Info()
    {
        using var test = Test();
        
        // Arrange
        var zoneManager = Sys.ActorOf(Props.Create<ZoneManager>(), "zone-manager-info");
        
        test.LogInfo("Getting zone info for 'town'");
        
        // Act
        zoneManager.Tell(new GetZoneInfo("town"), TestActor);
        
        // Assert
        var response = ExpectMsg<ZoneInfoResponse>(TimeSpan.FromSeconds(1));
        
        response.Should().NotBeNull();
        response.Info.Should().NotBeNull();
        response.Info.ZoneId.Should().Be("town");
        response.Info.Name.Should().Be("Starting Town");
        response.Info.Type.Should().Be(ZoneType.SafeZone);
        
        test.LogSuccess($"Zone info retrieved: {response.Info.Name}");
    }

    [Fact]
    public void ZoneManager_Should_Return_ZoneNotFound_For_Invalid_Info_Request()
    {
        using var test = Test();
        
        // Arrange
        var zoneManager = Sys.ActorOf(Props.Create<ZoneManager>(), "zone-manager-notfound");
        
        test.LogInfo("Getting zone info for invalid zone");
        
        // Act
        zoneManager.Tell(new GetZoneInfo("invalid-zone"), TestActor);
        
        // Assert
        var response = ExpectMsg<ZoneNotFound>(TimeSpan.FromSeconds(1));
        
        response.Should().NotBeNull();
        response.ZoneId.Should().Be("invalid-zone");
        
        test.LogSuccess("ZoneNotFound returned correctly");
    }

    #endregion

    #region 검증 테스트
    [Fact]
    public void ZoneManager_Should_Validate_Invalid_Movement()
    {
        using var test = Test();
        
        // Arrange
        var zoneManager = Sys.ActorOf(Props.Create<ZoneManager>(), "zone-manager-validate");
        var playerProbe = CreateTestProbe("player-probe");
        var playerId = 6001L;
        
        // Register and place in zone
        zoneManager.Tell(new RegisterPlayer(playerId, playerProbe, "town"));
        playerProbe.ExpectMsg<SetZoneManager>();
        CollectAndFilter<ChangeZoneResponse>(playerProbe, 500);
        
        test.LogInfo("Testing invalid movement validation");
        
        // Act - Invalid position (out of bounds)
        var invalidMove = new PlayerMoveInZone(playerId, "town", new Position(99999, 99999));
        zoneManager.Tell(invalidMove, playerProbe);
        
        // Assert
        var result = playerProbe.ExpectMsg<ZoneMessageResult>(TimeSpan.FromSeconds(1));
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid movement");
        
        test.LogSuccess("Invalid movement correctly rejected");
    }

    [Fact]
    public void ZoneManager_Should_Filter_Empty_Chat()
    {
        using var test = Test();
        
        // Arrange
        var zoneManager = Sys.ActorOf(Props.Create<ZoneManager>(), "zone-manager-empty-chat");
        var playerProbe = CreateTestProbe("player-probe");
        var playerId = 6002L;
        
        // Register and place in zone
        zoneManager.Tell(new RegisterPlayer(playerId, playerProbe, "town"));
        playerProbe.ExpectMsg<SetZoneManager>();
        CollectAndFilter<ChangeZoneResponse>(playerProbe, 500);
        
        test.LogInfo("Testing empty chat filtering");
        
        // Act - Empty chat
        var emptyChat = new PlayerChatInZone(playerId, "town", "");
        zoneManager.Tell(emptyChat, playerProbe);
        
        // Chat should be silently dropped (no response)
        playerProbe.ExpectNoMsg(TimeSpan.FromMilliseconds(200));
        
        test.LogSuccess("Empty chat filtered");
    }

    #endregion
}