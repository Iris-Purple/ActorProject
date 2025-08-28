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


    [Fact]
    public void PlayerMove_Should_Use_Actor_Reference()
    {
        using var scope = Test();

        // Arrange
        var playerProbe = CreateTestProbe("playerProbe");
        var zoneActor = Sys.ActorOf(Props.Create<ZoneActor>(), "test-zone-ref");
        const long playerId = 5001;

        // 1. Zone에 진입
        zoneActor.Tell(new ChangeZoneRequest(playerProbe.Ref, playerId, ZoneId.Town));
        playerProbe.ExpectMsg<ZoneChanged>();

        // Act
        // 2. PlayerActor 참조로 이동 요청
        zoneActor.Tell(new PlayerMove(
            PlayerActor: playerProbe.Ref,  // TestProbe를 PlayerActor처럼 사용
            PlayerId: playerId,
            X: 50.0f,
            Y: 75.0f
        ));

        // Assert
        // 3. PlayerActor(probe)가 직접 응답받음
        var moved = playerProbe.ExpectMsg<PlayerMoved>(TimeSpan.FromSeconds(1));

        moved.PlayerId.Should().Be(playerId);
        moved.X.Should().Be(50.0f);
        moved.Y.Should().Be(75.0f);

        scope.LogSuccess($"Actor reference worked - Player moved to ({moved.X}, {moved.Y})");
    }
    /// <summary>
    /// 테스트 5: PlayerDisconnected 메시지 처리 - 플레이어 제거
    /// </summary>
    [Fact]
    public void ZoneActor_Should_Remove_Player_On_Disconnection()
    {
        using var scope = Test();

        // Arrange
        var playerProbe = CreateTestProbe("playerProbe");
        var zoneActor = Sys.ActorOf(Props.Create<ZoneActor>(), "test-zone-disconnect");
        const long testPlayerId = 2004;

        scope.LogInfo($"Testing PlayerDisconnected for Player {testPlayerId}");

        // Act 1 - 플레이어를 Town에 추가
        zoneActor.Tell(new ChangeZoneRequest(playerProbe.Ref, testPlayerId, ZoneId.Town));
        var response = playerProbe.ExpectMsg<ZoneChanged>(TimeSpan.FromSeconds(1));
        response.NewZoneId.Should().Be(ZoneId.Town);
        scope.LogSuccess("Player added to Town");

        // Act 2 - 플레이어 연결 종료
        zoneActor.Tell(new PlayerDisconnected(testPlayerId));
        scope.LogWarning($"Sent PlayerDisconnected for Player {testPlayerId}");

        Thread.Sleep(500); // 처리 대기

        // Act 3 - 같은 플레이어로 다시 Zone 진입 시도 (제거되었으면 가능해야 함)
        zoneActor.Tell(new ChangeZoneRequest(playerProbe.Ref, testPlayerId, ZoneId.Town));
        var reconnectResponse = playerProbe.ExpectMsg<ZoneChanged>(TimeSpan.FromSeconds(1));

        // Assert
        reconnectResponse.Should().NotBeNull();
        reconnectResponse.NewZoneId.Should().Be(ZoneId.Town);
        scope.LogSuccess("Player successfully removed and can re-enter zone");
    }

    /// <summary>
    /// 테스트 6: 여러 플레이어 중 특정 플레이어만 Disconnect 처리
    /// </summary>
    [Fact]
    public void ZoneActor_Should_Handle_Selective_Disconnection()
    {
        using var scope = Test();

        // Arrange
        var player1Probe = CreateTestProbe("player1Probe");
        var player2Probe = CreateTestProbe("player2Probe");
        var player3Probe = CreateTestProbe("player3Probe");
        var zoneActor = Sys.ActorOf(Props.Create<ZoneActor>(), "test-zone-selective");

        const long playerId1 = 2005;
        const long playerId2 = 2006;
        const long playerId3 = 2007;

        scope.LogInfo("Testing selective player disconnection");

        // Act 1 - 3명 모두 Town에 추가
        zoneActor.Tell(new ChangeZoneRequest(player1Probe.Ref, playerId1, ZoneId.Town));
        player1Probe.ExpectMsg<ZoneChanged>();

        zoneActor.Tell(new ChangeZoneRequest(player2Probe.Ref, playerId2, ZoneId.Town));
        player2Probe.ExpectMsg<ZoneChanged>();

        zoneActor.Tell(new ChangeZoneRequest(player3Probe.Ref, playerId3, ZoneId.Town));
        player3Probe.ExpectMsg<ZoneChanged>();

        scope.Log("All 3 players added to Town");

        // Act 2 - Player2만 disconnect
        zoneActor.Tell(new PlayerDisconnected(playerId2));
        scope.LogWarning($"Disconnected Player {playerId2}");

        Thread.Sleep(500);

        // Act 3 - Player1과 Player3는 여전히 이동 가능해야 함
        zoneActor.Tell(new PlayerMove(player1Probe.Ref, playerId1, 10.0f, 10.0f));
        var move1 = player1Probe.ExpectMsg<PlayerMoved>(TimeSpan.FromSeconds(1));
        move1.PlayerId.Should().Be(playerId1);

        zoneActor.Tell(new PlayerMove(player3Probe.Ref, playerId3, 20.0f, 20.0f));
        var move3 = player3Probe.ExpectMsg<PlayerMoved>(TimeSpan.FromSeconds(1));
        move3.PlayerId.Should().Be(playerId3);

        // Act 4 - Player2는 이동 불가 (Zone에서 제거됨)
        zoneActor.Tell(new PlayerMove(player2Probe.Ref, playerId2, 30.0f, 30.0f));
        var errorMsg = player2Probe.ExpectMsg<ErrorMessage>(TimeSpan.FromSeconds(1));
        errorMsg.Type.Should().Be(ERROR_MSG_TYPE.PLAYER_MOVE_ERROR);

        // Assert
        scope.LogSuccess("Player1 and Player3 continue working, Player2 was removed");
    }

    /// <summary>
    /// 테스트 8: 존재하지 않는 플레이어 Disconnect 처리
    /// </summary>
    [Fact]
    public void ZoneActor_Should_Handle_Disconnect_For_NonExistent_Player()
    {
        using var scope = Test();

        // Arrange
        var zoneActor = Sys.ActorOf(Props.Create<ZoneActor>(), "test-zone-nonexistent");
        const long nonExistentPlayerId = 2009;

        scope.LogInfo($"Testing disconnect for non-existent Player {nonExistentPlayerId}");

        // Act - 존재하지 않는 플레이어 disconnect (에러 없이 처리되어야 함)
        var exception = Record.Exception(() =>
        {
            zoneActor.Tell(new PlayerDisconnected(nonExistentPlayerId));
            Thread.Sleep(500);
        });

        // Assert
        exception.Should().BeNull();
        scope.LogSuccess("Non-existent player disconnect handled gracefully");
    }

    /// <summary>
    /// 테스트 9: 동시 다중 Disconnect 처리
    /// </summary>
    [Fact]
    public void ZoneActor_Should_Handle_Multiple_Simultaneous_Disconnects()
    {
        using var scope = Test();

        // Arrange
        var zoneActor = Sys.ActorOf(Props.Create<ZoneActor>(), "test-zone-multi-disconnect");
        var probes = new List<TestProbe>();
        var playerIds = new List<long>();

        // 10명의 플레이어 생성
        for (int i = 0; i < 10; i++)
        {
            probes.Add(CreateTestProbe($"player{i}Probe"));
            playerIds.Add(2010 + i);
        }

        scope.LogInfo("Testing simultaneous disconnection of 10 players");

        // Act 1 - 모든 플레이어 Town에 추가
        for (int i = 0; i < 10; i++)
        {
            zoneActor.Tell(new ChangeZoneRequest(probes[i].Ref, playerIds[i], ZoneId.Town));
            probes[i].ExpectMsg<ZoneChanged>(TimeSpan.FromSeconds(1));
        }
        scope.Log("All 10 players added to Town");

        // Act 2 - 모든 플레이어 동시 disconnect
        foreach (var playerId in playerIds)
        {
            zoneActor.Tell(new PlayerDisconnected(playerId));
        }
        scope.LogWarning("Sent disconnect for all 10 players");

        Thread.Sleep(1000);

        // Act 3 - 모든 플레이어 재진입 시도
        var reconnectSuccess = true;
        for (int i = 0; i < 10; i++)
        {
            zoneActor.Tell(new ChangeZoneRequest(probes[i].Ref, playerIds[i], ZoneId.Town));
            var response = probes[i].ExpectMsg<ZoneChanged>(TimeSpan.FromSeconds(1));
            if (response.NewZoneId != ZoneId.Town)
            {
                reconnectSuccess = false;
                break;
            }
        }

        // Assert
        reconnectSuccess.Should().BeTrue();
        scope.LogSuccess("All 10 players successfully removed and re-entered");
    }
}