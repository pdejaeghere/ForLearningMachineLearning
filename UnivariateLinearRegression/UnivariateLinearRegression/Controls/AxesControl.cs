using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using static UnivariateLinearRegression.ViewModels.LinearRegressionViewModel;

namespace UnivariateLinearRegression.Controls;

public class AxesControl : Control
{


    // Collection of data points to draw (bind your ObservableCollection<DataPoint> from VM)
    public static readonly StyledProperty<IEnumerable<DataPoint>?> PointsProperty =
        AvaloniaProperty.Register<AxesControl, IEnumerable<DataPoint>?>(nameof(Points), null);
    // Line parameters for y = a * x + b
    public static readonly StyledProperty<double> LinearParameter_aProperty =
        AvaloniaProperty.Register<AxesControl, double>(nameof(LinearParameter_a), 1.0);

    public static readonly StyledProperty<double> LinearParameter_bProperty =
        AvaloniaProperty.Register<AxesControl, double>(nameof(LinearParameter_b), 0.0);


    public static readonly StyledProperty<double> LinearParameter_aModelProperty =
      AvaloniaProperty.Register<AxesControl, double>(nameof(LinearParameter_aModel), 1.0);

    public static readonly StyledProperty<double> LinearParameter_bModelProperty =
        AvaloniaProperty.Register<AxesControl, double>(nameof(LinearParameter_bModel), 0.0);


    // Axis scale (used to map coordinates to pixels)
    public static readonly StyledProperty<double> XMaxProperty =
        AvaloniaProperty.Register<AxesControl, double>(nameof(XMax), 10.0);

    public static readonly StyledProperty<double> YMaxProperty =
        AvaloniaProperty.Register<AxesControl, double>(nameof(YMax), 5.0);
   

    public IEnumerable<DataPoint>? Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public double LinearParameter_a
    {
        get => GetValue(LinearParameter_aProperty);
        set => SetValue(LinearParameter_aProperty, value);
    }

    public double LinearParameter_b
    {
        get => GetValue(LinearParameter_bProperty);
        set => SetValue(LinearParameter_bProperty, value);
    }

    public double XMax
    {
        get => GetValue(XMaxProperty);
        set => SetValue(XMaxProperty, value);
    }

    public double LinearParameter_aModel
    {
        get => GetValue(LinearParameter_aModelProperty);
        set => SetValue(LinearParameter_aModelProperty, value);
    }

    public double LinearParameter_bModel
    {
        get => GetValue(LinearParameter_bModelProperty);
        set => SetValue(LinearParameter_bModelProperty, value);
    }


    public double YMax
    {
        get => GetValue(YMaxProperty);
        set => SetValue(YMaxProperty, value);
    }
    static readonly Typeface DefaultTypeface = new Typeface("Segoe UI");
    static readonly IBrush AxisBrush = Brushes.Black;
    static readonly Pen AxisPen = new Pen(Brushes.Black, 2);
    static readonly Pen TickPen = new Pen(Brushes.Black, 1);

    // Pens/brushes for points and line
    static readonly IBrush PointBrush = Brushes.Red;
    static readonly Pen BlueLinePen = new Pen(Brushes.Blue, 1.5);
    static readonly Pen RedLinePen = new Pen(Brushes.Red, 1.5);


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (
             change.Property == PointsProperty ||
             change.Property == LinearParameter_aProperty ||
             change.Property == LinearParameter_bProperty ||
                   change.Property == LinearParameter_aModelProperty ||
             change.Property == LinearParameter_bModelProperty ||   
             change.Property == XMaxProperty ||
             change.Property == YMaxProperty)
        {
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext dc)
    {
        base.Render(dc);

        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0 || XMax <= 0 || YMax <= 0)
        {
            return;
        }

        var clipRect = new Rect(0, 0, w, h);
        using var pushedtate = dc.PushClip(clipRect);


        // margins to leave space for labels
        const double leftMargin = 40;
        const double bottomMargin = 30;
        const double topMargin = 10;
        const double rightMargin = 10;

