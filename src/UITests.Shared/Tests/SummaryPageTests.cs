using NUnit.Framework;

namespace UITests;

// The app runs its suite on launch (AutoRun), so by the time these assertions run the summary
// page is already populated.
public class SummaryPageTests : BaseTest
{
    [Test]
    public void SummaryShowsTheExpectedCounts()
    {
        // The sample suite in Tests.cs is deliberately half-failing.
        Assert.Multiple(() =>
        {
            Assert.That(FindElement("TotalTestCount").Text, Is.EqualTo("8"));
            Assert.That(FindElement("PassCount").Text, Is.EqualTo("4"));
            Assert.That(FindElement("FailureCount").Text, Is.EqualTo("4"));
            Assert.That(FindElement("ErrorCount").Text, Is.EqualTo("0"));
            Assert.That(FindElement("NotRunCount").Text, Is.EqualTo("0"));
        });
    }

    [Test]
    public void OverallResultReflectsTheFailures()
    {
        Assert.That(FindElement("OverallResult").Text, Does.Contain("Failed"));
    }

    // Guards the bug where the chart's geometry was centred on (0,0), which clipped it down to
    // a three-pixel sliver. Its rendered size is the cheap, stable half of that check.
    [Test]
    public void ChartIsRenderedAtItsRequestedSize()
    {
        var chart = FindElement("ResultsChart");
        var size = chart.Size;

        Assert.Multiple(() =>
        {
            Assert.That(size.Width, Is.GreaterThan(50), "chart is far narrower than requested");
            Assert.That(size.Height, Is.GreaterThan(50), "chart is far shorter than requested");
            Assert.That(
                (double)size.Width / size.Height,
                Is.EqualTo(1.0).Within(0.15),
                "chart should be square");
        });
    }

    // The shape of the ring is only visible in pixels, so this is what catches a clipped or
    // mis-proportioned chart.
    [Test]
    public void SummaryPageMatchesBaseline()
    {
        ScreenshotAssert.MatchesBaseline(App, "summary-page");
    }
}
