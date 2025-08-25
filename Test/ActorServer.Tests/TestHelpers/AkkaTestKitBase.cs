using Akka.Actor;
using Akka.TestKit.Xunit2;
using Xunit.Abstractions;
using System.Runtime.CompilerServices;

namespace ActorServer.Tests.TestHelpers;

/// <summary>
/// Akka.NET Actor 테스트를 위한 베이스 클래스
/// </summary>
public abstract class AkkaTestKitBase : TestKit
{
    protected AkkaTestKitBase(ITestOutputHelper output)
        : base(@"
            akka {
                loglevel = WARNING
                stdout-loglevel = WARNING
                log-dead-letters = off
                log-dead-letters-during-shutdown = off
                actor {
                    debug {
                        receive = off
                        autoreceive = off
                        lifecycle = off
                        event-stream = off
                        unhandled = off
                    }
                }
            }
        ", output: output)
    {
        // 테스트 환경 설정 - DB가 테스트 DB를 사용하도록
        Environment.SetEnvironmentVariable("TEST_ENVIRONMENT", "true");
    }

    /// <summary>
    /// 테스트 종료 시 정리 작업
    /// </summary>
    protected override void AfterAll()
    {
        Shutdown();
        base.AfterAll();
    }

    // ========================================
    // 자동 로깅을 위한 메서드들
    // ========================================

    /// <summary>
    /// 테스트 시작 - 기본 설정 (100ms Actor 대기)
    /// </summary>
    protected TestScope Test(int milliseconds = 100, [CallerMemberName] string testName = "")
        => new TestScope(Output, testName, milliseconds);

    /// <summary>
    /// 테스트 시작 - 긴 Actor 대기 (복잡한 Actor 테스트용)
    /// </summary>
    protected TestScope SlowTest([CallerMemberName] string testName = "")
        => new TestScope(Output, testName, 2000);


    // ========================================
    // TestScope - 자동 시작/종료 로깅
    // ========================================

    protected class TestScope : IDisposable
    {
        private readonly ITestOutputHelper? _output;
        private readonly string _testName;
        private readonly DateTime _startTime;
        private readonly int _actorWaitTime;
        private bool _disposed;

        public TestScope(ITestOutputHelper? output, string testName, int actorWaitTime = 100)
        {
            _output = output;
            _testName = testName;
            _startTime = DateTime.Now;
            _actorWaitTime = actorWaitTime;

            // 테스트 시작 로그
            PrintHeader();
        }
        public void WaitForActors()
        {
            if (_actorWaitTime > 0)
            {
                LogWithColor($"⏳ Waiting {_actorWaitTime}ms for actors...", ConsoleColor.DarkGray);
                Thread.Sleep(_actorWaitTime);
            }
        }

        private void PrintHeader()
        {
            var border = new string('=', 70);
            var header = new[]
            {
                border,
                $"📝 TEST: {_testName}",
                $"⏰ START: {_startTime:yyyy-MM-dd HH:mm:ss.fff}",
                border
            };

            // 콘솔과 xUnit 둘 다 출력
            foreach (var line in header)
            {
                _output?.WriteLine(line);
                Console.WriteLine(line);
            }
        }

        /// <summary>
        /// 테스트 중간 로그 - 콘솔과 xUnit 둘 다 출력
        /// </summary>
        public void Log(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logMessage = $"[{timestamp}] {message}";

            _output?.WriteLine(logMessage);
            Console.WriteLine(logMessage);
        }

        /// <summary>
        /// 컬러 로그 - 콘솔에만 색상 적용
        /// </summary>
        public void LogWithColor(string message, ConsoleColor color)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logMessage = $"[{timestamp}] {message}";

            _output?.WriteLine(logMessage);

            Console.ForegroundColor = color;
            Console.WriteLine(logMessage);
            Console.ResetColor();
        }

        /// <summary>
        /// 성공 로그 (초록색)
        /// </summary>
        public void LogSuccess(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logMessage = $"[{timestamp}] ✅ {message}";

            _output?.WriteLine(logMessage);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(logMessage);
            Console.ResetColor();
        }

        /// <summary>
        /// 경고 로그 (노란색)
        /// </summary>
        public void LogWarning(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logMessage = $"[{timestamp}] ⚠️  {message}";

            _output?.WriteLine(logMessage);

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(logMessage);
            Console.ResetColor();
        }

        /// <summary>
        /// 에러 로그 (빨간색)
        /// </summary>
        public void LogError(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logMessage = $"[{timestamp}] ❌ {message}";

            _output?.WriteLine(logMessage);

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(logMessage);
            Console.ResetColor();
        }

        /// <summary>
        /// 정보 로그 (파란색)
        /// </summary>
        public void LogInfo(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logMessage = $"[{timestamp}] ℹ️  {message}";

            _output?.WriteLine(logMessage);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(logMessage);
            Console.ResetColor();
        }

        /// <summary>
        /// 구분선 출력
        /// </summary>
        public void LogSeparator()
        {
            var separator = new string('-', 70);
            _output?.WriteLine(separator);
            Console.WriteLine(separator);
        }

        public void Dispose()
        {
            if (_disposed) return;

            WaitForActors();

            // 테스트 종료 로그
            var elapsed = DateTime.Now - _startTime;
            var footer = new[]
            {
                new string('-', 70),
                $"✅ COMPLETED: {_testName}",
                $"⏱️  ELAPSED: {elapsed.TotalMilliseconds:F2}ms",
                new string('=', 70),
                ""
            };

            // 콘솔과 xUnit 둘 다 출력
            foreach (var line in footer)
            {
                _output?.WriteLine(line);

                if (line.Contains("COMPLETED"))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(line);
                    Console.ResetColor();
                }
                else
                {
                    Console.WriteLine(line);
                }
            }

            _disposed = true;
        }
    }

    // ========================================
    // TestKit 헬퍼 메서드들
    // ========================================

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
    /// 여러 메시지를 순서대로 검증 (3개)
    /// </summary>
    protected void ExpectMsgsInOrder<T1, T2, T3>()
    {
        ExpectMsg<T1>();
        ExpectMsg<T2>();
        ExpectMsg<T3>();
    }

    // ========================================
    // 간단한 로그 메서드 (Test() 없이 사용할 때)
    // ========================================

    /// <summary>
    /// 테스트 로그 출력 (콘솔 + xUnit)
    /// </summary>
    protected void LogTest(string message, [CallerMemberName] string method = "")  // 변경: Log -> LogTest
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var logMessage = $"[{timestamp}] [{method}] {message}";

        Output?.WriteLine(logMessage);
        Console.WriteLine(logMessage);
    }

    /// <summary>
    /// 간단한 로그 (TestScope 없이 사용)
    /// </summary>
    protected void WriteLog(string message)  // 변경: 새로운 이름
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var logMessage = $"[{timestamp}] {message}";

        Output?.WriteLine(logMessage);
        Console.WriteLine(logMessage);
    }

    /// <summary>
    /// 성공 로그 (콘솔은 초록색)
    /// </summary>
    protected void LogTestSuccess(string message, [CallerMemberName] string method = "")  // 변경: LogSuccess -> LogTestSuccess
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var logMessage = $"[{timestamp}] [{method}] ✅ {message}";

        Output?.WriteLine(logMessage);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(logMessage);
        Console.ResetColor();
    }

    /// <summary>
    /// 에러 로그 (콘솔은 빨간색)
    /// </summary>
    protected void LogTestError(string message, [CallerMemberName] string method = "")  // 변경: LogError -> LogTestError
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var logMessage = $"[{timestamp}] [{method}] ❌ {message}";

        Output?.WriteLine(logMessage);

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(logMessage);
        Console.ResetColor();
    }

    /// <summary>
    /// 경고 로그 (콘솔은 노란색)
    /// </summary>
    protected void LogTestWarning(string message, [CallerMemberName] string method = "")  // 추가
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var logMessage = $"[{timestamp}] [{method}] ⚠️  {message}";

        Output?.WriteLine(logMessage);

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(logMessage);
        Console.ResetColor();
    }

    /// <summary>
    /// 정보 로그 (콘솔은 파란색)
    /// </summary>
    protected void LogTestInfo(string message, [CallerMemberName] string method = "")  // 추가
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var logMessage = $"[{timestamp}] [{method}] ℹ️  {message}";

        Output?.WriteLine(logMessage);

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(logMessage);
        Console.ResetColor();
    }
}