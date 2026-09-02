using System.Reflection;
using NUnit.Maui.Runner;
using NUnit.Runner.Services;

namespace NUnitTests;

public static class MauiProgram {
	public static MauiApp CreateMauiApp() {
		var builder = MauiApp.CreateBuilder();
		builder.UseMauiApp<NUnit.Maui.Runner.App>();
		builder.Services.AddSingleton<IRunnerConfiguration, RunnerConfig>();
		
		return builder.Build();
	}
}

class RunnerConfig : IRunnerConfiguration {
#if ANDROID
    private const string localhost = "10.0.2.2";
#else
    private const string localhost = "127.0.0.1";
#endif
	public TestOptions ProvideOption() {
		return new TestOptions() {
			AutoRun = true,
#if AUTOMATED_TEST_RUN
			// Only exit by itself when the build script is driving the run; leaving the app
			// open is what you want when launching it from an IDE.
			TerminateAfterExecution = true,
			// Only stream results when the build script has a listener on the other end.
			// Outside an automated run nothing is listening, and the attempt surfaces a red
			// "Cannot connect to ..." banner in the app.
			TcpWriterParameters = new TcpWriterInfo(localhost, 13000),
#endif
		};
	}
	public IEnumerable<Assembly> ProvideAssemblies() {
		return new List<Assembly> {
			typeof(RunnerConfig).Assembly
		};
	}
}
