using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Hawa.App.Popup;

public partial class BatteryRing : UserControl
{
    public BatteryRing() { InitializeComponent(); }

    public string Label { set => LabelText.Text = value; }

    public void Update(int? percent, bool charging, int lowThreshold)
    {
        PercentText.Text = percent is { } p ? $"{p}%" : "–";
        Bolt.Visibility = charging ? Visibility.Visible : Visibility.Collapsed;
        Arc.Stroke = percent is { } q && q <= lowThreshold ? new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x3A))
                                                            : new SolidColorBrush(Color.FromRgb(0x34, 0xC7, 0x59));
        Arc.Data = percent is { } r ? ArcGeometry(r / 100.0) : null;
    }

    private static Geometry? ArcGeometry(double fraction)
    {
        if (fraction <= 0) return null;
        const double cx = 28, cy = 28, radius = 25.5;
        double angle = Math.Min(fraction, 0.9999) * 360.0;
        double rad = (angle - 90) * Math.PI / 180.0;
        var start = new Point(cx, cy - radius);
        var end = new Point(cx + radius * Math.Cos(rad), cy + radius * Math.Sin(rad));
        var figure = new PathFigure { StartPoint = start };
        figure.Segments.Add(new ArcSegment(end, new Size(radius, radius), 0, angle > 180, SweepDirection.Clockwise, true));
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }
}
