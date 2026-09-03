# NUnit MAUI Runner

Runs NUnit tests inside a .NET MAUI app, on a device, simulator or emulator, and reports the
results in the app or streams them over TCP for a build to collect.

> **This is a fork.** It exists to publish a build targeting .NET 10 and MAUI 10.
>
> The original is [JaneySprings/NUnit-MAUI-Runner](https://github.com/JaneySprings/NUnit-MAUI-Runner)
> by Nikita Romanov, by way of
> [Abhirasmanu-Trimble/NUnit-MAUI-Runner](https://github.com/Abhirasmanu-Trimble/NUnit-MAUI-Runner).
> Only the package id is prefixed: the assembly name and namespaces are unchanged, so code
> written against the upstream package needs no edits beyond the package reference.
>
> Changes here are not offered upstream. If the upstream project picks .NET 10 up, use that
> instead.

## Supported platforms

`net10.0-android` and `net10.0-ios`. Android 5.0 (API 21) or higher, iOS 12.2 or higher.

## Installation

```bash
dotnet add package MScottFord.Forks.NUnit.Maui.Runner
```

## Usage

Create a configuration class:

```csharp
class RunnerConfig : IRunnerConfiguration {
    public TestOptions ProvideOption() {
        return new TestOptions() {
            // Paste NUnit options here
            // AutoRun = true
        };
    }
    public IEnumerable<Assembly> ProvideAssemblies() {
        return new List<Assembly> {
            // Paste your assemblies with tests here
        };
    }
}
```

Add the NUnit app and your configuration class to `MauiProgram.cs`:

```csharp
var builder = MauiApp.CreateBuilder();
  builder
      .UseMauiApp<NUnit.Maui.Runner.App>()
      .Services.AddSingleton<IRunnerConfiguration, RunnerConfig>();

return builder.Build();
```

`TestOptions` also covers running the suite on launch (`AutoRun`), exiting when it finishes
(`TerminateAfterExecution`), writing an XML result file, and streaming results to a TCP
listener (`TcpWriterParameters`) so a build can collect them.

## Source, issues and contributing

[github.com/mscottford/NUnit-MAUI-Runner](https://github.com/mscottford/NUnit-MAUI-Runner) —
see the repository README for how to build it and run its own tests.

## Licence

MIT.
