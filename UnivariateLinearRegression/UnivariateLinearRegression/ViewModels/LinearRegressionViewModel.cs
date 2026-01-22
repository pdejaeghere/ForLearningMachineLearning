using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static UnivariateLinearRegression.ViewModels.LinearRegressionViewModel;
namespace UnivariateLinearRegression.ViewModels;

public partial class LinearRegressionViewModel : ViewModelBase
{
    private const int MaxIterations = 10000;

  
    [ObservableProperty]
    private int _XMax = 10;

    [ObservableProperty]
    private int _YMax = 20;

    [ObservableProperty]
    private string _parseError = "";
    [ObservableProperty]
    private string _equation = "y = 2x + 1";
    
    [ObservableProperty]
    private double _a = 0.0;

    [ObservableProperty]
    private double _b = 0.0;


    [ObservableProperty]
    private double _aOfModel = 0.0;

    [ObservableProperty]
    private double _bOfModel = 0.0;

    [ObservableProperty]
    private int _ExampleCount = 50;

    [ObservableProperty]
    private int _ErrorRange = 1;

    [ObservableProperty]
    private double _tolerance = 1e-6;

    [ObservableProperty]
    private bool _StepByStep = true;

    // Observable collection of generated points (X,Y) for binding to the view
    [ObservableProperty]
    private List<DataPoint> _dataPoints = new();

    // Observable collection of coast values for binding to the view
    [ObservableProperty]
    private List<double> _HistoricalCoasts = new();


    [ObservableProperty]
    private bool _canTrain = false;

    [ObservableProperty]
    private bool _isTraining = false;

    [ObservableProperty]
    private double _learningRate = 0.01;

    [ObservableProperty]
    private int _Iteration = 0;

    // Cancellation support for Stop button
    private CancellationTokenSource? _cts;


    [RelayCommand]
    public void GenerateData()
    {
        CanTrain=false;
        AOfModel = 0;
        BOfModel = 0;
        if (TryParseLinearEquation(Equation, out var a, out var b, out var error))
        {
            A = a;
            B = b;
            ParseError = string.Empty;
            GenerateRandomDataPoint();
            CanTrain = true;
        }
        else
        {
            ParseError = error;
        }
    }
   
    private void GenerateRandomDataPoint()
    {

        // Generate ExampleCount points between 0 and XMax (inclusive)
        var rnd = new Random();
        DataPoints = new();
        HistoricalCoasts = new();
        int count = Math.Max(1, ExampleCount);
        for (int i = 0; i < count; i++)
        {
            double x;
            if (count == 1)
                x = 0;
            else
                x = (double)XMax * i / (count - 1); // evenly spaced from 0 to XMax

            // random error in [-ErrorRange, +ErrorRange]
            double noise = (rnd.NextDouble() * 2.0 - 1.0) * ErrorRange;
            double y = A * x + B + noise;

            DataPoints.Add(new DataPoint(x, y));
        }

        // Adjust YMax to fit generated points (add 1 for margin), ensure at least 1
        if (DataPoints.Any())
        {
            double maxY = DataPoints.Max(p => p.Y);
            double minY = DataPoints.Min(p => p.Y);
            // choose max absolute deviation to ensure whole range fits (optional)
            double top = Math.Max(maxY, Math.Abs(minY));
          //  YMax = Math.Max(1, (int)Math.Ceiling(top + 1.0));
        }
        else
        {
            YMax = Math.Max(1, YMax);
        }
    }


    /// <summary>
    /// y=ax + b
    /// </summary>
    /// <param name="input"></param>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <param name="error"></param>
    /// <returns></returns>
    private static bool TryParseLinearEquation(string input, out double a, out double b, out string error)
    {
        a = 0;
        b = 0;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Empty equation.";
            return false;
        }

        // Normalize
        var s = input.Trim().ToLowerInvariant();
        s = s.Replace(" ", string.Empty);
        s = s.Replace("*", string.Empty);

        // Remove leading "y=" if present
        if (s.StartsWith("y="))
        {
            s = s.Substring(2);
        }

        // If there's no 'x', treat as constant (a=0, b=value)
        var idxX = s.IndexOf('x');
        var culture = CultureInfo.InvariantCulture;
        if (idxX < 0)
        {
            if (double.TryParse(s, NumberStyles.Float | NumberStyles.AllowLeadingSign, culture, out var constVal))
            {
                a = 0;
                b = constVal;
                return true;
            }

            error = "Constant not found";
            return false;
        }

        // Parse a part (before 'x')
        var a_Part = s.Substring(0, idxX);
        if (string.IsNullOrEmpty(a_Part) || a_Part == "+")
            a = 1.0;
        else if (a_Part == "-")
            a = -1.0;
        else if (!double.TryParse(a_Part, NumberStyles.Float | NumberStyles.AllowLeadingSign, culture, out a))
        {
            error = $"a cannot be parsed: '{a_Part}'";
            return false;
        }

