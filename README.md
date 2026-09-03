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

To run against a device that is already up -- which is what CI does -- name it instead, and it
will be left alone afterwards:

```bash
dotnet cake --target Test --platform=android --android-device=emulator-5554
dotnet cake --target Test --platform=ios --ios-device="iPhone 17 Pro"
```

### Screenshot baselines

The UI tests compare against committed screenshots, which are specific to the device that
produced them: a different emulator or simulator will fail with a size mismatch rather than a
confusing diff. After an intentional UI change, regenerate and review them:

```bash
dotnet cake --target UITest --update-baselines
```

The updated images are written next to the test binaries; copy them into
`src/UITests.Shared/Baselines/<platform>/` and commit them once you have checked them.

## Versioning and releasing

Versions come from the commit messages. Commits follow
[Conventional Commits](https://www.conventionalcommits.org), and
[Versionize](https://github.com/versionize/versionize) reads the ones since the last tag to
decide whether the next release is a patch, a minor or a major, writes `CHANGELOG.md` and tags
it. The released version is recorded in `nuget/NUnit.Maui.Runner.Package.csproj`; nothing needs
editing by hand.

`build.cake` then resolves the version to build with:

| Situation | Version |
| --- | --- |
| `--release-version` passed | that value, normalised |
| `HEAD` is on a tag | that tag, normalised |
| otherwise | next patch of the released version, `-preview.<commits>` |

`dotnet cake --target Prepare` prints the version it would use without building anything.

### How a change gets released

Work happens on branches and lands through pull requests; the default branch is only updated
when something should be released.

1. Open a pull request. `CI` runs the tests on both platforms and packs the package without
   publishing it.
2. Merge it. `Release` runs the same tests again, this time against what actually landed, and
   only then versions, tags, publishes to NuGet and creates a GitHub release.

Both workflows call the same `Test` workflow, so a release cannot run a weaker set of checks
than a pull request does, and a pull request having been green is not taken as sufficient on its
own -- the branch it was tested against may have moved since.

Within the release, the package is built and its version checked against the tag *before*
anything is pushed. A build failure therefore leaves the branch untouched and the run can be
retried; the tag only appears once there is a package to go with it.

A merge that contains nothing significant -- only `docs`, `chore` or `refactor` commits --
produces no release: Versionize exits non-zero and the publishing steps are skipped.

Releasing needs no secrets. Publishing uses
[trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing): the
release job asks GitHub for a short-lived OIDC token, and nuget.org exchanges it for an API key
valid for an hour after matching it against a policy registered for this repository and the
`release.yml` workflow. Nothing long-lived is stored, but the policy is tied to that file name,
so renaming the workflow means updating the policy on nuget.org too.

To pack locally without publishing:

```bash
dotnet cake --configuration release
dotnet cake --target Pack --configuration release
```

## Contributing
We love pull requests! All NUnit projects are built and maintained entirely by the community, contributions of any kind are welcome. Not sure where to start? Have a look at our [Contributor's guide](https://github.com/nunit/nunit/blob/master/CONTRIBUTING.md).

Adding something new? We suggest posting an issue first, to run your idea by the team. 

