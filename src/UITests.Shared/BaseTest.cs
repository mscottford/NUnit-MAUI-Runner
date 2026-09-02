using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Support.UI;

namespace UITests;

public abstract class BaseTest
{
    protected static AppiumDriver App => AppiumSetup.App;

    // The platform name used to pick a baseline directory, so android and ios keep separate
    // reference images.
    protected static string PlatformName => AppiumSetup.PlatformName;

    protected static AppiumElement FindElement(string automationId)
    {
        return App.FindElement(MobileBy.Id(automationId));
    }

    // The app runs its suite on launch, so tests generally need to wait for an element rather
    // than assume it is on screen already.
    protected static AppiumElement WaitForElement(string automationId, int timeoutSeconds = 30)
    {
        var wait = new DefaultWait<AppiumDriver>(App)
        {
            Timeout = TimeSpan.FromSeconds(timeoutSeconds),
            PollingInterval = TimeSpan.FromMilliseconds(250)
        };
        wait.IgnoreExceptionTypes(typeof(NoSuchElementException));

        return wait.Until(driver => driver.FindElement(MobileBy.Id(automationId)) as AppiumElement);
    }

    protected static void WaitForText(
        string automationId,
        string expected,
        int timeoutSeconds = 30)
    {
        var wait = new DefaultWait<AppiumDriver>(App)
        {
            Timeout = TimeSpan.FromSeconds(timeoutSeconds),
            PollingInterval = TimeSpan.FromMilliseconds(250)
        };
        wait.IgnoreExceptionTypes(typeof(NoSuchElementException));

        wait.Until(driver => driver.FindElement(MobileBy.Id(automationId)).Text == expected);
    }

    // The suite the sample app runs is fast, but the report is only rendered once it finishes.
    // Waiting on the total count is the cheapest signal that the run is done.
    protected static void WaitForTestRunToComplete(int timeoutSeconds = 60)
    {
        WaitForText("TotalTestCount", "8", timeoutSeconds);
    }

    // Tests share one Appium session and therefore one app instance, and some of them leave the
    // app showing a filtered run or a different page. Returning to the summary and re-running
    // the full suite gives every test the same starting point regardless of what ran before it,
    // so one failure does not cascade into the rest of the fixture.
    [SetUp]
    public void ResetToCompletedFullRun()
    {
        for (int attempt = 0; attempt < 3 && !IsOnSummaryPage(); attempt++)
        {
            App.Navigate().Back();
        }

        WaitForElement("RunButton").Click();
        WaitForTestRunToComplete();
    }

    private static bool IsOnSummaryPage()
    {
        try
        {
            App.FindElement(MobileBy.Id("RunButton"));
            return true;
        }
        catch (NoSuchElementException)
        {
            return false;
        }
    }
}
