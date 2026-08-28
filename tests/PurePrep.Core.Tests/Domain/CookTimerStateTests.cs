using FluentAssertions;
using PurePrep.Domain;

namespace PurePrep.Core.Tests.Domain;

/// <summary>
/// A running cook timer, expressed as a deadline rather than a countdown.
///
/// The original implementation decremented a counter on each dispatcher tick, so the display drifted
/// and was simply wrong after the app had been backgrounded — the ticks stop, the clock does not.
/// Anchoring on an end time makes the remaining figure correct whenever it is next read.
/// </summary>
public sealed class CookTimerStateTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    private static CookTimerState Timer(int totalSeconds, int elapsedSeconds = 0) =>
        new("20 min", totalSeconds, Now.AddSeconds(totalSeconds - elapsedSeconds));

    [Fact]
    public void RemainingSeconds_AtTheStart_ShouldBeTheFullDuration()
    {
        // Act & Assert
        Timer(1200).RemainingSeconds(Now).Should().Be(1200);
    }

    [Fact]
    public void RemainingSeconds_PartWayThrough_ShouldCountDown()
    {
        // Act & Assert
        Timer(1200, elapsedSeconds: 300).RemainingSeconds(Now).Should().Be(900);
    }

    [Fact]
    public void RemainingSeconds_AfterALongBackgrounding_ShouldReflectRealElapsedTime()
    {
        // Arrange — the exact case the old tick-counter got wrong.
        var timer = Timer(1200);

        // Act
        var remaining = timer.RemainingSeconds(Now.AddMinutes(15));

        // Assert
        remaining.Should().Be(300);
    }

    [Fact]
    public void RemainingSeconds_PastTheDeadline_ShouldClampToZero()
    {
        // Act & Assert
        Timer(600).RemainingSeconds(Now.AddHours(3)).Should().Be(0);
    }

    [Fact]
    public void HasFinished_BeforeTheDeadline_ShouldBeFalse()
    {
        // Act & Assert
        Timer(600).HasFinished(Now.AddSeconds(599)).Should().BeFalse();
    }

    [Fact]
    public void HasFinished_AtTheDeadline_ShouldBeTrue()
    {
        // Act & Assert
        Timer(600).HasFinished(Now.AddSeconds(600)).Should().BeTrue();
    }

    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(9, "00:09")]
    [InlineData(65, "01:05")]
    [InlineData(1200, "20:00")]
    [InlineData(3599, "59:59")]
    [InlineData(3600, "1:00:00")]
    [InlineData(5445, "1:30:45")]
    public void Clock_ShouldFormatForGlanceability(int seconds, string expected)
    {
        // Act & Assert
        CookTimerState.Clock(seconds).Should().Be(expected);
    }

    [Fact]
    public void Progress_ShouldRunFromZeroToOne()
    {
        // Arrange
        var timer = Timer(1000);

        // Act & Assert
        timer.Progress(Now).Should().BeApproximately(0, 0.001);
        timer.Progress(Now.AddSeconds(500)).Should().BeApproximately(0.5, 0.01);
        timer.Progress(Now.AddSeconds(1000)).Should().BeApproximately(1, 0.001);
    }

    [Fact]
    public void Progress_PastTheDeadline_ShouldNotExceedOne()
    {
        // Act & Assert
        Timer(600).Progress(Now.AddHours(1)).Should().Be(1);
    }

    [Fact]
    public void Start_ShouldAnchorTheDeadlineOnTheGivenTime()
    {
        // Act
        var timer = CookTimerState.Start("20 min", 1200, Now);

        // Assert
        timer.EndsAt.Should().Be(Now.AddSeconds(1200));
        timer.TotalSeconds.Should().Be(1200);
        timer.Label.Should().Be("20 min");
    }
}
