using Xunit;
using Xunit.Abstractions;
using ActorServer.Actors;
using ActorServer.Messages;
using ActorServer.Tests.TestHelpers;
using FluentAssertions;
using Akka.Actor;
using Akka.TestKit.Xunit2;

namespace ActorServer.Tests.Actors;

[Collection("ActorTest")]
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
    #endregion
}