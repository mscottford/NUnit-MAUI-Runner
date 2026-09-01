using System.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using NUnit.Runner.Messages;
using NUnit.Runner.ViewModel;

namespace NUnit.Runner.View {
    public partial class SummaryView : ContentPage {
        public static SummaryView Instance { get; private set; }

        SummaryViewModel _model;

        internal SummaryView (SummaryViewModel model) {
            if(Instance != null) throw new Exception();
            Instance = this;

            _model = model;
		    _model.Navigation = Navigation;
		    BindingContext = _model;
			InitializeComponent();

            _model.Completed += (sender, args) => { chart.UpdateChart(); };
            WeakReferenceMessenger.Default.Register<ErrorMessage>(this, (recipient, error) => {
                Dispatcher.Dispatch(() => errorElement.Text = error.Message);
            });
        }
        protected override void OnAppearing() {
            base.OnAppearing();
            _model.OnAppearing();
        }

        public IView GetTesUI() {
            return testUIHost.Children[0];
        }
        public void SetTestUI(IView content) {
            if(content == null) {
                testUIHostContainer.IsVisible = false;
                container.IsVisible = true;
                testUIHost.Children.Clear();
                return;
            }
            testUIHostContainer.IsVisible = true;
            container.IsVisible = false;
            testUIHost.Children.Add(content);
        }
    }
}
