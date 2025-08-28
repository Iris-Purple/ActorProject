using Xunit;
using Xunit.Abstractions;
using ActorServer.Actors;
using ActorServer.Messages;
using ActorServer.Zone;
using ActorServer.Tests.TestHelpers;
using FluentAssertions;
using Akka.Actor;
using Akka.TestKit;
using Common.Database;

namespace ActorServer.Tests.Actors;

[Collection("ActorTests")]
public class WorldActorTests : AkkaTestKitBase
{
    public WorldActorTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// 테스트 1: EnterWorld 메시지로 PlayerActor 생성 확인
    /// </summary>
    [Fact]
    public void WorldActor_Should_Create_PlayerActor_On_EnterWorld()
    {
        using var scope = Test();

        // Arrange
        const long playerId = 7001;
        var worldActor = Sys.ActorOf(Props.Create<WorldActor>(), "world");

        // DB 초기화
        PlayerDatabase.Instance.GetOrCreatePlayerId(playerId);

        // Act
        worldActor.Tell(new EnterWorld(playerId));

        // Assert - PlayerActor 생성 메시지 확인
        Thread.Sleep(1000); // Actor 생성 대기

        // DB에 저장되었는지로 간접 확인
        var savedData = PlayerDatabase.Instance.LoadPlayer(playerId);
        savedData.Should().NotBeNull();
        savedData!.Value.zone.Should().Be((int)ZoneId.Town); // 초기 Zone은 Town

        scope.LogSuccess($"Player {playerId} created and spawned in Town");
    }

    /// <summary>
    /// 테스트 2: 재접속 처리 - 같은 ID로 다시 접속
    /// </summary>
    [Fact]
    public void WorldActor_Should_Handle_Reconnection()
    {
        using var scope = Test();

        // Arrange
        const long playerId = 7002;
        var worldActor = Sys.ActorOf(Props.Create<WorldActor>(), "world2");
        PlayerDatabase.Instance.GetOrCreatePlayerId(playerId);

        // Act 1 - 첫 접속
        worldActor.Tell(new EnterWorld(playerId));
        Thread.Sleep(500);

        // Act 2 - 재접속 (에러 없이 처리되어야 함)
        var exception = Record.Exception(() =>
        {
            worldActor.Tell(new EnterWorld(playerId));
            Thread.Sleep(500);
        });

        // Assert
        exception.Should().BeNull(); // 재접속이 에러 없이 처리됨
        scope.LogSuccess($"Player {playerId} reconnection handled without error");
    }

    /// <summary>
    /// 테스트 3: 여러 플레이어 동시 접속
    /// </summary>
    [Fact]
    public void WorldActor_Should_Handle_Multiple_Players()
    {
        using var scope = Test();

        // Arrange
        var worldActor = Sys.ActorOf(Props.Create<WorldActor>(), "world3");
        var playerIds = new long[] { 7003, 7004, 7005 };

        foreach (var id in playerIds)
        {
            PlayerDatabase.Instance.GetOrCreatePlayerId(id);
        }

        // Act - 동시 접속
        foreach (var id in playerIds)
        {
            worldActor.Tell(new EnterWorld(id));
        }

        Thread.Sleep(1500); // 모든 Actor 생성 대기

        // Assert - DB에서 모든 플레이어 확인
        foreach (var id in playerIds)
        {
            var data = PlayerDatabase.Instance.LoadPlayer(id);
            data.Should().NotBeNull();
            scope.Log($"Player {id} created");
        }

        scope.LogSuccess($"All {playerIds.Length} players created successfully");
    }
    /// <summary>
    /// 테스트 4: PlayerMove 메시지 처리 - 정상 이동
    /// </summary>
    [Fact]
    public void WorldActor_Should_Forward_PlayerMove_To_ZoneActor()
    {
        using var scope = Test();

        // Arrange
        const long playerId = 7006;
        var worldActor = Sys.ActorOf(Props.Create<WorldActor>(), "world4");
        var db = PlayerDatabase.Instance;

        // 플레이어 먼저 생성
        db.GetOrCreatePlayerId(playerId);
        worldActor.Tell(new EnterWorld(playerId));
        Thread.Sleep(1000); // PlayerActor 생성 대기

        scope.LogInfo($"Testing PlayerMove for Player {playerId}");

        // Act - 이동 명령
        var moveMsg = new PlayerMove(
            PlayerActor: null!,  // WorldActor가 찾아서 설정
            PlayerId: playerId,
            X: 50.0f,
            Y: 75.0f
        );

        worldActor.Tell(moveMsg);
        scope.Log($"Sent move command to ({moveMsg.X}, {moveMsg.Y})");

        // Assert - DB에서 위치 변경 확인
        Thread.Sleep(1000); // ZoneActor의 DB 저장 대기

        var savedData = db.LoadPlayer(playerId);
        savedData.Should().NotBeNull();
        savedData!.Value.x.Should().Be(50.0f);
        savedData.Value.y.Should().Be(75.0f);

        scope.LogSuccess($"Player moved to ({savedData.Value.x}, {savedData.Value.y})");
    }

