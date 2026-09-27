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

    [Fact]
    public void Extend_WhenRangeTimerFinishedAtMinimum_ShouldAddUpToMaximum()
    {
        // Arrange
        var now = DateTimeOffset.UnixEpoch;
        var timer = CookTimerState.Start("Fry onion", 600, 720, now);

        // Act
        var extended = timer.Extend(120);
        var overExtended = extended.Extend(120);

        // Assert
        extended.EndsAt.Should().Be(now.AddSeconds(720));
        extended.TotalSeconds.Should().Be(720);
        extended.CanExtend.Should().BeFalse();
        overExtended.EndsAt.Should().Be(now.AddSeconds(720));
    }

    [Fact]
    public void Start_WhenSingleDuration_ShouldNotBeExtendable()
    {
        // Arrange
        var now = DateTimeOffset.UnixEpoch;

        // Act
        var timer = CookTimerState.Start("Boil", 300, now);

        // Assert
        timer.MaxSeconds.Should().Be(300);
        timer.CanExtend.Should().BeFalse();
    }

    [Fact]
    public void IsAtCheckpoint_WhenRangeTimerReachesItsMinimum_ShouldBeTrue()
    {
        // Arrange
        var now = DateTimeOffset.UnixEpoch;
        var timer = CookTimerState.Start("Fry onion", 600, 720, now);

        // Act & Assert
        timer.IsAtCheckpoint(now.AddSeconds(600)).Should().BeTrue();
    }

    [Fact]
    public void IsAtCheckpoint_BeforeTheMinimumIsReached_ShouldBeFalse()
    {
        // Arrange
        var now = DateTimeOffset.UnixEpoch;
        var timer = CookTimerState.Start("Fry onion", 600, 720, now);

        // Act & Assert
        timer.IsAtCheckpoint(now.AddSeconds(599)).Should().BeFalse();
    }

    [Fact]
    public void IsAtCheckpoint_WhenSingleDurationTimerFinishes_ShouldBeFalse()
    {
        // Arrange — nothing to extend towards, so its deadline is a genuine finish, never a checkpoint.
        var now = DateTimeOffset.UnixEpoch;
        var timer = CookTimerState.Start("Boil", 300, now);

        // Act & Assert
        timer.IsAtCheckpoint(now.AddSeconds(300)).Should().BeFalse();
    }

    [Fact]
    public void IsAtCheckpoint_WhenExtendedToItsMaximum_ShouldBeFalse()
    {
        // Arrange — extending a 600–720s range by 120s lands exactly on the maximum: no longer
        // extendable, so reaching that new deadline is a genuine finish, not another checkpoint.
        var now = DateTimeOffset.UnixEpoch;
        var timer = CookTimerState.Start("Fry onion", 600, 720, now).Extend(120);

        // Act & Assert
        timer.IsAtCheckpoint(now.AddSeconds(720)).Should().BeFalse();
        timer.HasFinished(now.AddSeconds(720)).Should().BeTrue();
    }

    [Fact]
    public void ShouldRestore_WhenDeadlineNotYetReached_ShouldBeTrue()
    {
        // Arrange — still running, regardless of whether it's a range or a single duration.
        var now = DateTimeOffset.UnixEpoch;
        var endsAt = now.AddSeconds(1);

        // Act & Assert
        CookTimerState.ShouldRestore(totalSeconds: 300, maxSeconds: 300, endsAt, now).Should().BeTrue();
    }

    [Fact]
    public void ShouldRestore_WhenSingleDurationTimerPastItsDeadline_ShouldBeFalse()
    {
        // Arrange — a genuinely finished timer already alerted via the platform notification while
        // the app was away; restoring it would just resurrect a done timer.
        var now = DateTimeOffset.UnixEpoch;
        var endsAt = now.AddSeconds(-1);

        // Act & Assert
        CookTimerState.ShouldRestore(totalSeconds: 300, maxSeconds: 300, endsAt, now).Should().BeFalse();
    }

    [Fact]
    public void ShouldRestore_WhenRangeTimerPastItsMinimumAndNotYetAtMaximum_ShouldBeTrue()
    {
        // Arrange — a range timer that reached its checkpoint while the app was away resumes right
        // there, at "Check now", rather than vanishing.
        var now = DateTimeOffset.UnixEpoch;
        var endsAt = now.AddSeconds(-1);

        // Act & Assert
        CookTimerState.ShouldRestore(totalSeconds: 600, maxSeconds: 720, endsAt, now).Should().BeTrue();
    }

    [Fact]
    public void ShouldRestore_WhenLegacyRecordHasNoMaxSeconds_PastDeadline_ShouldBeFalse()
    {
        // Arrange — pre-Task-18 JSON has no MaxSeconds (0 on deserialize); that must be treated as
        // equal to TotalSeconds (not extendable), never as a bogus "always extendable" 0.
        var now = DateTimeOffset.UnixEpoch;
        var endsAt = now.AddSeconds(-1);

        // Act & Assert
        CookTimerState.ShouldRestore(totalSeconds: 300, maxSeconds: 0, endsAt, now).Should().BeFalse();
    }
}
