using System.Net;
using System.Net.Sockets;


// Listens for the XML report the runner streams back over TCP (see TcpWriterInfo in the
// runner's TestOptions). Returns false if nothing connected within the timeout, so a test
// run that never starts fails the build instead of hanging it.
public bool RecieveXmlReport(string fileName, int port, int timeoutSeconds) {
    var listener = new TcpListener(IPAddress.Loopback, port);
    listener.Start();
    try {
        var waited = 0;
        while (!listener.Pending()) {
            if (waited >= timeoutSeconds * 1000) {
                Warning($"No test report received on port {port} after {timeoutSeconds}s.");
                return false;
            }
            System.Threading.Thread.Sleep(200);
            waited += 200;
        }

        using (var socket = listener.AcceptSocket())
        using (var file = System.IO.File.Create(fileName)) {
            socket.ReceiveTimeout = timeoutSeconds * 1000;
            var buffer = new byte[8192];
            int readBytes;
            while ((readBytes = socket.Receive(buffer)) != 0) {
                file.Write(buffer, 0, readBytes);
            }
        }
        return true;
    }
    finally {
        listener.Stop();
    }
}

// iossimulator-arm64 on Apple silicon, iossimulator-x64 on Intel.
public string GetSimulatorRuntimeIdentifier() {
    return System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture ==
        System.Runtime.InteropServices.Architecture.Arm64
            ? "iossimulator-arm64"
            : "iossimulator-x64";
}

// Installs, launches and uninstalls an app bundle on a simulator via XHarness, which also
// provisions the simulator and reports the app's exit code.
//
// resetSimulator wipes the simulator before the run and, more usefully, shuts it down
// afterwards instead of leaving it running. That costs roughly a minute for the erase and
// cold boot, so it can be turned off when iterating locally.
public void XHarnessAppleRun(
    DirectoryPath appBundle,
    string target,
    DirectoryPath outputDirectory,
    string timeout,
    bool resetSimulator) {

    var arguments = new ProcessArgumentBuilder()
        .Append("xharness").Append("apple").Append("run")
        .Append("--app").AppendQuoted(MakeAbsolute(appBundle).FullPath)
        .Append("--target").Append(target)
        .Append("--output-directory").AppendQuoted(MakeAbsolute(outputDirectory).FullPath)
        .Append("--timeout").Append(timeout);

    if (resetSimulator) {
        arguments.Append("--reset-simulator");
    }

    var exitCode = StartProcess("dotnet", new ProcessSettings { Arguments = arguments });
    if (exitCode != 0) {
        throw new Exception(
            $"XHarness exited with code {exitCode}. See the logs in {outputDirectory}.");
    }
}

int XHarness(ProcessArgumentBuilder arguments, bool ignoreExitCode = false, bool quiet = false) {
    var settings = new ProcessSettings { Arguments = arguments };
    int exitCode;

    if (quiet) {
        // adb passthrough commands are chatty; keep their output out of the build log.
        settings.RedirectStandardOutput = true;
        settings.RedirectStandardError = true;
        IEnumerable<string> discarded;
        exitCode = StartProcess("dotnet", settings, out discarded);
    }
    else {
        exitCode = StartProcess("dotnet", settings);
    }

    if (exitCode != 0 && !ignoreExitCode) {
        throw new Exception($"XHarness exited with code {exitCode}.");
    }
    return exitCode;
}

// XHarness's `android test` drives an instrumentation, which this app does not have — it is a
// plain MAUI activity that reports its results over TCP. So install it, start the launcher
// activity through XHarness's bundled adb, and let the caller wait for the report.
//
// Every command is pinned to one device id so a run never touches another emulator that
// happens to be attached.
public void XHarnessAndroidInstall(
    FilePath apk,
    string packageName,
    string deviceId,
    DirectoryPath outputDirectory) {

    XHarness(new ProcessArgumentBuilder()
        .Append("xharness").Append("android").Append("install")
        .Append("--app").AppendQuoted(MakeAbsolute(apk).FullPath)
        .Append("--package-name").Append(packageName)
        .Append("--device-id").Append(deviceId)
        .Append("--output-directory").AppendQuoted(MakeAbsolute(outputDirectory).FullPath));
}

