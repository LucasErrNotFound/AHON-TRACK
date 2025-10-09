using System;
using System.ComponentModel;
using System.Drawing;
using HotAvalonia;
using LiveChartsCore;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using ShadUI;
using SkiaSharp;
using CommunityToolkit.Mvvm.Input;

namespace AHON_TRACK.ViewModels;

public partial class GymDemographicsViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    public PieData[] PieDataCollection { get; set; }
    public Axis[] XAxes { get; set; }
    public Axis[] YAxes { get; set; }
    
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;

    public GymDemographicsViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        
        UpdateSeriesFill(Color.DodgerBlue);
        
        PieDataCollection = 
        [
            new PieData("Male", [null, null, 55], "#1976D2"),
            new PieData("Female", [null, null, 45], "#D32F2F")
        ];
        
        XAxes =
        [
            new Axis
            {
                Name = "Age Range",
                NameTextSize = 16,
                Labels = ["18-29", "30-39", "40-54", "55+"],
                LabelsPaint = new SolidColorPaint { Color = SKColors.Gray },
                TextSize = 12,
                MinStep = 1
            }
        ];

        YAxes =
        [
            new Axis
            {
                Name = "Age Population",
                NameTextSize = 16,
                Labeler = Labelers.Default,
                LabelsPaint = new SolidColorPaint { Color = SKColors.Gray },
                TextSize = 12,
                MinStep = 1,
                ShowSeparatorLines = true,
                SeparatorsPaint = new SolidColorPaint(SKColors.Gray)
                {
                    StrokeThickness = 1,
                    PathEffect = new DashEffect([3, 3]) 
                }
            }
        ];
    }

    public GymDemographicsViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        ((ColumnSeries<double>)Series[0]).Values = GenerateRandomValues();
    }
    
    private void UpdateSeriesFill(Color primary)
    {
        var color = new SKColor(primary.R, primary.G, primary.B, primary.A);
        if (Series.Length > 0) ((ColumnSeries<double>)Series[0]).Fill = new SolidColorPaint(color);
    }

    private SolidColorPaint GetPaint(int index)
    {
        var paints = new[]
        {
            new SolidColorPaint(SKColors.Red),
            new SolidColorPaint(SKColors.LimeGreen),
            new SolidColorPaint(SKColors.DodgerBlue),
            new SolidColorPaint(SKColors.Yellow)
        };

        return paints[index % paints.Length];
    }

    [RelayCommand]
    private void OnPointMeasured(ChartPoint point)
    {
        // This is straight from their documentation, NOT FROM CHATGPT/CLAUDE !!! - LucasErrNotFound
        
        // the PointMeasured command/event is called every time a point is measured,
        // this happens when the chart loads, rezizes or when the data changes, this method
        // is called for every point in the series.

        if (point.Context.Visual is null) return;

        // here we can customize the visual of the point, for example we can set
        // a different color for each point.
        point.Context.Visual.Fill = GetPaint(point.Index);
    }

    public ISeries[] Series { get; set; } =
    [
        new ColumnSeries<double>
        {
            Values = GenerateRandomValues(),
            Fill = new SolidColorPaint(SKColors.Transparent)
        }
    ];

    private static double[] GenerateRandomValues()
    {
        var random = new Random();

        var values = new double[4];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = random.Next(1, 500);
        }

        return values;
    }
}

public class PieData(string name, double?[] values, string color)
{
    public string Name { get; set; } = name;
    public double?[] Values { get; set; } = values;
    public string Color { get; set; } = color;
    public bool IsTotal => Name is "Male" or "Female";
    public Func<ChartPoint, string> Formatter { get; } = point =>
        $"{name}{Environment.NewLine}{point.StackedValue!.Share:P2}";
}