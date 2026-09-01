# NUnit 3 MAUI Runner

> **This is a fork.** It exists to publish a build targeting .NET 10 and MAUI 10, and is
> released on NuGet as
> [`MScottFord.Forks.NUnit.Maui.Runner`](https://www.nuget.org/packages/MScottFord.Forks.NUnit.Maui.Runner).
>
> The original is [JaneySprings/NUnit-MAUI-Runner](https://github.com/JaneySprings/NUnit-MAUI-Runner)
> by Nikita Romanov, by way of
> [Abhirasmanu-Trimble/NUnit-MAUI-Runner](https://github.com/Abhirasmanu-Trimble/NUnit-MAUI-Runner).
> Only the package id is prefixed: the assembly name and namespaces are unchanged, so code
> written against the upstream package needs no edits beyond the package reference.
>
> Changes in this fork are not offered upstream. If the upstream project picks .NET 10 up, use
> that instead.

## Installation

```bash
dotnet add package MScottFord.Forks.NUnit.Maui.Runner
```

## Usage
Create configuration class:
```
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

Add NUnit App and configuration class to your MauiProgram.cs:
```
var builder = MauiApp.CreateBuilder();
  builder
      .UseMauiApp<NUnit.Maui.Runner.App>()
      .Services.AddSingleton<IRunnerConfiguration, RunnerConfig>();

return builder.Build();
```

## Building and testing

### Prerequisites

Node and Appium are managed by [mise](https://mise.jdx.dev), so getting set up is:

```bash
mise install          # Node and Appium, at the versions in mise.toml
mise run setup        # Appium's platform drivers, plus dotnet tool restore
```

These are not managed by mise and need installing separately:

- The .NET SDK, with the `maui-android` and `maui-ios` workloads
  (`dotnet workload install maui-android maui-ios`).
- The Android SDK, including an emulator system image. The build script finds it via
  `ANDROID_HOME`, or in the usual per-platform location if that is not set.
- Xcode, for anything targeting iOS.

### Running the tests

The sample suite in `src/NUnitTests` is deliberately half-failing: a healthy run is 4 passed
and 4 failed out of 8. Both targets run on iOS and Android by default; pass
`--platform=ios` or `--platform=android` for one of them.

```bash
dotnet cake --target Test      # runs the suite on a simulator/emulator and checks the report
dotnet cake --target UITest    # drives the app's UI with Appium
```

`Test` deploys the app, lets it run its suite, and validates the XML report it streams back.
`UITest` drives the UI itself: it taps the buttons and compares screenshots against the
reference images in `src/UITests.Shared/Baselines`.

Both targets provision what they need. Android gets a dedicated emulator (`NUnitMauiRunner_Test`)
rather than any you already have open, and iOS uses a simulator; both are shut down afterwards.
Pass `--keep-emulator` or `--keep-simulator` to leave them running, which makes repeated runs
considerably faster.

### Screenshot baselines

The UI tests compare against committed screenshots, which are specific to the device that
produced them: a different emulator or simulator will fail with a size mismatch rather than a
confusing diff. After an intentional UI change, regenerate and review them:

```bash
dotnet cake --target UITest --update-baselines
```

The updated images are written next to the test binaries; copy them into
`src/UITests.Shared/Baselines/<platform>/` and commit them once you have checked them.

## Versioning and publishing

`version.txt` holds the release line, and is the only file to edit when starting a new one.
Everything else is derived at build time:

| Situation | Version |
| --- | --- |
| `--release-version` passed | that value, normalised |
| `HEAD` is on a tag | that tag, normalised |
| otherwise | `<line>.<commits>-preview` |

Normalising pads the version out to three components and tolerates a leading `v`, so the
version the build resolves is the version in the package file name. `dotnet cake --target Prepare`
prints the version it would use without building anything.

Cutting a release is therefore a tag:

```bash
git tag v2.0.0 && git push origin v2.0.0
```

The `Publish` workflow builds, packs and pushes to NuGet. A push to the default branch
publishes a preview; pushing a `v*` tag publishes the release that tag names. It needs a
`NUGET_API_KEY` repository secret, and it runs on macOS because the package carries iOS
assemblies.

To pack locally without publishing:

```bash
dotnet cake --configuration release
dotnet cake --target Pack --configuration release
```

## Contributing
We love pull requests! All NUnit projects are built and maintained entirely by the community, contributions of any kind are welcome. Not sure where to start? Have a look at our [Contributor's guide](https://github.com/nunit/nunit/blob/master/CONTRIBUTING.md).

Adding something new? We suggest posting an issue first, to run your idea by the team. 

