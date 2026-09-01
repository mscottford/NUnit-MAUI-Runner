#addin nuget:?package=Cake.FileHelpers&version=5.0.0
// #addin nuget:?package=Cake.AppleSimulator&version=0.2.0
#load "utils.cake"

string target = Argument("target", "Build");
string configuration = Argument("configuration", "debug");
string version = Argument("release-version", "1.0");
string key = Argument("nuget-key", "");

// Which platforms the Test target runs on: ios, android, or both. Validated here so a typo
// fails before anything is built.
string platform = Argument("platform", "both").ToLowerInvariant();
if (platform != "ios" && platform != "android" && platform != "both") {
    throw new Exception($"Unknown --platform '{platform}'. Expected 'ios', 'android' or 'both'.");
}

// The Test target shuts the simulator down when it finishes, which costs about a minute for
// the erase and cold boot. Pass --keep-simulator to leave it running for faster reruns.
bool keepSimulator = HasArgument("keep-simulator");

// Likewise for Android: the tests run on their own emulator, which is shut down afterwards
// unless --keep-emulator is passed. A kept emulator is reused by the next run.
bool keepEmulator = HasArgument("keep-emulator");

// Must match the TargetFrameworks in src/NUnit.Maui.Runner and src/NUnitTests.
const string AndroidTfm = "net10.0-android";
const string IosTfm = "net10.0-ios26.0";

// XHarness provisions and boots the simulator itself; run `dotnet xharness apple run --help`
// for the other targets it accepts. Append e.g. _26.4 to pin an OS version.
const string SimulatorTarget = "ios-simulator-64";

// The simulator the UI tests attach to. XHarness picks its own device for the Test target, but
// Appium has to be pointed at a specific one.
const string SimulatorDeviceName = "iPhone 17 Pro";

// The id in nuget/MScottFord.Forks.NUnit.Maui.Runner.nuspec, which is also the nupkg's file
// name. This is a fork, so the package id is prefixed while the assembly keeps its original
// name and namespaces.
const string PackageId = "MScottFord.Forks.NUnit.Maui.Runner";

// ApplicationId in src/NUnitTests/NUnitTests.csproj.
const string TestAppPackageName = "com.companyname.nunittests";

// XHarness finds devices but cannot create them, so the tests get their own AVD rather than
// whatever emulator happens to be open. The port fixes the device id and keeps this run clear
// of the default 5554 that a manually started emulator takes.
const string AndroidAvdName = "NUnitMauiRunner_Test";
const int AndroidEmulatorPort = 5560;
const int AndroidEmulatorBootTimeoutSeconds = 300;

// Port the runner streams its XML report to, from RunnerConfig in src/NUnitTests/MauiProgram.cs.
// The Android app reaches it on 10.0.2.2, the emulator's alias for the host loopback.
const int ReportPort = 13000;

// Covers installing the app and running the suite, so it needs headroom over the run itself.
const int ReportTimeoutSeconds = 180;

// Set UITEST_UPDATE_BASELINES=1 (or pass --update-baselines) to rewrite the reference
// screenshots instead of comparing against them.
bool updateBaselines = HasArgument("update-baselines");

// The sample suite in src/NUnitTests/Tests.cs deliberately contains four failing tests, so a
// healthy run is 4 passed / 4 failed. Anything else means the runner itself is broken.
const int ExpectedTotal = 8;
const int ExpectedPassed = 4;
const int ExpectedFailed = 4;


Task("Prepare")
    .Does(() => {
        CleanDirectory("./Build");
        CleanDirectory("./Artifacts");
        CleanDirectories("./**/bin");
        CleanDirectories("./**/obj");

        var options = System.Text.RegularExpressions.RegexOptions.None;
        var pattern = "<vers" + "ion>.*</version>";
        FilePath[] files = FindRegexInFiles($"./**/*.*", pattern, options);
        foreach (var file in files) {
            List<string> matches = FindRegexMatchesInFile(file, pattern, options);
            foreach (var match in matches) {
                ReplaceTextInFiles(file.ToString(), match, "<vers" + $"ion>{version}</version>");
            }
        }
    });

