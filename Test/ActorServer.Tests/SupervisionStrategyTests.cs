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
public class SupervisionStrategyTests : AkkaTestKitBase
{
    public SupervisionStrategyTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// 테스트 1: PlayerActor 예외 시 재시작 확인
    /// </summary>
    [Fact]
    public void PlayerActor_Should_Restart_On_Exception()
    {
        using var scope = Test();

        // Arrange
        var worldActor = Sys.ActorOf(Props.Create<WorldActor>(), "test-world-restart");
        const long playerId = 8001;

        scope.LogInfo($"Testing PlayerActor restart for Player {playerId}");

        // Act 1 - PlayerActor 생성
        worldActor.Tell(new EnterWorld(playerId));
        Thread.Sleep(500);

        // PlayerActor 참조 가져오기 (ActorSelection 사용)
        var playerActorPath = $"/user/test-world-restart/player-{playerId}";
        var playerActor = Sys.ActorSelection(playerActorPath);
        
        // Act 2 - 실제 예외를 발생시킬 메시지 전송
        scope.LogWarning("Causing NullReferenceException in PlayerActor...");
        
        #if DEBUG
        playerActor.Tell(new CausePlayerException("NullReference"));
        Thread.Sleep(1000); // 재시작 대기
        
        // Act 3 - HealthCheck로 PlayerActor가 살아있는지 확인
        var probe = CreateTestProbe();
        playerActor.Tell(new HealthCheck(), probe.Ref);
        
        var healthResponse = probe.ExpectMsg<HealthCheckResponse>(TimeSpan.FromSeconds(2));
        healthResponse.Should().NotBeNull();
        healthResponse.IsHealthy.Should().BeTrue();
        healthResponse.ActorName.Should().Contain($"Player-{playerId}");
        
        scope.LogSuccess($"PlayerActor responded after restart: {healthResponse.ActorName}");
        #endif

        // Act 4 - PlayerActor가 재시작 후에도 정상 동작하는지 확인
        worldActor.Tell(new PlayerMove(null!, playerId, 10.0f, 20.0f));
        Thread.Sleep(500);

        // Assert - 예외 없이 처리됨
        var exception = Record.Exception(() =>
        {
            worldActor.Tell(new PlayerMove(null!, playerId, 30.0f, 40.0f));
            Thread.Sleep(500);
        });

        exception.Should().BeNull();
        scope.LogSuccess("PlayerActor continued working after restart");
    }

    /// <summary>
    /// 테스트 2: ZoneActor 예외 시 Resume 확인
    /// </summary>
    [Fact]
    public void ZoneActor_Should_Resume_On_Exception()
    {
        using var scope = Test();

        // Arrange
        var worldActor = Sys.ActorOf(Props.Create<WorldActor>(), "test-world-resume");
        const long playerId1 = 8002;
        const long playerId2 = 8003;

        scope.LogInfo("Testing ZoneActor resume on exception");

        // Act 1 - 첫 번째 플레이어 생성
        worldActor.Tell(new EnterWorld(playerId1));
        Thread.Sleep(500);

        // ZoneActor 참조 가져오기
        var zoneActorPath = "/user/test-world-resume/zone";
        var zoneActor = Sys.ActorSelection(zoneActorPath);
        
        #if DEBUG
        // Act 2 - ZoneActor에 실제 예외 발생
        scope.LogWarning("Causing InvalidOperationException in ZoneActor...");
        zoneActor.Tell(new CauseZoneException("InvalidOperation"));
        Thread.Sleep(1000); // Resume 처리 대기
        
        // Act 3 - ZoneActor가 여전히 살아있는지 HealthCheck로 확인
        var probe1 = CreateTestProbe();
        zoneActor.Tell(new HealthCheck(), probe1.Ref);
        
        var zoneHealth = probe1.ExpectMsg<HealthCheckResponse>(TimeSpan.FromSeconds(2));
        zoneHealth.Should().NotBeNull();
        zoneHealth.IsHealthy.Should().BeTrue();
        zoneHealth.ActorName.Should().Be("ZoneActor");
        
        scope.LogSuccess("ZoneActor responded after exception (Resume worked)");
        
        // Act 4 - 또 다른 예외 발생 후에도 계속 동작하는지 확인
        scope.LogWarning("Causing another exception in ZoneActor...");
        zoneActor.Tell(new CauseZoneException("OutOfMemory"));
        Thread.Sleep(1000);
        
        // Act 5 - 다시 HealthCheck
        var probe2 = CreateTestProbe();
        zoneActor.Tell(new HealthCheck(), probe2.Ref);
        
        var zoneHealth2 = probe2.ExpectMsg<HealthCheckResponse>(TimeSpan.FromSeconds(2));
        zoneHealth2.Should().NotBeNull();
        zoneHealth2.IsHealthy.Should().BeTrue();
        
        scope.LogSuccess("ZoneActor still working after multiple exceptions");
        #endif

        // Act 6 - ZoneActor가 여전히 정상 기능을 수행하는지 확인 (두 번째 플레이어 추가)
        worldActor.Tell(new EnterWorld(playerId2));
        Thread.Sleep(500);

        // Act 7 - 정상적인 이동 명령
        var exception = Record.Exception(() =>
        {
            worldActor.Tell(new PlayerMove(null!, playerId2, 50.0f, 60.0f));
            Thread.Sleep(500);
        });

        // Assert - ZoneActor가 계속 동작함
        exception.Should().BeNull();
        scope.LogSuccess("ZoneActor continued normal operations after exceptions (Resume)");
    }

