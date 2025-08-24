using Akka.Actor;
using Akka.TestKit.Xunit2;
using Xunit.Abstractions;

namespace ActorServer.Tests.TestHelpers;

/// <summary>
/// Akka.NET Actor 테스트를 위한 베이스 클래스
/// ActorServer의 모든 Actor 테스트에서 사용
/// </summary>
public abstract class AkkaTestKitBase : TestKit
{
    // 변경: Output 제거 (TestKit에 이미 있음)
    // TestKit의 Output은 ITestOutputHelper 타입이므로 그대로 사용

    protected AkkaTestKitBase(ITestOutputHelper output) 
        : base("akka.loglevel = DEBUG", output: output)  // 부모 클래스에 output 전달
    {
        // 테스트 환경 설정 - DB가 테스트 DB를 사용하도록
        Environment.SetEnvironmentVariable("TEST_ENVIRONMENT", "true");
    }

    /// <summary>
    /// 테스트 종료 시 정리 작업
    /// </summary>
    protected override void AfterAll()
    {
        // ActorSystem 종료 대기
        Shutdown();
        base.AfterAll();
    }

    /// <summary>
    /// Actor가 특정 메시지를 받을 때까지 대기
    /// </summary>
    protected T ExpectMsgFrom<T>(IActorRef sender, TimeSpan? timeout = null)
    {
        var msg = ExpectMsg<T>(timeout);
        Assert.Equal(sender, LastSender);
        return msg;
    }

    /// <summary>
    /// 여러 메시지를 순서대로 검증
    /// </summary>
    protected void ExpectMsgsInOrder<T1, T2>()
    {
        ExpectMsg<T1>();
        ExpectMsg<T2>();
    }

    /// <summary>
    /// 테스트 로그 출력 헬퍼 메서드 추가
    /// </summary>
    protected void LogTest(string message)
    {
        // 변경: TestKit의 Output 속성 직접 사용
        Output?.WriteLine($"[TEST] {message}");
    }
}