using Xunit;
using Xunit.Abstractions;
using ActorServer.Actors;
using ActorServer.Messages;
using ActorServer.Zone;
using ActorServer.Tests.TestHelpers;
using FluentAssertions;
using Akka.Actor;
using Common.Database;

namespace ActorServer.Tests.Actors;

[Collection("ActorTests")]
public class ZoneDbSaveTests : AkkaTestKitBase
{
    public ZoneDbSaveTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void ZoneActor_Should_Save_Player_Position_To_DB_On_Zone_Change()
    {
        using var scope = Test();

        // Arrange
        var playerProbe = CreateTestProbe("playerProbe");
        var zoneActor = Sys.ActorOf(Props.Create<ZoneActor>(), "test-zone-db");
        const long testPlayerId = 3001;
        var db = PlayerDatabase.Instance;
        
        // 초기 플레이어 생성
        db.GetOrCreatePlayerId(testPlayerId);
        
        scope.LogInfo($"Testing DB save for Player ID: {testPlayerId}");

        // Act - Town으로 이동
        var changeRequest = new ChangeZoneRequest(
            PlayerActor: playerProbe.Ref,
            PlayerId: testPlayerId,
            TargetZoneId: ZoneId.Town
        );
        
        zoneActor.Tell(changeRequest);
        scope.Log("Sent zone change request to Town");

        // Assert
        var response = playerProbe.ExpectMsg<ZoneChanged>(TimeSpan.FromSeconds(2));
        response.NewZoneId.Should().Be(ZoneId.Town);
        
        // DB 확인 (약간의 지연 후)
        Thread.Sleep(500);
        var savedData = db.LoadPlayer(testPlayerId);
        
        savedData.Should().NotBeNull();
        savedData!.Value.zone.Should().Be((int)ZoneId.Town);
        savedData.Value.x.Should().Be(0);  // Town spawn point
        savedData.Value.y.Should().Be(0);
        
        scope.LogSuccess($"DB save verified - Zone: {savedData.Value.zone}, Position: ({savedData.Value.x}, {savedData.Value.y})");
    }
}