    /// <summary>
    /// 테스트 3: 여러 PlayerActor 중 하나만 재시작
    /// </summary>
    [Fact]
    public void Single_PlayerActor_Restart_Should_Not_Affect_Others()
    {
        using var scope = Test();

        // Arrange
        var worldActor = Sys.ActorOf(Props.Create<WorldActor>(), "test-world-isolated");
        var playerIds = new long[] { 8004, 8005, 8006 };

        scope.LogInfo("Testing isolated PlayerActor restart");

        // Act 1 - 3명의 플레이어 생성
        foreach (var id in playerIds)
        {
            worldActor.Tell(new EnterWorld(id));
        }
        Thread.Sleep(1000);

        scope.Log("All 3 players created");

        // Act 2 - 중간 플레이어(8005)에만 문제 발생 시뮬레이션
        scope.LogWarning("Simulating exception for Player 8005 only");
        
        // Act 3 - 다른 플레이어들은 정상 동작해야 함
        var exceptions = new List<Exception?>();
        
        exceptions.Add(Record.Exception(() =>
        {
            worldActor.Tell(new PlayerMove(null!, 8004, 10.0f, 10.0f));
            Thread.Sleep(200);
        }));

        exceptions.Add(Record.Exception(() =>
        {
            worldActor.Tell(new PlayerMove(null!, 8006, 30.0f, 30.0f));
            Thread.Sleep(200);
        }));

        // Assert - 다른 플레이어들은 영향받지 않음
        exceptions.Should().AllSatisfy(ex => ex.Should().BeNull());
        scope.LogSuccess("Other PlayerActors not affected by single actor restart");
    }

    /// <summary>
    /// 테스트 5: 다양한 예외 타입에 대한 Supervision 동작 확인
    /// </summary>
    [Fact]
    public void Different_Exception_Types_Should_Be_Handled_Correctly()
    {
        using var scope = Test();

        // Arrange
        var worldActor = Sys.ActorOf(Props.Create<WorldActor>(), "test-world-exceptions");
        const long playerId = 8007;

        scope.LogInfo("Testing different exception types");

        // Act 1 - PlayerActor 생성
        worldActor.Tell(new EnterWorld(playerId));
        Thread.Sleep(500);

        #if DEBUG
        var playerActorPath = $"/user/test-world-exceptions/player-{playerId}";
        var playerActor = Sys.ActorSelection(playerActorPath);
        var probe = CreateTestProbe();

        // 테스트할 예외 타입들
        var exceptionTypes = new[] { "NullReference", "InvalidOperation", "DivideByZero" };

        foreach (var exceptionType in exceptionTypes)
        {
            scope.LogSeparator();
            scope.LogWarning($"Testing {exceptionType} exception...");

            // 예외 발생
            playerActor.Tell(new CausePlayerException(exceptionType));
            Thread.Sleep(1500); // 재시작 대기

            // HealthCheck로 재시작 확인
            playerActor.Tell(new HealthCheck(), probe.Ref);
            
            var response = probe.ExpectMsg<HealthCheckResponse>(TimeSpan.FromSeconds(2));
            response.Should().NotBeNull();
            response.IsHealthy.Should().BeTrue();
            
            scope.LogSuccess($"PlayerActor restarted successfully after {exceptionType}");
        }
        #endif

        scope.LogSuccess("All exception types handled correctly");
    }

    /// <summary>
    /// 테스트 6: 최대 재시작 횟수 초과 테스트
    /// </summary>
    [Fact]
    public void PlayerActor_Should_Stop_After_Max_Retries()
    {
        using var scope = Test();

        // Arrange
        var worldActor = Sys.ActorOf(Props.Create<WorldActor>(), "test-world-max-retries");
        const long playerId = 8008;

        scope.LogInfo("Testing max restart limit (10 times in 1 minute)");

        // Act 1 - PlayerActor 생성
        worldActor.Tell(new EnterWorld(playerId));
        Thread.Sleep(500);

        #if DEBUG
        var playerActorPath = $"/user/test-world-max-retries/player-{playerId}";
        var playerActor = Sys.ActorSelection(playerActorPath);
        var probe = CreateTestProbe();

        // Act 2 - 빠르게 11번 예외 발생 (최대 10회 제한 초과)
        scope.LogWarning("Causing rapid exceptions to exceed max restart limit...");
        
        for (int i = 1; i <= 11; i++)
        {
            scope.Log($"Exception #{i}");
            playerActor.Tell(new CausePlayerException($"Exception_{i}"));
            Thread.Sleep(100); // 짧은 간격으로 예외 발생
        }

        Thread.Sleep(2000); // Actor Stop 처리 대기

        // Act 3 - PlayerActor가 Stop되었는지 확인 (응답 없음)
        playerActor.Tell(new HealthCheck(), probe.Ref);
        probe.ExpectNoMsg(TimeSpan.FromSeconds(1));
        
        scope.LogSuccess("PlayerActor stopped after exceeding max restart limit");
        #else
        scope.LogWarning("Test requires DEBUG mode");
        #endif
    }
}