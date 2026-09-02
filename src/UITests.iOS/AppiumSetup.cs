using NUnit.Framework;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Enums;
using OpenQA.Selenium.Appium.iOS;

namespace UITests;

[SetUpFixture]
public class AppiumSetup
{
    private static IOSDriver? driver;

    public static AppiumDriver App =>
        driver ?? throw new InvalidOperationException("The Appium driver is not initialised.");

    public static string PlatformName => "ios";

    // Matches ApplicationId in NUnitTests.csproj.
    private const string BundleId = "com.companyname.nunittests";

    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        AppiumServerHelper.StartAppiumLocalServer();

        var options = new AppiumOptions
        {
            AutomationName = AutomationName.iOSXcuiTest,
            PlatformName = "iOS",
            App = BundleId,
        };

        // The simulator XHarness provisions varies by machine, so the device and OS version are
        // supplied by the build script rather than hard-coded.
        string deviceName = Environment.GetEnvironmentVariable("UITEST_IOS_DEVICE_NAME")
            ?? "iPhone 17 Pro";
        options.DeviceName = deviceName;

        string? platformVersion = Environment.GetEnvironmentVariable("UITEST_IOS_VERSION");
        if (!string.IsNullOrEmpty(platformVersion))
        {
            options.PlatformVersion = platformVersion;
        }

        string? udid = Environment.GetEnvironmentVariable("UITEST_IOS_UDID");
        if (!string.IsNullOrEmpty(udid))
        {
            options.AddAdditionalAppiumOption(MobileCapabilityType.Udid, udid);
        }

        // The app is installed by the build script; leave it in place between tests.
        options.AddAdditionalAppiumOption(MobileCapabilityType.NoReset, true);

        driver = new IOSDriver(AppiumServerHelper.ServiceUrl, options);
        driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
    }

    [OneTimeTearDown]
    public void RunAfterAnyTests()
    {
        driver?.Quit();
        driver = null;
        AppiumServerHelper.DisposeAppiumLocalServer();
    }
}
