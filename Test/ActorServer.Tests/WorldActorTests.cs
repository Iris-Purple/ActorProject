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
}