public void XHarnessAndroidUninstall(string packageName, string deviceId) {
    XHarness(new ProcessArgumentBuilder()
        .Append("xharness").Append("android").Append("uninstall")
        .Append("--package-name").Append(packageName)
        .Append("--device-id").Append(deviceId),
        ignoreExitCode: true);
}

// Runs an adb command against one device through XHarness's bundled adb and returns its
// output, so none of this depends on adb being on PATH or on ANDROID_HOME being set.
public IEnumerable<string> Adb(string deviceId, params string[] adbArguments) {
    var arguments = new ProcessArgumentBuilder()
        .Append("xharness").Append("android").Append("adb").Append("--")
        .Append("-s").Append(deviceId);
    foreach (var argument in adbArguments) {
        arguments.Append(argument);
    }

    IEnumerable<string> output;
    StartProcess(
        "dotnet",
        new ProcessSettings {
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        },
        out output);
    return output ?? Enumerable.Empty<string>();
}

// `monkey` refuses to launch the app on some system images, so resolve the launcher activity
// and start it explicitly. The activity name carries a generated prefix, hence the lookup.
public void XHarnessAndroidStartApp(string packageName, string deviceId) {
    var resolved = Adb(
        deviceId,
        "shell", "cmd", "package", "resolve-activity", "--brief",
        "-c", "android.intent.category.LAUNCHER", packageName);

    string activity = resolved
        .Select(line => line.Trim())
        .LastOrDefault(line => line.StartsWith(packageName + "/"));

    if (string.IsNullOrEmpty(activity)) {
        throw new Exception($"Could not find a launcher activity for '{packageName}'.");
    }

    var started = Adb(deviceId, "shell", "am", "start", "-n", activity);
    if (!started.Any(line => line.Contains("Starting:"))) {
        throw new Exception($"Failed to start {activity}: {string.Join(" ", started)}");
    }
}

public void XHarnessAndroidStopApp(string packageName, string deviceId) {
    // A safety net: the runner exits by itself once it has sent its report.
    Adb(deviceId, "shell", "am", "force-stop", packageName);
}

// Dumps the device log so a failed run has something to diagnose from.
public void XHarnessAndroidSaveLogcat(string deviceId, FilePath destination) {
    System.IO.File.WriteAllLines(
        MakeAbsolute(destination).FullPath,
        Adb(deviceId, "logcat", "-d"));
}

// A Debug Android build defaults to Fast Deployment, which leaves the assemblies out of the
// APK for the IDE to push separately. Such an APK installs fine and then aborts at startup
// with "No assemblies found", so check before installing and say so plainly. Embedded
// assemblies ship as lib/<abi>/lib_*.dll.so entries.
public void VerifyApkHasEmbeddedAssemblies(FilePath apk) {
    using (var archive = System.IO.Compression.ZipFile.OpenRead(MakeAbsolute(apk).FullPath)) {
        bool hasAssemblies = archive.Entries.Any(entry => entry.FullName.EndsWith(".dll.so"));
        if (!hasAssemblies) {
            throw new Exception(
                $"{apk} has no embedded assemblies, so it would abort on startup. It was " +
                "built without EmbedAssembliesIntoApk=true — rebuild through the Test target.");
        }
    }
}

// --- Android emulator provisioning -------------------------------------------------------
//
// XHarness locates devices but does not create or boot them, so the emulator the tests run on
// is provisioned here. Using a dedicated AVD on its own port keeps a run from touching any
// emulator you already have open.

