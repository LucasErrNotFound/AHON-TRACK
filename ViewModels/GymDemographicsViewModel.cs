using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using CommunityToolkit.Mvvm.ComponentModel;
using HotAvalonia;
using LiveChartsCore;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using ShadUI;
using SkiaSharp;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore.Kernel.Events;

namespace AHON_TRACK.ViewModels;

public partial class GymDemographicsViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty]
    private DateTime _gymPopulationSelectedFromDate = DateTime.Today;
    
    [ObservableProperty]
    private DateTime _gymPopulationSelectedToDate = DateTime.Today.AddMonths(1);
    
    [ObservableProperty]
    private DateTime _demographicsGroupSelectedFromDate = DateTime.Today;
    
    [ObservableProperty]
    private DateTime _demographicsGroupSelectedToDate = DateTime.Today.AddMonths(1);
    
    [ObservableProperty] 
    private ISeries[] _populationSeriesCollection;

    [ObservableProperty] 
    private Axis[] _populationLineChartXAxes;
    
    [ObservableProperty]
    private PieData[] _genderPieDataCollection;
    
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
        UpdateDemographicsGroupChart();
        UpdatePopulationChart();
        
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
        ((ColumnSeries<double>)AgeSeries[0]).Values = GenerateRandomValues();
    }
    
    private void UpdateSeriesFill(Color primary)
    {
        var color = new SKColor(primary.R, primary.G, primary.B, primary.A);
        if (AgeSeries.Length > 0) ((ColumnSeries<double>)AgeSeries[0]).Fill = new SolidColorPaint(color);
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
    
    [RelayCommand]
    private void OnHoveredPointsChanged(HoverCommandArgs args)
    {
        foreach (var hovered in args.NewPoints ?? [])
        {
            hovered.Context.Visual!.Stroke = new SolidColorPaint(SKColors.Black, 3);
        }

        foreach (var hovered in args.OldPoints ?? [])
        {
            hovered.Context.Visual!.Stroke = null;
        }
    }

    public ISeries[] AgeSeries { get; set; } =
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

    private void UpdatePopulationChart()
    {
        var days = new List<string>();
        var populations = new List<double>();
        var random = new Random();
    
        var currentDate = GymPopulationSelectedFromDate;
        while (currentDate <= GymPopulationSelectedToDate)
        {
            days.Add(currentDate.ToString("MMM dd"));
            populations.Add(random.Next(1, 100));
            currentDate = currentDate.AddDays(1);
        }
    
        PopulationSeriesCollection =
        [
            new LineSeries<double>
            {
                Values = populations,
                ShowDataLabels = false,
                Fill = new SolidColorPaint(SKColors.DodgerBlue.WithAlpha(100)),
                Stroke = new SolidColorPaint(SKColors.Red) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColors.Red),
                GeometryStroke = new SolidColorPaint(SKColors.Black) { StrokeThickness = 2 },
                LineSmoothness = 0.3
            }
        ];
    
        PopulationLineChartXAxes =
        [
            new Axis
            {
                Labels = days.ToArray(),
                LabelsPaint = new SolidColorPaint { Color = SKColors.Gray },
                TextSize = 12,
                MinStep = 1,
            }
        ];
    }
    
    private void UpdateDemographicsGroupChart()
    {
        var random = new Random();
        var values = new double[4];
    
        // Generate random values for age groups
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = random.Next(1, 500);
        }
    
        AgeSeries =
        [
            new ColumnSeries<double>
            {
                Values = values,
                Fill = new SolidColorPaint(SKColors.Transparent)
            }
        ];
    
        // Generate random values for pie chart (Male/Female distribution)
        var malePercentage = random.Next(30, 70);
        var femalePercentage = 100 - malePercentage;
    
        GenderPieDataCollection = 
        [
            new PieData("Male", [null, null, malePercentage], "#1976D2"),
            new PieData("Female", [null, null, femalePercentage], "#D32F2F")
        ];
    
        OnPropertyChanged(nameof(AgeSeries));
    }

    partial void OnDemographicsGroupSelectedFromDateChanged(DateTime value)
    {
        UpdateDemographicsGroupChart();
    }

    partial void OnDemographicsGroupSelectedToDateChanged(DateTime value)
    {
        UpdateDemographicsGroupChart();
    }
    
    partial void OnGymPopulationSelectedFromDateChanged(DateTime value)
    {
        UpdatePopulationChart();
    }

    partial void OnGymPopulationSelectedToDateChanged(DateTime value)
    {
        UpdatePopulationChart();
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