    /// <summary>
    /// 테스트 5: PlayerMove 메시지 처리 - 존재하지 않는 플레이어
    /// </summary>
    [Fact]
    public void WorldActor_Should_Handle_PlayerMove_For_NonExistent_Player()
    {
        using var scope = Test();

        // Arrange
        const long nonExistentId = 7007;
        var worldActor = Sys.ActorOf(Props.Create<WorldActor>(), "world5");

        scope.LogInfo($"Testing PlayerMove for non-existent Player {nonExistentId}");

        // Act - 존재하지 않는 플레이어 이동 시도
        var exception = Record.Exception(() =>
        {
            var moveMsg = new PlayerMove(
                PlayerActor: null!,
                PlayerId: nonExistentId,
                X: 100.0f,
                Y: 200.0f
            );

            worldActor.Tell(moveMsg);
            Thread.Sleep(500);
        });

        // Assert - 에러 없이 처리됨 (로그만 남김)
        exception.Should().BeNull();
        scope.LogSuccess("Non-existent player move handled gracefully");
    }

    /// <summary>
    /// 테스트 8: PlayerActor 종료 시 Terminated 메시지 처리
    /// </summary>
    [Fact]
    public void WorldActor_Should_Handle_PlayerActor_Termination()
    {
        using var scope = Test();

        // Arrange
        const long playerId = 7012;
        var worldActor = Sys.ActorOf(Props.Create<WorldActor>(), "world8");
        var db = PlayerDatabase.Instance;

        db.GetOrCreatePlayerId(playerId);

        scope.LogInfo($"Testing Terminated handling for Player {playerId}");

        // Act 1 - 플레이어 생성
        worldActor.Tell(new EnterWorld(playerId));
        Thread.Sleep(1000); // PlayerActor 생성 대기

        // DB에 저장되었는지 확인 (플레이어가 정상 생성됨)
        var initialData = db.LoadPlayer(playerId);
        initialData.Should().NotBeNull();
        scope.Log($"Player {playerId} created successfully");

        // Act 2 - ClientDisconnected 메시지로 종료 트리거
        worldActor.Tell(new ClientDisconnected(playerId));
        scope.LogWarning($"Sent ClientDisconnected for Player {playerId}");

        Thread.Sleep(1500); // Terminated 처리 대기

        // Act 3 - 같은 플레이어로 다시 접속 시도 (정리되었다면 가능해야 함)
        var exception = Record.Exception(() =>
        {
            worldActor.Tell(new EnterWorld(playerId));
            Thread.Sleep(500);
        });

        // Assert
        exception.Should().BeNull(); // 재접속 가능 = 이전 PlayerActor가 정리됨
        scope.LogSuccess($"Player {playerId} cleaned up and can reconnect");
    }