Task("Build")
    .IsDependentOn("Prepare")
    .Does(() => {
        DotNetBuild("./src/framework/nunit.framework.csproj", new DotNetBuildSettings { Configuration = configuration});
        DotNetBuild("./src/NUnit.Maui.Runner/NUnit.Maui.Runner.csproj", new DotNetBuildSettings { Configuration = configuration});

        // Lay out the per-platform files the nuspec packages. The runner
        // multi-targets, so each target framework has its own output directory.
        EnsureDirectoryExists("./Build");
        EnsureDirectoryExists("./Build/Android");
        EnsureDirectoryExists("./Build/iOS");

        CopyFileToDirectory($"./src/framework/bin/{configuration}/nunit.framework.dll", "./Build");
        CopyFileToDirectory($"./src/NUnit.Maui.Runner/bin/{configuration}/{AndroidTfm}/NUnit.Maui.Runner.dll", "./Build/Android");
        CopyFileToDirectory($"./src/NUnit.Maui.Runner/bin/{configuration}/{IosTfm}/NUnit.Maui.Runner.dll", "./Build/iOS");
    });

// Runs the sample suite on a simulator/emulator through the runner and validates the XML
// report it streams back. This is the end-to-end check that the runner works on-device.
// Use --platform=ios or --platform=android to run just one.
Task("Test")
    .IsDependentOn("Build")
    .Does(() => {
        bool runIos = platform == "both" || platform == "ios";
        bool runAndroid = platform == "both" || platform == "android";

        // The iOS half needs Xcode, so it can only run on macOS. Asking for it explicitly
        // elsewhere is an error; 'both' just skips it.
        if (runIos && Context.Environment.Platform.Family != PlatformFamily.OSX) {
            if (platform == "ios") {
                throw new Exception("--platform=ios requires macOS.");
            }
            Warning("Skipping the iOS tests: they require macOS.");
            runIos = false;
        }

        EnsureDirectoryExists("./Artifacts");

        if (runIos) {
            RunIosTests();
        }
        if (runAndroid) {
            RunAndroidTests();
        }

        Information("All tests passed!");
    });

public void RunIosTests() {
    Information("--- iOS ---");
    string runtimeIdentifier = GetSimulatorRuntimeIdentifier();

    DotNetBuild("./src/NUnitTests/NUnitTests.csproj", new DotNetBuildSettings {
        Configuration = configuration,
        Framework = IosTfm,
        MSBuildSettings = new DotNetMSBuildSettings()
            .WithProperty("RuntimeIdentifier", runtimeIdentifier)
            // Makes the app exit once the suite finishes, which is how XHarness knows the run
            // is over. Off for ordinary builds so the app stays open in an IDE.
            .WithProperty("AutomatedTestRun", "true")
    });

    var appBundle = Directory($"./src/NUnitTests/bin/{configuration}/{IosTfm}/{runtimeIdentifier}/NUnitTests.app");
    if (!DirectoryExists(appBundle)) {
        throw new Exception($"App bundle was not produced at {appBundle}.");
    }

    var logDirectory = Directory("./Artifacts/xharness-ios");
    var reportPath = MakeAbsolute(File("./Artifacts/Runner_iOS_Test_Results.xml"));

    // The listener has to be accepting before the app launches and connects out to it.
    var report = System.Threading.Tasks.Task.Run(
        () => RecieveXmlReport(reportPath.FullPath, ReportPort, ReportTimeoutSeconds));

    // Blocks until the app exits, which the runner does itself once it has sent its report
    // (TerminateAfterExecution in RunnerConfig).
    XHarnessAppleRun(
        appBundle, SimulatorTarget, logDirectory, "00:05:00",
        resetSimulator: !keepSimulator);

    if (!report.Result) {
        throw new Exception(
            $"The iOS app ran but sent no report. See the logs in {logDirectory}.");
    }

    VerifyTestReport(reportPath.FullPath);
}

