using Xunit;
using Xunit.Abstractions;
using ActorServer.Actors;
using ActorServer.Messages;
using ActorServer.Zone;
using ActorServer.Tests.TestHelpers;
using FluentAssertions;
using Akka.Actor;
using Akka.TestKit;

namespace ActorServer.Tests.Actors;

[Collection("ActorTests")]
public class ZoneActorTests : AkkaTestKitBase
{
    public ZoneActorTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// 테스트 1: 정상적인 Zone 변경
    /// </summary>
    [Fact]
    public void ZoneActor_Should_Handle_Zone_Change_Successfully()
    {
        using var scope = Test();

        // Arrange - 준비
        var playerProbe = CreateTestProbe("playerProbe");  // PlayerActor 대신 TestProbe 사용
        var zoneActor = Sys.ActorOf(Props.Create<ZoneActor>(), "test-zone-actor");
        const long testPlayerId = 2001;
        
        scope.LogInfo($"Created ZoneActor and PlayerProbe for Player ID: {testPlayerId}");

        // Act - 실행
        scope.LogSeparator();
        
        // Town으로 Zone 변경 요청
        var changeRequest = new ChangeZoneRequest(
            PlayerActor: playerProbe.Ref,
            PlayerId: testPlayerId,
            TargetZoneId: ZoneId.Town  // Town으로 이동
        );
        
        zoneActor.Tell(changeRequest);
        scope.Log($"Sent zone change request: Player {testPlayerId} → Town");

        // Assert - 검증
        scope.LogSeparator();
        
        // ZoneChanged 메시지를 받아야 함
        var response = playerProbe.ExpectMsg<ZoneChanged>(TimeSpan.FromSeconds(2));
        
        response.Should().NotBeNull();
        response.NewZoneId.Should().Be(ZoneId.Town);
        response.SpawnPosition.Should().Be(new Position(0, 0));  // Town의 스폰 위치
        
        scope.LogSuccess($"Zone change successful - Zone: {response.NewZoneId}, Position: ({response.SpawnPosition.X}, {response.SpawnPosition.Y})");
    }

    /// <summary>
    /// 테스트 2: 존재하지 않는 Zone으로 변경 시도
    /// </summary>
    [Fact]
    public void ZoneActor_Should_Handle_Invalid_Zone_Request()
    {
        using var scope = Test();

        // Arrange
        var playerProbe = CreateTestProbe("playerProbe");
        var zoneActor = Sys.ActorOf(Props.Create<ZoneActor>(), "test-zone-actor-invalid");
        const long testPlayerId = 2002;
        
        scope.LogInfo("Testing invalid zone change request");

        // Act
        var invalidRequest = new ChangeZoneRequest(
            PlayerActor: playerProbe.Ref,
            PlayerId: testPlayerId,
            TargetZoneId: (ZoneId)999  // 존재하지 않는 Zone ID
        );
        
        zoneActor.Tell(invalidRequest);
        scope.LogWarning($"Sent invalid zone request: Zone ID 999");

        // Assert
        // ErrorMessage를 받아야 함
        var errorMsg = playerProbe.ExpectMsg<ErrorMessage>(TimeSpan.FromSeconds(2));
        
        errorMsg.Should().NotBeNull();
        errorMsg.Type.Should().Be(ERROR_MSG_TYPE.ZONE_CHANGE_ERROR);
        errorMsg.Reason.Should().Contain("Zone not found");
        
        scope.LogSuccess($"Correctly received error: {errorMsg.Reason}");
    }

    /// <summary>
    /// 테스트 3: Zone 간 이동 (Town → Forest → Town)
    /// </summary>
    [Fact]
    public void ZoneActor_Should_Handle_Multiple_Zone_Changes()
    {
        using var scope = Test();

        // Arrange
        var playerProbe = CreateTestProbe("playerProbe");
        var zoneActor = Sys.ActorOf(Props.Create<ZoneActor>(), "test-zone-actor-multi");
        const long testPlayerId = 2003;
        
        scope.LogInfo("Testing multiple zone changes");

        // Act & Assert - 첫 번째 이동: Town
        scope.LogSeparator();
        
        zoneActor.Tell(new ChangeZoneRequest(playerProbe.Ref, testPlayerId, ZoneId.Town));
        var response1 = playerProbe.ExpectMsg<ZoneChanged>(TimeSpan.FromSeconds(1));
        response1.NewZoneId.Should().Be(ZoneId.Town);
        scope.LogSuccess("Move 1: → Town");

        // 두 번째 이동: Forest
        zoneActor.Tell(new ChangeZoneRequest(playerProbe.Ref, testPlayerId, ZoneId.Forest));
        var response2 = playerProbe.ExpectMsg<ZoneChanged>(TimeSpan.FromSeconds(1));
        response2.NewZoneId.Should().Be(ZoneId.Forest);
        response2.SpawnPosition.Should().Be(new Position(100, 100));  // Forest 스폰 위치
        scope.LogSuccess("Move 2: Town → Forest");

        // 세 번째 이동: 다시 Town으로
        zoneActor.Tell(new ChangeZoneRequest(playerProbe.Ref, testPlayerId, ZoneId.Town));
        var response3 = playerProbe.ExpectMsg<ZoneChanged>(TimeSpan.FromSeconds(1));
        response3.NewZoneId.Should().Be(ZoneId.Town);
        scope.LogSuccess("Move 3: Forest → Town");
        
        scope.LogInfo("All zone changes completed successfully");
    }
}