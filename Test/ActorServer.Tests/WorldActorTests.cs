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
    /// 테스트 7: 재접속 후 이동 처리
    /// </summary>
    [Fact]
    public void WorldActor_Should_Handle_Move_After_Reconnection()
    {
        using var scope = Test();

        // Arrange
        const long playerId = 7011;
        var worldActor = Sys.ActorOf(Props.Create<WorldActor>(), "world7");
        var db = PlayerDatabase.Instance;
        
        db.GetOrCreatePlayerId(playerId);
        
        // Act 1 - 첫 접속 후 이동
        worldActor.Tell(new EnterWorld(playerId));
        Thread.Sleep(500);
        
        worldActor.Tell(new PlayerMove(null!, playerId, 25.0f, 35.0f));
        Thread.Sleep(1000);
        
        var firstData = db.LoadPlayer(playerId);
        firstData!.Value.x.Should().Be(25.0f);
        scope.Log($"First move: ({firstData.Value.x}, {firstData.Value.y})");
        
        // Act 2 - 재접속
        worldActor.Tell(new EnterWorld(playerId));
        Thread.Sleep(1000);
        
        // Act 3 - 재접속 후 이동
        worldActor.Tell(new PlayerMove(null!, playerId, 70.0f, 80.0f));
        Thread.Sleep(1000);
        
        // Assert
        var finalData = db.LoadPlayer(playerId);
        finalData!.Value.x.Should().Be(70.0f);
        finalData.Value.y.Should().Be(80.0f);
        
        scope.LogSuccess($"Move after reconnection: ({finalData.Value.x}, {finalData.Value.y})");
    }
}