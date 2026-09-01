using NUnit.Framework;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.Enums;

namespace UITests;

[SetUpFixture]
public class AppiumSetup
{
    private static AndroidDriver? driver;

    public static AppiumDriver App =>
        driver ?? throw new InvalidOperationException("The Appium driver is not initialised.");

    public static string PlatformName => "android";

    // Matches ApplicationId in NUnitTests.csproj and the [Register] attribute on MainActivity.
    private const string AppPackage = "com.companyname.nunittests";
    private const string AppActivity = "com.companyname.nunittests.MainActivity";

    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        AndroidSdk.EnsureEnvironment();
        AppiumServerHelper.StartAppiumLocalServer();

        var options = new AppiumOptions
        {
            AutomationName = AutomationName.AndroidUIAutomator2,
            PlatformName = "Android",
        };

        // Debug builds rely on Fast Deployment, and Appium's default reset deletes the files it
        // needs, so the app must already be installed and is left alone here.
        options.AddAdditionalAppiumOption(MobileCapabilityType.NoReset, true);
        options.AddAdditionalAppiumOption(AndroidMobileCapabilityType.AppPackage, AppPackage);
        options.AddAdditionalAppiumOption(AndroidMobileCapabilityType.AppActivity, AppActivity);

        // The emulator the build script provisions, when one is running.
        string? deviceId = Environment.GetEnvironmentVariable("UITEST_ANDROID_DEVICE");
        if (!string.IsNullOrEmpty(deviceId))
        {
            options.AddAdditionalAppiumOption(MobileCapabilityType.Udid, deviceId);
        }

        driver = new AndroidDriver(AppiumServerHelper.ServiceUrl, options);
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
