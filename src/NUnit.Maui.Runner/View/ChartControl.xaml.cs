using NUnit.Runner.Helpers;

namespace NUnit.Runner.View;

public partial class ChartControl: Grid {
    // The drawing box, matching WidthRequest/HeightRequest in ChartControl.xaml.
    private const int Size = 100;

    // Matches StrokeThickness on the polylines in ChartControl.xaml.
    private const int StrokeThickness = 16;

    // The stroke straddles the arc, so the radius is inset by half of it to keep the ring
    // inside the box. Using Size/2 would clip the outer edge flat on all four sides.
    private const double Radius = (Size - StrokeThickness) / 2.0;
    private const double Centre = Size / 2.0;

    public static readonly BindableProperty SeriesProperty = BindableProperty.Create(
        propertyName: nameof(Series),
        returnType: typeof(ResultSummary),
        declaringType: typeof(ChartControl));
    
    public ResultSummary Series
    {
        get => (ResultSummary)GetValue(SeriesProperty);
        private set {
            SetValue(SeriesProperty, value);
        }
    }

    public ChartControl() {
        InitializeComponent();
    }
    
    public void UpdateChart() {
        if (Series == null)
            return;
        
        double total = Series.TestCount;
        double passed = Series.PassCount / total;
        double errors = (Series.ErrorCount / total) + passed;
        double failure = (Series.FailureCount / total) + errors;
        double ignored = (Series.NotRunCount / total) + failure;

        figure1.Points = CreatePointsByPercentage(ignored);
        figure2.Points = CreatePointsByPercentage(failure);
        figure3.Points = CreatePointsByPercentage(errors);
        figure4.Points = CreatePointsByPercentage(passed);
    }
    
    // The angle a full circle sweeps, starting from the top and running anticlockwise.
    private const double FullSweep = 2 * Math.PI;

    // How far apart the points along an arc are. Small enough to look smooth at this size.
    private const double AngleStep = 0.05;

    private PointCollection CreatePointsByPercentage(double percentage) {
        var points = new PointCollection();

        // An outcome with no tests draws nothing. Sweeping from the start angle and rounding
        // the end up, as this used to, left a visible sliver of colour for a count of zero.
        if (percentage <= 0) {
            return points;
        }

        double sweep = FullSweep * Math.Min(percentage, 1.0);

        // A shape draws in its own coordinate space, which starts at (0,0), so the circle is
        // centred on the box rather than the origin. Centring it on (0,0) puts three quarters
        // of every arc at negative coordinates, where it is clipped away.
        for (double a = 0; a < sweep; a += AngleStep) {
            points.Add(PointAt(-Math.PI + a));
        }

        // Land exactly on the end of the arc rather than wherever the last step happened to
        // fall, so each segment is the size its share of the results says it is.
        points.Add(PointAt(-Math.PI + sweep));

        return points;
    }

    private Point PointAt(double angle) {
        return new Point(
            Centre + Radius * Math.Sin(angle),
            Centre + Radius * Math.Cos(angle));
    }
}