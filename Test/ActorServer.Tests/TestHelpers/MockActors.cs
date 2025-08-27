using Akka.Actor;
using ActorServer.Messages;

namespace ActorServer.Tests.TestHelpers;

/// <summary>
/// 테스트용 Mock Client Actor
/// PlayerActor로부터 메시지를 받아 TestProbe로 전달
/// </summary>
public class MockClientActor : ReceiveActor
{
    private readonly IActorRef _probe;

    public MockClientActor(IActorRef probe)
    {
        _probe = probe;

        // PlayerActor가 보내는 모든 메시지를 TestProbe로 전달
        ReceiveAny(msg => _probe.Forward(msg));
    }
}

/// <summary>
/// 테스트용 Mock Zone Actor
/// </summary>
public class MockZoneActor : ReceiveActor
{
    private readonly IActorRef _probe;

    public MockZoneActor(IActorRef probe)
    {
        _probe = probe;

        // Zone 관련 메시지를 TestProbe로 전달
        ReceiveAny(msg => _probe.Forward(msg));
    }
}