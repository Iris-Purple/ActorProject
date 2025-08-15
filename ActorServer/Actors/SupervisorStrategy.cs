using Akka.Actor;
using ActorServer.Exceptions;

namespace ActorServer.Actors;


public static class GameServerStrategies
{
    public static SupervisorStrategy ForWorldActor()
    {
        return new OneForOneStrategy(
            maxNrOfRetries: 10, // 10번까지 재시도
            withinTimeMilliseconds: 60000,
            localOnlyDecider: ex =>
            {
                var actorName = "WorldActor";
                switch (ex)
                {
                    case TimeoutException _:
                        LogSupervision(actorName, "Restart", ex);
                        return Directive.Restart;

                    case ArgumentNullException _:
                    case ArgumentException _:
                        LogSupervision(actorName, "Resume", ex);
                        return Directive.Resume;

                    case OutOfMemoryException _:
                    case StackOverflowException _:
                        LogSupervision(actorName, "Stop", ex);
                        return Directive.Stop;

                    case GameLogicException _:
                        LogSupervision(actorName, "Resume", ex);
                        return Directive.Resume;

                    case TemporaryGameException _:
                        LogSupervision(actorName, "Restart", ex);
                        return Directive.Restart;

                    case CriticalGameException _:
                        LogSupervision(actorName, "Stop", ex);
                        return Directive.Stop;

                    default:
                        LogSupervision(actorName, "Restart (default)", ex);
                        return Directive.Restart;
                }
            });
    }
    public static SupervisorStrategy ForZoneManager()
    {
        return new OneForOneStrategy(
                maxNrOfRetries: 5,
                withinTimeMilliseconds: 30000,  // 30초
                localOnlyDecider: ex =>
                {
                    var actorName = "ZoneManager";

                    switch (ex)
                    {
                        // Zone 관련 예외
                        case ZoneException zoneEx:
                            Console.WriteLine($"[{actorName}] Zone {zoneEx.ZoneId} error: {zoneEx.Message}");
                            return Directive.Restart;

                        // Zone 과부하
                        case ZoneOverloadException _:
                            LogSupervision(actorName, "Restart", ex);
                            return Directive.Restart;

                        default:
                            LogSupervision(actorName, "Resume", ex);
                            return Directive.Resume;
                    }
                });
    }

    public static SupervisorStrategy ForZoneActor()
    {
        return new OneForOneStrategy(
            maxNrOfRetries: 3,
            withinTimeMilliseconds: 10000,  // 10초
            localOnlyDecider: ex =>
            {
                var actorName = "ZoneActor";

                switch (ex)
                {
                    // 플레이어 데이터 손상 - 재시작
                    case PlayerDataException _:
                        LogSupervision(actorName, "Restart", ex);
                        return Directive.Restart;

                    // 일반적인 게임 로직 오류 - 계속 진행
                    case GameLogicException _:
                        LogSupervision(actorName, "Resume", ex);
                        return Directive.Resume;

                    // 네트워크 오류 - 재시작
                    case System.Net.Sockets.SocketException _:
                        LogSupervision(actorName, "Restart", ex);
                        return Directive.Restart;

                    default:
                        LogSupervision(actorName, "Restart", ex);
                        return Directive.Restart;
                }
            });
    }


    private static void LogSupervision(string actorName, string action, Exception ex)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var color = action switch
        {
            "Restart" => ConsoleColor.Yellow,
            "Resume" => ConsoleColor.Green,
            "Stop" => ConsoleColor.Red,
            _ => ConsoleColor.Gray
        };

        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine($"[{timestamp}] [Supervision] {actorName} -> {action}: {ex.GetType().Name} - {ex.Message}");
        Console.ForegroundColor = originalColor;
    }

}