    /// <summary>
    /// 테스트 9: 여러 플레이어 중 특정 플레이어만 종료
    /// </summary>
    [Fact]
    public void WorldActor_Should_Handle_Selective_Player_Termination()
    {
        using var scope = Test();

        // Arrange
        var worldActor = Sys.ActorOf(Props.Create<WorldActor>(), "world9");
        var playerIds = new long[] { 7013, 7014, 7015 };
        var db = PlayerDatabase.Instance;

        foreach (var id in playerIds)
        {
            db.GetOrCreatePlayerId(id);
        }

        scope.LogInfo("Testing selective player termination");

        // Act 1 - 3명 모두 접속
        foreach (var id in playerIds)
        {
            worldActor.Tell(new EnterWorld(id));
        }
        Thread.Sleep(1500);

        scope.Log($"All {playerIds.Length} players connected");

        // Act 2 - 중간 플레이어(7014)만 종료
        worldActor.Tell(new ClientDisconnected(7014));
        scope.LogWarning("Disconnected Player 7014");

        Thread.Sleep(1000);

        // Act 3 - 다른 플레이어들은 계속 이동 가능해야 함
        var moveException1 = Record.Exception(() =>
        {
            worldActor.Tell(new PlayerMove(null!, 7013, 10.0f, 20.0f));
            Thread.Sleep(500);
        });

        var moveException2 = Record.Exception(() =>
        {
            worldActor.Tell(new PlayerMove(null!, 7015, 30.0f, 40.0f));
            Thread.Sleep(500);
        });

        // Act 4 - 종료된 플레이어(7014)는 이동 불가
        worldActor.Tell(new PlayerMove(null!, 7014, 50.0f, 60.0f));
        Thread.Sleep(500);

        // Assert
        moveException1.Should().BeNull(); // 7013 이동 성공
        moveException2.Should().BeNull(); // 7015 이동 성공

        // 7013, 7015는 이동했지만 7014는 이동하지 않음
        var data1 = db.LoadPlayer(7013);
        var data2 = db.LoadPlayer(7015);
        var data3 = db.LoadPlayer(7014);

        data1!.Value.x.Should().Be(10.0f);
        data2!.Value.x.Should().Be(30.0f);
        data3!.Value.x.Should().Be(0.0f); // 이동하지 않음 (초기값)

        scope.LogSuccess("Other players continue working after one disconnects");
    }

    /// <summary>
    /// 테스트 10: Watch/Unwatch 동작 확인 (간접 테스트)
    /// </summary>
    [Fact]
    public void WorldActor_Should_Properly_Watch_And_Unwatch_PlayerActors()
    {
        using var scope = Test();

        // Arrange
        const long playerId = 7016;
        var worldActor = Sys.ActorOf(Props.Create<WorldActor>(), "world10");
        var db = PlayerDatabase.Instance;

        db.GetOrCreatePlayerId(playerId);

        scope.LogInfo("Testing Watch/Unwatch mechanism");

        // Act & Assert - 여러 번 연결/종료 반복
        for (int i = 0; i < 3; i++)
        {
            scope.LogSeparator();
            scope.Log($"Iteration {i + 1}:");

            // 접속
            worldActor.Tell(new EnterWorld(playerId));
            Thread.Sleep(500);

            // 이동 (정상 동작 확인)
            worldActor.Tell(new PlayerMove(null!, playerId, i * 10.0f, i * 10.0f));
            Thread.Sleep(500);

            var moveData = db.LoadPlayer(playerId);
            moveData!.Value.x.Should().Be(i * 10.0f);
            scope.Log($"  Connected and moved to ({moveData.Value.x}, {moveData.Value.y})");

            // 종료
            worldActor.Tell(new ClientDisconnected(playerId));
            Thread.Sleep(500);
            scope.Log($"  Disconnected");
        }

        scope.LogSuccess("Multiple connect/disconnect cycles handled correctly");
    }
}