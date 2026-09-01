using System.Diagnostics;
using OpenQA.Selenium.Appium.Service;

namespace UITests;

// Starts an Appium server for the duration of the run so the tests are self-contained and
// nobody has to remember to leave `appium` running in a terminal.
//
// Node and Appium are provided by mise (see mise.toml). Its shims are only on PATH in a shell
// where mise is activated, which is not true of, say, an IDE test runner, so both are located
// explicitly rather than left to the Appium client's own PATH search.
public static class AppiumServerHelper
{
    private static AppiumLocalService? appiumLocalService;

    public const string DefaultHostAddress = "127.0.0.1";
    public const int DefaultHostPort = 4723;

    public static void StartAppiumLocalServer(
        string host = DefaultHostAddress,
        int port = DefaultHostPort)
    {
        if (appiumLocalService is not null)
        {
            return;
        }

        var builder = new AppiumServiceBuilder()
            .WithIPAddress(host)
            .UsingPort(port);

        string? node = ResolveTool("node");
        string? appiumEntryPoint = ResolveAppiumEntryPoint();

        if (node is not null)
        {
            builder = builder.UsingDriverExecutable(new FileInfo(node));
        }

        if (appiumEntryPoint is not null)
        {
            builder = builder.WithAppiumJS(new FileInfo(appiumEntryPoint));
        }

        appiumLocalService = builder.Build();
        appiumLocalService.Start();
    }

    // Drivers must be pointed at this server explicitly. Constructing one from options alone
    // makes the client build its own default service, which searches PATH for node and Appium
    // and therefore misses mise-managed installs.
    public static Uri ServiceUrl =>
        appiumLocalService?.ServiceUrl
        ?? throw new InvalidOperationException("The Appium server has not been started.");

    public static void DisposeAppiumLocalServer()
    {
        appiumLocalService?.Dispose();
        appiumLocalService = null;
    }

    // Appium's own launcher is a shell wrapper, but the client needs the JavaScript entry
    // point. The package declares it as index.js next to the wrapper's node_modules directory.
    private static string? ResolveAppiumEntryPoint()
    {
        string? fromEnvironment = Environment.GetEnvironmentVariable("APPIUM_JS");
        if (!string.IsNullOrEmpty(fromEnvironment) && File.Exists(fromEnvironment))
        {
            return fromEnvironment;
        }

        string? launcher = ResolveTool("appium");
        if (launcher is null)
        {
            return null;
        }

        // .../node_modules/.bin/appium -> .../node_modules/appium/index.js
        DirectoryInfo? binDirectory = new FileInfo(launcher).Directory;
        DirectoryInfo? nodeModules = binDirectory?.Parent;
        if (nodeModules is null)
        {
            return null;
        }

        string entryPoint = Path.Combine(nodeModules.FullName, "appium", "index.js");
        return File.Exists(entryPoint) ? entryPoint : null;
    }

    // Asks mise where a tool lives, falling back to a plain PATH lookup for machines that do
    // not use mise.
    private static string? ResolveTool(string tool)
    {
        string? path = Run("mise", $"which {tool}");
        if (path is not null && File.Exists(path))
        {
            return path;
        }

        path = Run(OperatingSystem.IsWindows() ? "where" : "which", tool);
        return path is not null && File.Exists(path) ? path : null;
    }

    private static string? Run(string fileName, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });

            if (process is null)
            {
                return null;
            }

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(10_000);

            if (process.ExitCode != 0)
            {
                return null;
            }

            // `where` on Windows can return several matches.
            string? first = output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();

            return string.IsNullOrEmpty(first) ? null : first;
        }
        catch (Exception)
        {
            // The tool is not installed; the caller falls back to Appium's own discovery.
            return null;
        }
    }
}