public DirectoryPath AndroidSdkRoot() {
    foreach (var variable in new[] { "ANDROID_HOME", "ANDROID_SDK_ROOT" }) {
        var value = EnvironmentVariable(variable);
        if (!string.IsNullOrEmpty(value) && DirectoryExists(value)) {
            return Directory(value);
        }
    }

    var home = EnvironmentVariable("HOME") ?? EnvironmentVariable("USERPROFILE");
    foreach (var candidate in new[] { $"{home}/Library/Android/sdk", $"{home}/Android/Sdk" }) {
        if (DirectoryExists(candidate)) {
            return Directory(candidate);
        }
    }

    throw new Exception(
        "Could not find the Android SDK. Set ANDROID_HOME to point at it.");
}

// Picks the newest installed system image matching the host architecture, so the AVD does not
// hard-code an API level that may not be installed.
public string FindAndroidSystemImage(DirectoryPath sdkRoot) {
    string abi = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture ==
        System.Runtime.InteropServices.Architecture.Arm64 ? "arm64-v8a" : "x86_64";

    var images = GetDirectories($"{sdkRoot}/system-images/*/*/{abi}")
        .Select(path => new {
            Api = path.GetParent().GetParent().GetDirectoryName(),   // android-30
            Tag = path.GetParent().GetDirectoryName(),               // google_apis
        })
        .OrderByDescending(image => image.Api.Length)   // android-9 sorts below android-30
        .ThenByDescending(image => image.Api)
        .ToList();

    if (!images.Any()) {
        throw new Exception(
            $"No Android system image for '{abi}' is installed under {sdkRoot}/system-images. " +
            "Install one with sdkmanager.");
    }

    var chosen = images.First();
    return $"system-images;{chosen.Api};{chosen.Tag};{abi}";
}

public bool IsAndroidDeviceBooted(string deviceId) {
    return Adb(deviceId, "shell", "getprop", "sys.boot_completed")
        .Any(line => line.Trim() == "1");
}

// Returns true when the emulator on this port is the AVD we manage, so a run never shuts down
// an unrelated emulator that happens to be using the port.
public bool IsOurEmulator(string deviceId, string avdName) {
    return Adb(deviceId, "emu", "avd", "name")
        .Any(line => line.Trim() == avdName);
}

public void EnsureAvdExists(DirectoryPath sdkRoot, string avdName, string systemImage) {
    var emulator = sdkRoot.CombineWithFilePath("emulator/emulator");
    IEnumerable<string> existing;
    StartProcess(
        emulator.FullPath,
        new ProcessSettings { Arguments = "-list-avds", RedirectStandardOutput = true },
        out existing);

    if (existing != null && existing.Any(line => line.Trim() == avdName)) {
        return;
    }

    Information($"Creating AVD '{avdName}' from {systemImage}");
    var avdManager = sdkRoot.CombineWithFilePath("cmdline-tools/latest/bin/avdmanager");
    if (!FileExists(avdManager)) {
        throw new Exception($"avdmanager not found at {avdManager}.");
    }

    // avdmanager asks whether to create a custom hardware profile, but takes the default when
    // stdin is not a terminal, so it needs no input here. -d gives a sane screen size; the
    // bare default profile is small enough to upset some tooling.
    var exitCode = StartProcess(avdManager.FullPath, new ProcessSettings {
        Arguments = new ProcessArgumentBuilder()
            .Append("create").Append("avd")
            .Append("-n").Append(avdName)
            .Append("-k").AppendQuoted(systemImage)
            .Append("-d").Append("pixel")
            .Append("--force"),
        EnvironmentVariables = new Dictionary<string, string> {
            { "ANDROID_HOME", sdkRoot.FullPath },
            { "ANDROID_SDK_ROOT", sdkRoot.FullPath }
        }
    });

    if (exitCode != 0) {
        throw new Exception($"Failed to create AVD '{avdName}' (exit code {exitCode}).");
    }
}