public void RunAndroidTests() {
    Information("--- Android ---");

    // Debug Android builds default to Fast Deployment, which leaves the assemblies out of the
    // APK for the IDE to push separately. An APK installed on its own then aborts at startup
    // with "No assemblies found", so embed them.
    DotNetBuild("./src/NUnitTests/NUnitTests.csproj", new DotNetBuildSettings {
        Configuration = configuration,
        Framework = AndroidTfm,
        MSBuildSettings = new DotNetMSBuildSettings()
            .WithProperty("EmbedAssembliesIntoApk", "true")
            .WithProperty("AutomatedTestRun", "true")
    });

    var apk = File($"./src/NUnitTests/bin/{configuration}/{AndroidTfm}/{TestAppPackageName}-Signed.apk");
    if (!FileExists(apk)) {
        throw new Exception($"APK was not produced at {apk}.");
    }
    VerifyApkHasEmbeddedAssemblies(apk);

    var logDirectory = Directory("./Artifacts/xharness-android");
    EnsureDirectoryExists(logDirectory);
    var reportPath = MakeAbsolute(File("./Artifacts/Runner_Android_Test_Results.xml"));

    var sdkRoot = AndroidSdkRoot();
    string deviceId = StartAndroidEmulator(
        sdkRoot, AndroidAvdName, AndroidEmulatorPort, AndroidEmulatorBootTimeoutSeconds);

    try {
        XHarnessAndroidInstall(apk, TestAppPackageName, deviceId, logDirectory);
        try {
            // The listener has to be accepting before the app launches and connects out to
            // it, on 10.0.2.2 from inside the emulator.
            var report = System.Threading.Tasks.Task.Run(
                () => RecieveXmlReport(reportPath.FullPath, ReportPort, ReportTimeoutSeconds));

            XHarnessAndroidStartApp(TestAppPackageName, deviceId);

            if (!report.Result) {
                XHarnessAndroidSaveLogcat(
                    deviceId, logDirectory.Path.CombineWithFilePath("logcat.txt"));
                throw new Exception(
                    $"The Android app sent no report. See {logDirectory}/logcat.txt.");
            }
        }
        finally {
            XHarnessAndroidStopApp(TestAppPackageName, deviceId);
            XHarnessAndroidUninstall(TestAppPackageName, deviceId);
        }
    }
    finally {
        if (!keepEmulator) {
            StopAndroidEmulator(deviceId, AndroidAvdName);
        }
        else {
            Information($"Leaving {deviceId} running (--keep-emulator).");
        }
    }

    VerifyTestReport(reportPath.FullPath);
}

public void VerifyTestReport(string reportPath) {
    string report = System.IO.File.ReadAllText(reportPath);
    var summary = System.Text.RegularExpressions.Regex.Match(
        report,
        "<test-run\\b[^>]*?\\btotal=\"(?<total>\\d+)\"[^>]*?\\bpassed=\"(?<passed>\\d+)\"[^>]*?\\bfailed=\"(?<failed>\\d+)\"");

    if (!summary.Success) {
        throw new Exception($"Could not read a test-run summary from {reportPath}.");
    }

    int total = int.Parse(summary.Groups["total"].Value);
    int passed = int.Parse(summary.Groups["passed"].Value);
    int failed = int.Parse(summary.Groups["failed"].Value);
    Information($"Report: total={total} passed={passed} failed={failed}");

    if (total != ExpectedTotal || passed != ExpectedPassed || failed != ExpectedFailed) {
        throw new Exception(
            $"Unexpected results in {reportPath}: expected total={ExpectedTotal} " +
            $"passed={ExpectedPassed} failed={ExpectedFailed}, got total={total} " +
            $"passed={passed} failed={failed}.");
    }
}

// Drives the app's UI through Appium: taps the buttons and checks what is on screen, which
// the XML report cannot tell you. Needs `appium` on PATH with the uiautomator2 and xcuitest
// drivers installed, and it leaves the app installed so Appium can attach to it.
Task("UITest")
    .IsDependentOn("Build")
    .Does(() => {
        bool runIos = platform == "both" || platform == "ios";
        bool runAndroid = platform == "both" || platform == "android";

        if (runIos && Context.Environment.Platform.Family != PlatformFamily.OSX) {
            if (platform == "ios") {
                throw new Exception("--platform=ios requires macOS.");
            }
            Warning("Skipping the iOS UI tests: they require macOS.");
            runIos = false;
        }

        if (runIos) {
            RunIosUITests();
        }
        if (runAndroid) {
            RunAndroidUITests();
        }

        Information("All UI tests passed!");
    });

