using Android.App;
using Android.Runtime;
using Android.Content.PM;
using Android.OS;

namespace NUnitTests;

// Without [Register] the activity gets a generated name such as
// crc6489912d63bdd135a2.MainActivity, which Appium cannot be pointed at reliably. The value
// must match ApplicationId in NUnitTests.csproj plus ".MainActivity".
[Register("com.companyname.nunittests.MainActivity")]
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
}
