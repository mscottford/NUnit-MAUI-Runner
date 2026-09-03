# Change Log

All notable changes to this project will be documented in this file. See [versionize](https://github.com/versionize/versionize) for commit guidelines.

<a name="2.0.1"></a>
## [2.0.1](https://www.github.com/mscottford/NUnit-MAUI-Runner/releases/tag/v2.0.1) (2026-09-03)

### Bug Fixes

* **release:** publish via trusted publishing, and let a release be resumed ([#2](https://www.github.com/mscottford/NUnit-MAUI-Runner/issues/2)) ([7530f18](https://www.github.com/mscottford/NUnit-MAUI-Runner/commit/7530f18fb6f766baeadb76a10d287e3c94f62d4e))

<a name="2.0.0"></a>
## [2.0.0](https://www.github.com/mscottford/NUnit-MAUI-Runner/releases/tag/v2.0.0) (2026-09-02)

### Features

* **packaging:** publish as MScottFord.Forks.NUnit.Maui.Runner ([694c717](https://www.github.com/mscottford/NUnit-MAUI-Runner/commit/694c7175448fa98efc4eb4de2f63e31f34d0313d))
* **runtime:** upgrade to .NET 10 and MAUI 10 ([eab340e](https://www.github.com/mscottford/NUnit-MAUI-Runner/commit/eab340e6052ad8ef5203ad4dfd5ee2d169270f6d))

### Bug Fixes

* **build:** clear every build warning, and stop the iOS run timing out in CI ([a98ce7c](https://www.github.com/mscottford/NUnit-MAUI-Runner/commit/a98ce7c97086e0e9a59dbc7d18418448c2d3935e))
* **ci:** actually keep the cached Android system image ([54abae8](https://www.github.com/mscottford/NUnit-MAUI-Runner/commit/54abae8792ce725be9d00ab9f14e48420939a04a))
* **ci:** keep the emulator from stalling, and run the platforms in parallel ([c38fc59](https://www.github.com/mscottford/NUnit-MAUI-Runner/commit/c38fc590e6f40fee99f48c4d400217e3784330ee))
* **ci:** let the Android emulator fall back to software emulation ([f49762c](https://www.github.com/mscottford/NUnit-MAUI-Runner/commit/f49762c5375e0bfd9b8f01c3c6fd84d33338d83a))
* **ci:** pass the Android test arguments the emulator action can actually see ([4fa3c9c](https://www.github.com/mscottford/NUnit-MAUI-Runner/commit/4fa3c9c83b5da84b4cb5d063effe3e3dcac1c695))
* **ci:** run the Android tests on Linux, where a hypervisor is available ([289ac1d](https://www.github.com/mscottford/NUnit-MAUI-Runner/commit/289ac1dc8cea265e57d2bc066b058a8e4b24c303))
* **ui:** correct the chart geometry, summary layout and iOS letterboxing ([39fedf9](https://www.github.com/mscottford/NUnit-MAUI-Runner/commit/39fedf97559c9c1d3c88ddb93e29620320927e5c))

### Breaking Changes

* **packaging:** publish as MScottFord.Forks.NUnit.Maui.Runner ([694c717](https://www.github.com/mscottford/NUnit-MAUI-Runner/commit/694c7175448fa98efc4eb4de2f63e31f34d0313d))
* **runtime:** upgrade to .NET 10 and MAUI 10 ([eab340e](https://www.github.com/mscottford/NUnit-MAUI-Runner/commit/eab340e6052ad8ef5203ad4dfd5ee2d169270f6d))