        // Parse b part (after 'x'), may be like "+2" or "-1.5" or empty
        var b_Part = s.Substring(idxX + 1);
        if (string.IsNullOrEmpty(b_Part))
        {
            b = 0.0;
            return true;
        }

        // Ensure leading sign for parsing (e.g., "x+2" -> "+2")
        if (b_Part[0] != '+' && b_Part[0] != '-')
            b_Part = "+" + b_Part;

        if (!double.TryParse(b_Part, NumberStyles.Float | NumberStyles.AllowLeadingSign, culture, out b))
        {
            error = $" b cannot be parsed: '{b_Part}'";
            return false;
        }

        return true;
    }


    [RelayCommand]
    public async Task  StartGradientDescent()
    {
        if (IsTraining)
            return;
        Iteration = 0;
         _cts = new CancellationTokenSource();
        var token = _cts.Token;
        IsTraining = true;
        // Run heavy loop on background thread to avoid blocking UI
        var rnd = new Random();
        double a = rnd.NextDouble() * 10.0;
        double b = rnd.NextDouble() * 10.0;


        double prevCost = double.PositiveInfinity;
        int patience = 0;
        int iteration = 0;
        const int updateEvery =  1; // push intermediate A/B to UI periodically
        try
        {
            await Task.Run(async () =>
        {
            for ( iteration = 0; iteration < MaxIterations; iteration++)
            {
                token.ThrowIfCancellationRequested();

                Vector2 grad = Gradient(a, b, DataPoints);
                double grad_a = grad.X;
                double grad_b = grad.Y;
                double gradNorm = Math.Sqrt(grad_a * grad_a + grad_b * grad_b);

                // parameter update
                a = a - LearningRate * grad_a;
                b = b - LearningRate * grad_b;

                
                double cost = Cost(a, b, DataPoints);

                // convergence checks
                
                if (double.IsNaN(cost) || double.IsInfinity(cost))
                    break;

                if (Math.Abs(prevCost - cost) < Tolerance)
                {
                    // small change in cost -> converged
                    break;
                }
                
                if (gradNorm < Tolerance)
                {
                    // gradient is very small -> converged
                    break;
                }

                
                // optional patience mechanism (stop if no meaningful improvement for some iterations)
                if (prevCost - cost < Tolerance * 10)
                {
                    patience++;
                    if (patience > 200) // arbitrary patience threshold
                        break;
                }
                else
                {
                    patience = 0;
                }
               
                prevCost = cost;
                
                // periodically publish intermediate values to UI (keeps UI responsive)
                if (_StepByStep && iteration % updateEvery == 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(1)); 
                    var aa = a; var bb = b;
                    var it = iteration;
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        AOfModel = aa;
                        BOfModel = bb;
                        Iteration = it;
                    });
                }
                else
                {
                    await Task.Yield(); //For the wasm because it needs more frequent yielding to keep UI responsive: monothreaded synchronization 
                }
            }
        }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // stopped by user - swallow or set a flag if needed
        }
        finally
        {
            IsTraining = false;

            // final publish on UI thread
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AOfModel = a;
                BOfModel = b;
                Iteration = iteration;
            });
            _cts?.Dispose();
            _cts = null;
        }
        }


    [RelayCommand]
    public async Task StopGradientDescent()
    {
        if (!IsTraining)
            return;

        _cts?.Cancel();
    }

    /// <summary>
    /// mean squared error cost function (see https://www.youtube.com/watch?v=wg7-roETbbM)
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <param name="points"></param>
    /// <returns></returns>
    double Cost(double a, double b, IEnumerable<DataPoint> points)
    {
        if (!points.Any())
            return 0.0;
        double totalError = 0.0;
        int m = 0;
        foreach (var p in points)
        {
            double predictedY = a * p.X + b;
            double error = predictedY - p.Y;
            totalError += error * error;
            m++;
        }
        return totalError / (2*m);   //2 for mathematical convenience in derivative(later)
    }

    Vector2 Gradient(double a, double b, IEnumerable<DataPoint> points)
    {
      if (points==null || !points.Any())
            return new Vector2(0.0, 0.0);
      Vector2 grad = new Vector2(0.0, 0.0);
        foreach (var p in points)
        {
            double predictedY = a * p.X + b;
            double error = predictedY - p.Y;
            grad.X += p.X * (a * p.X + b - p.Y); // dérivée partielle en a au point p
            grad.Y += (a * p.X + b - p.Y);       // dérivée partielle en b au point p
        }

        return new Vector2(grad.X / points.Count(), grad.Y / points.Count());
    }

    


    // Simple point model used by the ViewModel and bound by the view
    public class DataPoint
    {
        public double X { get; set; }
        public double Y { get; set; }

        public DataPoint(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    public class Vector2
    {
        public double X { get; set; }
        public double Y { get; set; }
        public Vector2(double x, double y)
        {
            X = x;
            Y = y;
        }
    }


}
