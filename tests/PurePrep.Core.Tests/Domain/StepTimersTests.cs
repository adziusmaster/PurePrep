using FluentAssertions;
using PurePrep.Domain;

namespace PurePrep.Core.Tests.Domain;

/// <summary>
/// Detects tappable cook timers inside step text across the app's languages. A tester reported that
/// "leave 30 mins to cool"/rest style steps produced no timer once imported, because filler words
/// (e.g. Romanian "30 de minute") sat between the number and the unit. These cases lock in that
/// behaviour without regressing the plain "20 min" form.
/// </summary>
public sealed class StepTimersTests
{
    [Theory]
    [InlineData("Bake for 20 min until golden.", 20 * 60)]
    [InlineData("Leave 30 mins to cool.", 30 * 60)]
    [InlineData("Simmer for 1.5 hours.", 90 * 60)]
    [InlineData("Rest 45 seconds before serving.", 45)]
    public void DetectsPlainDurations(string step, int expectedSeconds)
    {
        StepTimers.Detect(step).Should().ContainSingle()
            .Which.TotalSeconds.Should().Be(expectedSeconds);
    }

    [Theory]
    [InlineData("Lăsați 30 de minute să se răcească.", 30 * 60)] // Romanian filler "de"
    [InlineData("Cuocere per 2 di ore.", 2 * 3600)]              // Italian filler "di"
    [InlineData("Leave a couple of 10 minutes.. wait 15 of minutes.", 15 * 60)] // English filler "of"
    public void DetectsDurationsWithFillerWords(string step, int expectedSeconds)
    {
        StepTimers.Detect(step).Should().Contain(t => t.TotalSeconds == expectedSeconds);
    }

    [Fact]
    public void ReturnsEmpty_WhenNoDuration()
    {
        StepTimers.Detect("Season the onion to taste.").Should().BeEmpty();
    }
}
