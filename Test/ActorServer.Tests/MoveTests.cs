using Xunit;
using Xunit.Abstractions;
using Akka.TestKit.Xunit2;
using Akka.Actor;
using ActorServer.Actors;
using ActorServer.Tests.TestHelpers;
using ActorServer.Messages;
using System;
using FluentAssertions;

namespace ActorServer.Tests
{
    // 메시지 라우터 Actor
    public class MessageRouterActor : ReceiveActor
    {
        public MessageRouterActor(IActorRef movementProbe, IActorRef chatProbe, IActorRef systemProbe)
        {
            Receive<ChatToClient>(msg =>
            {
                // 메시지 내용에 따라 다른 Probe로 라우팅
                if (msg.Message.Contains("Moved to"))
                {
                    movementProbe.Tell(msg);
                }
                else if (msg.From == "System")
                {
                    systemProbe.Tell(msg);
                }
                else
                {
                    chatProbe.Tell(msg);
                }
            });
        }
    }

    /// <summary>
    /// 플레이어 이동 기능 테스트
    /// </summary>
    [Collection("ActorTest")]
    public class MoveTests : AkkaTestKitBase
    {
        public MoveTests(ITestOutputHelper output) : base(output) { }

        /// <summary>
        /// 플레이어가 유효한 좌표로 이동할 수 있는지 테스트
        /// </summary>
        [Fact]
        public void Player_Should_Move_To_Valid_Position()
        {
            using var test = Test();

            // Arrange
            var zoneManager = Sys.ActorOf(Props.Create<ZoneManager>(), "zone-manager-register");
            var playerId = 1001L;
            var playerProbe = CreateTestProbe("player-probe");

            var playerActor = Sys.ActorOf(
                Props.Create<PlayerActor>(playerId),
                $"test-player-{playerId}");

            test.LogInfo($"Registering player {playerId}");
            // Act
            zoneManager.Tell(new RegisterPlayer(playerId, playerActor));
            Thread.Sleep(100);

            var newPosition = new Position(50f, 75f);
            test.LogInfo($"테스트 이동 좌표: ({newPosition.X}, {newPosition.Y})");

            // Act - 실행
            playerActor.Tell(new MoveCommand(newPosition), playerProbe);
            playerProbe.ExpectMsg<ZoneMessageResult>(
                msg => msg.Success == true,
                TimeSpan.FromMilliseconds(100));

            var invalidPosition = new Position(300, 100);
            test.LogError($"잘못된 경로 위치 이동시 Exception 발생 : {invalidPosition}");
            playerActor.Tell(new MoveCommand(invalidPosition), playerProbe);
        }
    }
}