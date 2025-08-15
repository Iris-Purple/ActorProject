namespace ActorServer.Messages
{
    // 테스트 전용 메시지
    public record TestNullCommand();  // null 커맨드 시뮬레이션
    public record TestInvalidData(string Data);
}
