using System.Reflection;
using System.Windows.Input;
using NUnit.Runner.Helpers;
using NUnit.Runner.View;

using NUnit.Runner.Services;
using NUnit.Framework.Internal;
using NUnit.Framework.Interfaces;

namespace NUnit.Runner.ViewModel {
    class SummaryViewModel : BaseViewModel {
        readonly TestPackage _testPackage;
        ResultSummary _results;
        bool _running;
        double _progress = 0;
        string _filter;
        string _executingTest;
        TestResultProcessor _resultProcessor;

        public SummaryViewModel() {
            _testPackage = new TestPackage();
            RunTestsCommand = new Command(
                async o => await RunTestsAsync(),
                o => !Running);
            RunFailedTestsCommand = new Command(
                async o => await RunFailedTestsAsync(),
                o => !Running && HasResults);
            ViewAllResultsCommand = new Command(
                async o => await Navigation.PushAsync(new ResultsView(new ResultsViewModel(_results.GetAllResults()))),
                o => !Running && HasResults);
            ViewFailedResultsCommand = new Command(
                async o => await Navigation.PushAsync(new ResultsView(new ResultsViewModel(_results.GetFailedResults()))),
                o => !Running && HasResults);
        }

        private TestOptions options;
        public TestOptions Options {
            get {
                if(options == null) {
                    options = new TestOptions();
                }
                return options;
            }
            set {
                options = value;
                Filter = options?.Filter;
            }
        }
        public ResultSummary Results {
            get { return _results; }
            set {
                if (Equals(value, _results)) return;
                _results = value;
                OnPropertyChanged();
                OnPropertyChanged("HasResults");
                Completed?.Invoke(this, EventArgs.Empty);
            }
        }
        public bool Running {
            get { return _running; }
            set {
                if (value.Equals(_running)) return;
                _running = value;
                OnPropertyChanged();
                RunTestsCommand.ChangeCanExecute();
                RunFailedTestsCommand.ChangeCanExecute();
                ViewAllResultsCommand.ChangeCanExecute();
                ViewFailedResultsCommand.ChangeCanExecute();
            }
        }
        public bool HasResults => Results != null;
        public double Progress {
            get => _progress;
            set {
                if(_progress == value) return;
                _progress = value;
                OnPropertyChanged();
            }
        }
        public string Filter {
            get => _filter;
            set {
                if(_filter == value) return;
                _filter = value;
                OnPropertyChanged();
            }
        }
        public string ExecutingTest {
            get => _executingTest;
            set {
                if(_executingTest == value) return;
                _executingTest = value;
                OnPropertyChanged();
            }
        }

        public EventHandler Completed;

        public Command RunTestsCommand { set; get; }
        public Command RunFailedTestsCommand { set; get; }
        public Command ViewAllResultsCommand { set; get; }
        public Command ViewFailedResultsCommand { set; get; }

        public async void OnAppearing() {
            if(Options.AutoRun) {
                // Don't rerun if we navigate back
                Options.AutoRun = false;
                await RunTestsAsync();
                return;
            }
        }

        internal void AddTest(Assembly testAssembly, Dictionary<string, object> options = null) {
            _testPackage.AddAssembly(testAssembly, options);
        }

        async Task RunTestsAsync() {
            TestFilter filter = TestFilter.Empty;
            if(!string.IsNullOrEmpty(Filter)) {
                filter = new DelegateTestFilter(x => {
                    return x.FullName.Contains(Filter, StringComparison.OrdinalIgnoreCase);
                });
            }
            await RunTestsCoreAsync(filter);
        }
        async Task RunFailedTestsAsync() {
            var set = Results.GetFailedResults().Select(x => x.FullName).ToHashSet();
            var filter = new DelegateTestFilter(x => {
                var res = set.Contains(x.FullName);
                return res;
            });
            await RunTestsCoreAsync(filter);
        }
        async Task RunTestsCoreAsync(TestFilter filter) {
            Running = true;
            Results = null;
            TestRunResult results = await _testPackage.ExecuteTests(filter, new Progress<(double progress, string testName)>(x => {
                Progress = x.progress;
                ExecutingTest = x.testName;
            }));
            ResultSummary summary = new ResultSummary(results);

            _resultProcessor = TestResultProcessor.BuildChainOfResponsability(Options);
            await _resultProcessor.Process(summary).ConfigureAwait(false);

            MainThread.BeginInvokeOnMainThread(() => {
                Results = summary;
                Running = false;
                Progress = 0;
                ExecutingTest = null;

                if (Options.TerminateAfterExecution)
                    TerminateWithSuccess();
            });
        }

        public static void TerminateWithSuccess()
        {
#if __IOS__
            var selector = new ObjCRuntime.Selector("terminateWithSuccess");
            UIKit.UIApplication.SharedApplication.PerformSelector(selector, UIKit.UIApplication.SharedApplication, 0);
#elif __ANDROID__
            System.Environment.Exit(0);
#elif WINDOWS_UWP
            Windows.UI.Xaml.Application.Current.Exit();
#endif
        }

        public class DelegateTestFilter : TestFilter {
            Func<ITest, bool> match;
            public DelegateTestFilter(Func<ITest, bool> match) {
                this.match = match;
            }
            public override bool Match(ITest test) {
                return true;
            }
            public override bool Pass(ITest test, bool negated) {
                return match(test);
            }
            public override bool IsExplicitMatch(ITest test) {
                return false;
            }
            public override TNode AddToXml(TNode parentNode, bool recursive) {
                return parentNode.AddElement("filter");
            }
        }
    }
}
