using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using HotAvalonia;
using LiveChartsCore;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using ShadUI;
using SkiaSharp;

namespace AHON_TRACK.ViewModels;

public partial class FinancialReportsViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty]
    private FinancialBreakdownPieData[] _financialBreakdownPieDataCollection;
    
    [ObservableProperty]
    private DateTime _financialBreakdownSelectedFromDate = DateTime.Today.AddMonths(-1);
    
    [ObservableProperty]
    private DateTime _financialBreakdownSelectedToDate = DateTime.Today;
    
    [ObservableProperty]
    private ISeries[] _revenueSeriesCollection;
    
    [ObservableProperty]
    private Axis[] _revenueChartXAxes;
    
    [ObservableProperty]
    private Axis[] _revenueChartYAxes;
    
    [ObservableProperty]
    private bool _hasValidDateRange = true;
    
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;

    public FinancialReportsViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        
        UpdateRevenueChart();
        UpdateFinancialBreakdownChart();
    }

    public FinancialReportsViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
    }
    
    private void UpdateFinancialBreakdownChart()
    {
        if (FinancialBreakdownSelectedFromDate > FinancialBreakdownSelectedToDate)
        {
            HasValidDateRange = false;
            FinancialBreakdownPieDataCollection = [];
            return;
        }

        HasValidDateRange = true;

        if (RevenueSeriesCollection.Length == 0)
            return;

        var categoryTotals = new List<(string Name, double Total, string Color)>();

        foreach (var series in RevenueSeriesCollection)
        {
            if (series is not StackedColumnSeries<double> stackedSeries) continue;
            if (stackedSeries.Values == null) continue;
    
            var total = stackedSeries.Values.Sum();
            var color = stackedSeries.Name switch
            {
                "Boxing" => "#1E88E5",
                "Muay Thai" => "#E53935",
                "Crossfit" => "#43A047",
                "Zumba" => "#8E24AA",
                _ => "#1976D2"
            };

            categoryTotals.Add((stackedSeries.Name, total, color)!);
        }

        // Create pie chart data with the correct individual totals
        FinancialBreakdownPieDataCollection = categoryTotals
            .Where(ct => ct.Total > 0)
            .Select(ct => new FinancialBreakdownPieData(ct.Name, ct.Total, ct.Color))
            .ToArray();
    }

    private void UpdateRevenueChart()
    {
        var dates = new List<string>();
        var boxingRevenue = new List<double>();
        var muayThaiRevenue = new List<double>();
        var crossfitRevenue = new List<double>();
        var zumbaRevenue = new List<double>();
        var random = new Random();

        var boxingPrices = new[] { 450, 350 };
        var muayThaiPrices = new[] { 500, 400 };
        var crossfitPrices = new[] { 300, 250 };
        var zumbaPrices = new[] { 200, 100 };

        var currentDate = FinancialBreakdownSelectedFromDate;
        while (currentDate <= FinancialBreakdownSelectedToDate)
        {
            dates.Add(currentDate.ToString("MMM dd"));
        
            var boxingPurchases = random.Next(0, 20);
            var boxingPrice = boxingPrices[random.Next(boxingPrices.Length)];
            boxingRevenue.Add(boxingPurchases * boxingPrice);
        
            var muayThaiPurchases = random.Next(0, 20);
            var muayThaiPrice = muayThaiPrices[random.Next(muayThaiPrices.Length)];
            muayThaiRevenue.Add(muayThaiPurchases * muayThaiPrice);
        
            var crossfitPurchases = random.Next(0, 20);
            var crossfitPrice = crossfitPrices[random.Next(crossfitPrices.Length)];
            crossfitRevenue.Add(crossfitPurchases * crossfitPrice);
        
            var zumbaPurchases = random.Next(0, 20);
            var zumbaPrice = zumbaPrices[random.Next(zumbaPrices.Length)];
            zumbaRevenue.Add(zumbaPurchases * zumbaPrice);
        
            currentDate = currentDate.AddDays(1);
        }

        RevenueSeriesCollection =
        [
            new StackedColumnSeries<double>
            {
                Values = boxingRevenue,
                Name = "Boxing",
                Fill = new SolidColorPaint(SKColors.DodgerBlue),
                Stroke = null,
                XToolTipLabelFormatter = point => $"Boxing: ₱{point.Coordinate.PrimaryValue:N0} ({point.StackedValue!.Share:P0})"
            },
            new StackedColumnSeries<double>
            {
                Values = muayThaiRevenue,
                Name = "Muay Thai",
                Fill = new SolidColorPaint(SKColors.Red),
                Stroke = null,
                XToolTipLabelFormatter = point => $"Muay Thai: ₱{point.Coordinate.PrimaryValue:N0} ({point.StackedValue!.Share:P0})"
            },
            new StackedColumnSeries<double>
            {
                Values = crossfitRevenue,
                Name = "Crossfit",
                Fill = new SolidColorPaint(SKColors.LimeGreen),
                Stroke = null,
                XToolTipLabelFormatter = point => $"Crossfit: ₱{point.Coordinate.PrimaryValue:N0} ({point.StackedValue!.Share:P0})"
            },
            new StackedColumnSeries<double>
            {
                Values = zumbaRevenue,
                Name = "Zumba",
                Fill = new SolidColorPaint(SKColors.Purple),
                Stroke = null,
                XToolTipLabelFormatter = point => $"Zumba: ₱{point.Coordinate.PrimaryValue:N0} ({point.StackedValue!.Share:P0})"
            }
        ];

        RevenueChartXAxes =
        [
            new Axis
            {
                Labels = dates.ToArray(),
                LabelsPaint = new SolidColorPaint { Color = SKColors.Gray },
                TextSize = 12,
                MinStep = 1,
                LabelsRotation = dates.Count > 10 ? 45 : 0
            }
        ];
    
        RevenueChartYAxes =
        [
            new Axis
            {
                LabelsPaint = new SolidColorPaint { Color = SKColors.Gray },
                TextSize = 12,
                MinStep = 1,
                SeparatorsPaint = new SolidColorPaint(SKColors.Gray)
                {
                    StrokeThickness = 2,
                    PathEffect = new DashEffect([3, 3])
                },
                Labeler = value => Labelers.FormatCurrency(value, ",", ".", "₱"),
                LabelsRotation = dates.Count > 10 ? 45 : 0
            }
        ];
    }

    partial void OnFinancialBreakdownSelectedFromDateChanged(DateTime value)
    {
        UpdateRevenueChart();
        UpdateFinancialBreakdownChart();
    }
    
    partial void OnFinancialBreakdownSelectedToDateChanged(DateTime value)
    {
        UpdateRevenueChart();
        UpdateFinancialBreakdownChart();
    }
}

public class FinancialBreakdownPieData
{
    public string Name { get; set; }
    public double Value { get; set; }
    public double?[] Values { get; set; }
    public string Color { get; set; }
    public bool IsTotal => Name is "Boxing" or "Muay Thai" or "Crossfit" or "Zumba";
    public Func<ChartPoint, string> Formatter { get; }
    
    public FinancialBreakdownPieData(string name, double value, string color)
    {
        Name = name;
        Value = value;
        // For pie charts in LiveCharts, typically only one value is needed
        // If your pie chart expects an array, use [value] instead of [null, null, value]
        Values = [value];
        Color = color;
        Formatter = _ => $"{name}{Environment.NewLine}₱{value:N2}";
    }
}