// Boots the AVD headless on a fixed port and waits for it to finish booting. Returns the
// device id. -no-snapshot and -wipe-data give each run a clean device.
public string StartAndroidEmulator(
    DirectoryPath sdkRoot,
    string avdName,
    int port,
    int bootTimeoutSeconds) {

    string deviceId = $"emulator-{port}";

    if (IsAndroidDeviceBooted(deviceId)) {
        Information($"Reusing the emulator already running on {deviceId}.");
        return deviceId;
    }

    EnsureAvdExists(sdkRoot, avdName, FindAndroidSystemImage(sdkRoot));

    Information($"Starting emulator '{avdName}' on {deviceId}");
    StartAndReturnProcess(
        sdkRoot.CombineWithFilePath("emulator/emulator").FullPath,
        new ProcessSettings {
            Arguments = new ProcessArgumentBuilder()
                .Append("-avd").Append(avdName)
                .Append("-port").Append(port.ToString())
                .Append("-no-window")
                .Append("-no-audio")
                .Append("-no-boot-anim")
                .Append("-no-snapshot")
                .Append("-wipe-data")
                .Append("-gpu").Append("swiftshader_indirect"),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            EnvironmentVariables = new Dictionary<string, string> {
                { "ANDROID_HOME", sdkRoot.FullPath },
                { "ANDROID_SDK_ROOT", sdkRoot.FullPath }
            }
        });

    for (var waited = 0; waited < bootTimeoutSeconds; waited += 2) {
        if (IsAndroidDeviceBooted(deviceId)) {
            Information($"Emulator booted after about {waited}s.");
            return deviceId;
        }
        System.Threading.Thread.Sleep(2000);
    }

    throw new Exception(
        $"Emulator '{avdName}' did not finish booting within {bootTimeoutSeconds}s.");
}

public void StopAndroidEmulator(string deviceId, string avdName) {
    if (!IsOurEmulator(deviceId, avdName)) {
        Warning($"{deviceId} is not '{avdName}', leaving it running.");
        return;
    }
    Information($"Shutting down {deviceId}");
    Adb(deviceId, "emu", "kill");
}

// --- iOS simulator for the UI tests -------------------------------------------------------
//
// The Test target lets XHarness pick and provision a simulator, but Appium has to attach to a
// named one, so these boot and populate it directly.

public string BootIosSimulator(string deviceName) {
    IEnumerable<string> output;
    StartProcess(
        "xcrun",
        new ProcessSettings {
            Arguments = "simctl list devices available",
            RedirectStandardOutput = true
        },
        out output);

    var device = new System.Text.RegularExpressions.Regex(
        @"^\s+" + System.Text.RegularExpressions.Regex.Escape(deviceName) +
        @"\s+\((?<udid>[0-9A-Fa-f-]{36})\)\s+\((?<state>\w+)\)");

    foreach (var line in output) {
        var match = device.Match(line);
        if (!match.Success) {
            continue;
        }

        string udid = match.Groups["udid"].Value;
        if (!match.Groups["state"].Value.Equals("Booted", StringComparison.OrdinalIgnoreCase)) {
            Information($"Booting simulator '{deviceName}' ({udid})");
            StartProcess("xcrun", $"simctl boot {udid}");
        }
        StartProcess("xcrun", new ProcessSettings {
            Arguments = $"simctl bootstatus {udid} -b",
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });
        return udid;
    }

    throw new Exception($"No available simulator named '{deviceName}' was found.");
}

public void InstallOnIosSimulator(string udid, DirectoryPath appBundle) {
    var exitCode = StartProcess("xcrun", new ProcessSettings {
        Arguments = new ProcessArgumentBuilder()
            .Append("simctl").Append("install").Append(udid)
            .AppendQuoted(MakeAbsolute(appBundle).FullPath)
    });

    if (exitCode != 0) {
        throw new Exception($"Failed to install {appBundle} on simulator {udid}.");
    }
}
