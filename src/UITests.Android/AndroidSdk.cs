namespace UITests;

// The uiautomator2 driver refuses to start a session unless ANDROID_HOME or ANDROID_SDK_ROOT is
// exported. Rather than make every developer and build agent set it, locate the SDK the same way
// the build script does and export it for the Appium server we spawn.
public static class AndroidSdk
{
    public static void EnsureEnvironment()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANDROID_HOME")) ||
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT")))
        {
            return;
        }

        string root = Locate();
        Environment.SetEnvironmentVariable("ANDROID_HOME", root);
        Environment.SetEnvironmentVariable("ANDROID_SDK_ROOT", root);
    }

    private static string Locate()
    {
        string home =
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        string[] candidates =
        [
            Path.Combine(home, "Library", "Android", "sdk"),        // macOS
            Path.Combine(home, "Android", "Sdk"),                   // Linux
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Android", "Sdk"),                                  // Windows
        ];

        foreach (string candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            "Could not find the Android SDK. Set ANDROID_HOME to point at it. Looked in: " +
            string.Join(", ", candidates));
    }
}