public void RunIosUITests() {
    Information("--- iOS UI tests ---");
    string runtimeIdentifier = GetSimulatorRuntimeIdentifier();

    // No AutomatedTestRun here: the app has to stay open for Appium to drive it.
    DotNetBuild("./src/NUnitTests/NUnitTests.csproj", new DotNetBuildSettings {
        Configuration = configuration,
        Framework = IosTfm,
        MSBuildSettings = new DotNetMSBuildSettings()
            .WithProperty("RuntimeIdentifier", runtimeIdentifier)
    });

    var appBundle = Directory($"./src/NUnitTests/bin/{configuration}/{IosTfm}/{runtimeIdentifier}/NUnitTests.app");
    var simulator = BootIosSimulator(SimulatorDeviceName);
    InstallOnIosSimulator(simulator, appBundle);

    RunUITestProject("./src/UITests.iOS/UITests.iOS.csproj", new Dictionary<string, string> {
        { "UITEST_IOS_UDID", simulator },
        { "UITEST_IOS_DEVICE_NAME", SimulatorDeviceName }
    });
}

public void RunAndroidUITests() {
    Information("--- Android UI tests ---");

    DotNetBuild("./src/NUnitTests/NUnitTests.csproj", new DotNetBuildSettings {
        Configuration = configuration,
        Framework = AndroidTfm,
        MSBuildSettings = new DotNetMSBuildSettings()
            .WithProperty("EmbedAssembliesIntoApk", "true")
    });

    var apk = File($"./src/NUnitTests/bin/{configuration}/{AndroidTfm}/{TestAppPackageName}-Signed.apk");
    VerifyApkHasEmbeddedAssemblies(apk);

    var sdkRoot = AndroidSdkRoot();
    string deviceId = StartAndroidEmulator(
        sdkRoot, AndroidAvdName, AndroidEmulatorPort, AndroidEmulatorBootTimeoutSeconds);

    try {
        XHarnessAndroidInstall(apk, TestAppPackageName, deviceId, "./Artifacts/xharness-android");
        RunUITestProject("./src/UITests.Android/UITests.Android.csproj",
            new Dictionary<string, string> { { "UITEST_ANDROID_DEVICE", deviceId } });
    }
    finally {
        XHarnessAndroidUninstall(TestAppPackageName, deviceId);
        if (!keepEmulator) {
            StopAndroidEmulator(deviceId, AndroidAvdName);
        }
    }
}

public void RunUITestProject(string project, Dictionary<string, string> environment) {
    if (updateBaselines) {
        environment["UITEST_UPDATE_BASELINES"] = "1";
    }

    DotNetTest(project, new DotNetTestSettings {
        Configuration = configuration,
        EnvironmentVariables = environment
    });
}

// Builds the package without publishing it. Expects the ./Build layout from the Build target.
// Uses dotnet pack via a packaging-only project so no nuget.exe/mono is needed.
Task("Pack")
    .Does(() => {
        // NoBuild skips restore too, so restore explicitly to get the assets file pack needs.
        DotNetRestore("./nuget/NUnit.Maui.Runner.Package.csproj");
        DotNetPack("./nuget/NUnit.Maui.Runner.Package.csproj", new DotNetPackSettings {
            Configuration = configuration,
            OutputDirectory = "./Artifacts",
            NoBuild = true,
            NoRestore = true
        });
    });

Task("Nuget")
    .IsDependentOn("Pack")
    .Does(() => {
        NuGetPush($"./Artifacts/{PackageId}.{version}.0.nupkg", new NuGetPushSettings {
            Source = "https://api.nuget.org/v3/index.json",
            ApiKey = key
        });
    });

RunTarget(target);


