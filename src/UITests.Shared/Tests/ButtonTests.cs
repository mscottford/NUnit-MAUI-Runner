using NUnit.Framework;

namespace UITests;

public class ButtonTests : BaseTest
{
    [Test]
    public void AllResultsButtonOpensTheResultsList()
    {
        FindElement("AllResultsButton").Click();

        var list = WaitForElement("ResultsList");
        Assert.That(list.Displayed, Is.True);

        // Every test in the sample suite should be listed.
        var names = App.FindElements(
            OpenQA.Selenium.Appium.MobileBy.Id("ResultName"));
        Assert.That(names, Is.Not.Empty, "the results list rendered no rows");

        ScreenshotAssert.MatchesBaseline(App, "all-results");
        App.Navigate().Back();
        WaitForElement("TotalTestCount");
    }

    [Test]
    public void FailedResultsButtonOpensTheResultsList()
    {
        FindElement("FailedResultsButton").Click();

        var list = WaitForElement("ResultsList");
        Assert.That(list.Displayed, Is.True);

        App.Navigate().Back();
        WaitForElement("TotalTestCount");
    }

    // Re-running has to leave the same totals behind, which also proves the button is wired to
    // the command rather than just being present.
    [Test]
    public void RunButtonRerunsTheSuite()
    {
        FindElement("RunButton").Click();
        WaitForTestRunToComplete();

        Assert.Multiple(() =>
        {
            Assert.That(FindElement("TotalTestCount").Text, Is.EqualTo("8"));
            Assert.That(FindElement("PassCount").Text, Is.EqualTo("4"));
            Assert.That(FindElement("FailureCount").Text, Is.EqualTo("4"));
        });
    }

    [Test]
    public void RunFailedButtonRunsOnlyTheFailures()
    {
        FindElement("RunFailedButton").Click();

        // Only the four failing tests are re-run, so the totals shrink to just those.
        WaitForText("TotalTestCount", "4");
        Assert.Multiple(() =>
        {
            Assert.That(FindElement("FailureCount").Text, Is.EqualTo("4"));
            Assert.That(FindElement("PassCount").Text, Is.EqualTo("0"));
        });

        // With nothing passing the ring must be entirely red. A zero-count outcome used to
        // still draw a short arc, leaving a sliver of green here.
        ScreenshotAssert.MatchesBaseline(App, "run-failed-all-red");
    }
}