        var origin = new Point(leftMargin, h - bottomMargin);
        var xEnd = new Point(w - rightMargin, origin.Y);
        var yEnd = new Point(origin.X, topMargin);

        // Draw axes
        dc.DrawLine(AxisPen, origin, xEnd); // X axis
        dc.DrawLine(AxisPen, origin, yEnd); // Y axis

        // Draw small arrows
        dc.DrawLine(AxisPen, xEnd, new Point(xEnd.X - 7, xEnd.Y - 4));
        dc.DrawLine(AxisPen, xEnd, new Point(xEnd.X - 7, xEnd.Y + 4));
        dc.DrawLine(AxisPen, yEnd, new Point(yEnd.X - 4, yEnd.Y + 7));
        dc.DrawLine(AxisPen, yEnd, new Point(yEnd.X + 4, yEnd.Y + 7));

        // X ticks and labels 1..XMax
        var usableWidth = xEnd.X - origin.X;
        var ticksX = Math.Max(1, XMax);
        for (int i = 1; i <= ticksX; i++)
        {
            double t = (double)i / ticksX;
            var tx = origin.X + t * usableWidth;
            // tick
            dc.DrawLine(TickPen, new Point(tx, origin.Y), new Point(tx, origin.Y - 5));
            // label
            var label = i.ToString();
            var ft = new FormattedText(
                label,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                DefaultTypeface,
                12,
                AxisBrush
            );
            var textPos = new Point(tx - ft.Width / 2, origin.Y + 4);
            dc.DrawText(ft, textPos);
        }

        // Y ticks and labels 1..YMax
        var usableHeight = origin.Y - yEnd.Y;
        var ticksY = Math.Max(1, YMax);
        for (int i = 1; i <= ticksY; i++)
        {
            double t = (double)i / ticksY;
            var ty = origin.Y - t * usableHeight;
            // tick (horizontal)
            dc.DrawLine(TickPen, new Point(origin.X, ty), new Point(origin.X + 5, ty));
            // label
            var label = i.ToString();
            var ft = new FormattedText(
                label,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                DefaultTypeface,
                12,
                AxisBrush
            );
            var textPos = new Point(origin.X - ft.Width - 6, ty - ft.Height / 2);
            dc.DrawText(ft, textPos);
        }

        if (Points is null||Points.Count()==0)
        {
            return; // nothing more to draw
        }

        // Helpers to map data coordinates to pixel coordinates
        double mapX(double x) => origin.X + (XMax <= 0 ? 0 : (x / XMax) * usableWidth);
        double mapY(double y) => origin.Y - (YMax <= 0 ? 0 : (y / YMax) * usableHeight);

        // Draw line y = a * x + b over [0, XMax]
        
            var p1 = new Point(mapX(0), mapY(LinearParameter_a * 0 + LinearParameter_b));
            var p2 = new Point(mapX(XMax), mapY(LinearParameter_a * XMax + LinearParameter_b));
            dc.DrawLine(RedLinePen, p1, p2);
        
        // Draw points        
            const double radius = 3.0;
            foreach (var pt in Points)
            {
                var px = mapX(pt.X);
                var py = mapY(pt.Y);
                // small circle for point
                dc.DrawEllipse(PointBrush, null, new Point(px, py), radius, radius);
            }

        // Draw line y = a * x + b over [0, XMax] of the model
        if (LinearParameter_aModel==0 && LinearParameter_bModel==0)
        {
            return;
        }
        var pModel1 = new Point(mapX(0), mapY(LinearParameter_aModel * 0 + LinearParameter_bModel));
        var pModel2 = new Point(mapX(XMax), mapY(LinearParameter_aModel * XMax + LinearParameter_bModel));
        dc.DrawLine(BlueLinePen, pModel1, pModel2);

        if (true)
        {
            foreach (var pt in Points)
            {
                var px = mapX(pt.X);
                var pyData = mapY(pt.Y);
                var pyModel = mapY(LinearParameter_aModel * pt.X + LinearParameter_bModel);
                // vertical line from data point to model line
                dc.DrawLine(new Pen(Brushes.Green, 1), new Point(px, pyData), new Point(px, pyModel));
            }
        }


    }
}