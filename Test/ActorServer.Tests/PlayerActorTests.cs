using Xunit;
using Xunit.Abstractions;
using ActorServer.Actors;
using ActorServer.Messages;
using ActorServer.Tests.TestHelpers;
using FluentAssertions;
using Akka.Actor;

namespace ActorServer.Tests.Actors;

[Collection("ActorTests")]
public class PlayerActorTests : AkkaTestKitBase
{
    public PlayerActorTests(ITestOutputHelper output) : base(output) { }

}