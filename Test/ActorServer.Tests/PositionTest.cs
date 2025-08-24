using ActorServer.Messages;
using FluentAssertions;
using Xunit;

namespace ActorServer.Tests.Unit;

public class PositionTests
{
    [Fact]
    public void Position_Should_Be_Created_With_Valid_Coordinates()
    {
        // Arrange & Act - 준비 및 실행
        var position = new Position(10.5f, 20.3f);

        // Assert - 검증
        position.X.Should().Be(10.5f);
        position.Y.Should().Be(20.3f);
    }
    [Fact]
    public void Position_Should_Detect_Invalid_NaN_Values()
    {
        var invalidPosition = new Position(float.NaN, 10f);
        var isValid = invalidPosition.IsValid();
        isValid.Should().BeFalse("NaN is not a valid coordinate");
    }
    [Theory]  // 여러 테스트 케이스를 한번에 테스트
    [InlineData(0, 0, 3, 4, 5)]      // 3-4-5 삼각형
    [InlineData(0, 0, 0, 10, 10)]    // 수직선
    [InlineData(5, 5, 5, 5, 0)]      // 같은 점
    public void Position_Should_Calculate_Distance_Correctly(
        float x1, float y1, float x2, float y2, float expectedDistance)
    {
        var pos1 = new Position(x1, y1);
        var pos2 = new Position(x2, y2);

        var distance = pos1.DistanceTo(pos2);
        distance.Should().BeApproximately(expectedDistance, 0.01f);
    }
    [Fact]
    public void Position_should_Detect_Infinity_As_Invalid()
    {
        var positions = new[]
        {
            new Position(float.PositiveInfinity, 0),
            new Position(0, float.NegativeInfinity),
            new Position(float.NaN, float.NaN),
        };
        foreach (var pos in positions)
        {
            pos.IsValid().Should().BeFalse(
                $"Position ({pos.X}, {pos.Y}) should be invalid"
            );
        }
    }
}