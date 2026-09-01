using System.Reflection;
using NUnit.Runner.Services;
using NUnit.Runner.View;
using NUnit.Runner.ViewModel;

namespace NUnit.Maui.Runner;

public partial class App : Application {
    private readonly SummaryViewModel _model;

    public App (IRunnerConfiguration config) {
        InitializeComponent ();

        _model = new SummaryViewModel();
        Options = config.ProvideOption();

        foreach (Assembly testItem in config.ProvideAssemblies()) {
            AddTestAssembly(testItem);
        }
    }

    protected override Window CreateWindow(IActivationState activationState) {
        return new Window(new NavigationPage(new SummaryView(_model)));
    }

    public void AddTestAssembly(Assembly testAssembly, Dictionary<string, object> options = null) {
        _model.AddTest(testAssembly, options);
    }

    public TestOptions Options {
        get { return _model.Options; }
        set { _model.Options = value; }
    }
}
