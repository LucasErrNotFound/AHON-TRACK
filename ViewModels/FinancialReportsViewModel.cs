using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using AHON_TRACK.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using HotAvalonia;
using LiveChartsCore;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using ShadUI;
using SkiaSharp;
using AHON_TRACK.Services.Events;

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
    private ISeries[] _revenueSeriesCollection = [];

    [ObservableProperty]
    private Axis[] _revenueChartXAxes;

    [ObservableProperty]
    private Axis[] _revenueChartYAxes;

    [ObservableProperty]
    private bool _hasValidDateRange = true;

    [ObservableProperty]
    private bool _isLoading = false;

    // === Dashboard Summary Cards ===
    [ObservableProperty] private double _totalRevenue;
    [ObservableProperty] private int _sales;
    [ObservableProperty] private int _gymPackageRevenue;
    [ObservableProperty] private int _walkInMember;

    // For growth indicators (optional UI use)
    [ObservableProperty] private double _revenueGrowthPercent;
    [ObservableProperty] private double _salesGrowthPercent;
    [ObservableProperty] private double _gymPackageGrowthPercent;
    [ObservableProperty] private double _walkInGrowthPercent;

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly DataCountingService _dataCountingService;

    public FinancialReportsViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager, DataCountingService dataCountingService)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _dataCountingService = dataCountingService;

        _ = LoadFinancialDataAsync();
        _ = LoadFinancialSummaryAsync();
        SubscribeToEvent();
    }

    public FinancialReportsViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());

        SubscribeToEvent();
        // For design-time, you'll need to handle this appropriately
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        SubscribeToEvent();
    }

    private void SubscribeToEvent()
    {
        var eventService = DashboardEventService.Instance;

        eventService.SalesUpdated += OnFinancialDataChanged;
        eventService.ChartDataUpdated += OnFinancialDataChanged;
        eventService.ProductPurchased += OnFinancialDataChanged;
    }

    private async void OnFinancialDataChanged(object? sender, EventArgs e)
    {
        try
        {
            await LoadFinancialDataAsync();
            await LoadFinancialSummaryAsync();
            await UpdateRevenueChartAsync();
            UpdateFinancialBreakdownChart();
        }
        catch (Exception ex)
        {
            _toastManager?.CreateToast($"Error refreshing financial data: {ex.Message}");
        }
    }

    private async Task LoadFinancialSummaryAsync()
    {
        try
        {
            // Current and previous date ranges for comparison
            var from = FinancialBreakdownSelectedFromDate;
            var to = FinancialBreakdownSelectedToDate;

            var prevFrom = from.AddMonths(-1);
            var prevTo = to.AddMonths(-1);

            // Fetch current and previous data
            var current = await _dataCountingService.GetFinancialSummaryAsync(from, to);
            var previous = await _dataCountingService.GetFinancialSummaryAsync(prevFrom, prevTo);

            // Assign values for current period
            TotalRevenue = current.TotalRevenue;
            Sales = current.TotalSales;
            GymPackageRevenue = current.TotalGymPackages;
            WalkInMember = current.TotalWalkInMembers;

            // Compute growth % safely
            RevenueGrowthPercent = CalculateGrowth(current.TotalRevenue, previous.TotalRevenue);
            SalesGrowthPercent = CalculateGrowth(current.TotalSales, previous.TotalSales);
            GymPackageGrowthPercent = CalculateGrowth(current.TotalGymPackages, previous.TotalGymPackages);
            WalkInGrowthPercent = CalculateGrowth(current.TotalWalkInMembers, previous.TotalWalkInMembers);
        }
        catch (Exception ex)
        {
            _toastManager?.CreateToast($"Error loading financial summary: {ex.Message}");
        }
    }

    private static double CalculateGrowth(double current, double previous)
    {
        if (previous <= 0) return 100;
        return Math.Round(((current - previous) / previous) * 100, 2);
    }

    private async Task LoadFinancialDataAsync()
    {
        if (FinancialBreakdownSelectedFromDate > FinancialBreakdownSelectedToDate)
        {
            HasValidDateRange = false;
            _toastManager?.CreateToast("Invalid date range: 'From' date must be before 'To' date");
            FinancialBreakdownPieDataCollection = [];
            RevenueSeriesCollection = [];
            return;
        }

        HasValidDateRange = true;
        IsLoading = true;

        try
        {
            await UpdateRevenueChartAsync();
            UpdateFinancialBreakdownChart();
        }
        catch (Exception ex)
        {
            _toastManager?.CreateToast($"Error loading financial data: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdateFinancialBreakdownChart()
    {
        if (RevenueSeriesCollection.Length == 0)
        {
            FinancialBreakdownPieDataCollection = [];
            return;
        }

        var categoryTotals = new List<(string Name, double Total, SKColor Color)>();

        foreach (var series in RevenueSeriesCollection)
        {
            if (series is not StackedColumnSeries<double> stackedSeries) continue;
            if (stackedSeries.Values == null) continue;

            var total = stackedSeries.Values.Sum();

            // Skip categories with zero or NaN totals
            if (double.IsNaN(total) || total <= 0) continue;

            // Extract the actual SKColor from the series
            var skColor = SKColors.Gray; // Default
            if (stackedSeries.Fill is SolidColorPaint solidPaint)
            {
                skColor = solidPaint.Color;
            }

            categoryTotals.Add((stackedSeries.Name!, total, skColor));
        }

        // Calculate grand total for percentage calculation
        var grandTotal = categoryTotals.Sum(ct => ct.Total);

        // Only create pie chart data if there's actual data
        if (grandTotal <= 0 || double.IsNaN(grandTotal))
        {
            FinancialBreakdownPieDataCollection = [];
            return;
        }

        // Create pie chart data with the correct percentages
        FinancialBreakdownPieDataCollection = categoryTotals
            .Where(ct => ct.Total > 0 && !double.IsNaN(ct.Total))
            .Select(ct => new FinancialBreakdownPieData(
                ct.Name,
                ct.Total,
                ct.Color,
                grandTotal))
            .ToArray();
    }

    private async Task UpdateRevenueChartAsync()
    {
        if (_dataCountingService == null)
        {
            UpdateRevenueChartWithMockData();
            return;
        }

        try
        {
            // Fetch data from database
            var salesData = await _dataCountingService.GetPackageSalesDataAsync(
                FinancialBreakdownSelectedFromDate,
                FinancialBreakdownSelectedToDate);

            var salesList = salesData.ToList();

            // If no data, show message and return
            if (!salesList.Any())
            {
                RevenueSeriesCollection = [];
                FinancialBreakdownPieDataCollection = [];
                _toastManager?.CreateToast("No sales data found for the selected date range.");
                return;
            }

            // Get unique package types from actual data
            var packageTypes = salesList
                .Select(s => s.PackageType)
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            var dateRange = Enumerable.Range(0, (FinancialBreakdownSelectedToDate - FinancialBreakdownSelectedFromDate).Days + 1)
                .Select(offset => FinancialBreakdownSelectedFromDate.AddDays(offset).Date)
                .ToList();

            var dates = dateRange.Select(d => d.ToString("MMM dd")).ToList();

            // Initialize revenue dictionaries for each package type
            var revenueByType = packageTypes.ToDictionary(
                type => type,
                type => dateRange.ToDictionary(date => date, date => 0.0)
            );

            // Fill in the actual sales data
            foreach (var sale in salesList)
            {
                var saleDate = sale.SaleDate.Date;
                if (revenueByType.ContainsKey(sale.PackageType) &&
                    revenueByType[sale.PackageType].ContainsKey(saleDate))
                {
                    revenueByType[sale.PackageType][saleDate] += sale.Revenue;
                }
            }

            // Create series for each package type with dynamic colors
            var seriesList = new List<StackedColumnSeries<double>>();
            var colorPalette = new[]
            {
                SKColors.DodgerBlue,
                SKColors.Red,
                SKColors.LimeGreen,
                SKColors.Purple,
                SKColors.Orange,
                SKColors.OrangeRed,
                SKColors.DarkGreen,
                SKColors.DarkGoldenrod,
                SKColors.DeepPink,
                SKColors.MediumSeaGreen
            };

            int colorIndex = 0;
            foreach (var packageType in packageTypes)
            {
                var values = dateRange.Select(date => revenueByType[packageType][date]).ToList();

                // Get color based on package type name or use palette
                var skColor = GetColorForPackageType(packageType, colorPalette[colorIndex % colorPalette.Length]);
                colorIndex++;

                // CRITICAL: Create local copy to avoid closure issue in lambda
                var localPackageType = packageType;

                seriesList.Add(new StackedColumnSeries<double>
                {
                    Values = values,
                    Name = localPackageType,
                    Fill = new SolidColorPaint(skColor),
                    Stroke = null,
                  /*  XToolTipLabelFormatter = point =>
                        $"{localPackageType}: ₱{point.Coordinate.PrimaryValue:N0} ({point.StackedValue!.Share:P0})" */
                });
            }

            RevenueSeriesCollection = seriesList.ToArray();

            // Update axes
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
                LabelsRotation = 0
            }
            ];
        }
        catch (Exception ex)
        {
            _toastManager?.CreateToast($"Error updating revenue chart: {ex.Message}");
            RevenueSeriesCollection = [];
        }
    }

    private SKColor GetColorForPackageType(string packageType, SKColor defaultColor)
    {
        // Match color based on package name (case-insensitive partial matching)
        var lowerName = packageType.ToLower();

        if (lowerName.Contains("boxing")) return SKColors.Red;
        if (lowerName.Contains("muay") || lowerName.Contains("thai")) return SKColors.Pink;
        if (lowerName.Contains("crossfit") || lowerName.Contains("cross")) return SKColors.DodgerBlue;
        if (lowerName.Contains("coaching") || lowerName.Contains("coach")) return SKColors.Purple;
        if (lowerName.Contains("walk")) return SKColors.Orange;
        if (lowerName.Contains("member")) return SKColors.Black;

        return defaultColor;
    }

    private void UpdateRevenueChartWithMockData()
    {
        var dates = new List<string>();
        var boxingRevenue = new List<double>();
        var muayThaiRevenue = new List<double>();
        var crossfitRevenue = new List<double>();
        var coachingRevenue = new List<double>();
        var walkInRevenue = new List<double>();
        var membershipRevenue = new List<double>();
        var random = new Random();

        var boxingPrices = new[] { 450, 350 };
        var muayThaiPrices = new[] { 500, 400 };
        var crossfitPrices = new[] { 300, 250 };
        var coachingPrices = new[] { 200, 150 };
        var walkInPrices = new[] { 150, 60 };
        var membershipPrices = new[] { 500 };

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

            var coachingPurchases = random.Next(0, 20);
            var coachingPrice = coachingPrices[random.Next(coachingPrices.Length)];
            coachingRevenue.Add(coachingPurchases * coachingPrice);

            var walkInPurchases = random.Next(0, 20);
            var walkInPrice = walkInPrices[random.Next(walkInPrices.Length)];
            walkInRevenue.Add(walkInPurchases * walkInPrice);

            var membershipPurchases = random.Next(0, 20);
            var membershipPrice = membershipPrices[random.Next(membershipPrices.Length)];
            membershipRevenue.Add(membershipPurchases * membershipPrice);

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
                Values = coachingRevenue,
                Name = "Coaching",
                Fill = new SolidColorPaint(SKColors.Purple),
                Stroke = null,
                XToolTipLabelFormatter = point => $"Coaching: ₱{point.Coordinate.PrimaryValue:N0} ({point.StackedValue!.Share:P0})"
            },
            new StackedColumnSeries<double>
            {
                Values = walkInRevenue,
                Name = "Walk-Ins",
                Fill = new SolidColorPaint(SKColors.Orange),
                Stroke = null,
                XToolTipLabelFormatter = point => $"Walk-Ins: ₱{point.Coordinate.PrimaryValue:N0} ({point.StackedValue!.Share:P0})"
            },
            new StackedColumnSeries<double>
            {
                Values = membershipRevenue,
                Name = "Membership",
                Fill = new SolidColorPaint(SKColors.OrangeRed),
                Stroke = null,
                XToolTipLabelFormatter = point => $"Membership: ₱{point.Coordinate.PrimaryValue:N0} ({point.StackedValue!.Share:P0})"
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
        _ = LoadFinancialDataAsync();
    }

    partial void OnFinancialBreakdownSelectedToDateChanged(DateTime value)
    {
        _ = LoadFinancialDataAsync();
    }

    // Add to FinancialReportsViewModel class

    protected override void DisposeManagedResources()
    {
        // Unsubscribe from events
        var eventService = DashboardEventService.Instance;
        eventService.SalesUpdated -= OnFinancialDataChanged;
        eventService.ChartDataUpdated -= OnFinancialDataChanged;
        eventService.ProductPurchased -= OnFinancialDataChanged;

        // Clear chart data
        RevenueSeriesCollection = [];
        RevenueChartXAxes = [];
        RevenueChartYAxes = [];
        FinancialBreakdownPieDataCollection = [];

        base.DisposeManagedResources();
    }
}

public class FinancialBreakdownPieData
{
    public string Name { get; set; }
    public double Value { get; set; }
    public double?[] Values { get; set; }
    public string Color { get; set; }
    public double Percentage { get; private set; }
    public bool IsTotal { get; set; }
    private readonly double _grandTotal;

    public Func<ChartPoint, string> Formatter { get; }
    public Func<ChartPoint, string> ToolTipFormatter { get; }

    public FinancialBreakdownPieData(string name, double value, SKColor skColor, double grandTotal)
    {
        Name = name;
        Value = value;
        Values = [value];
        _grandTotal = grandTotal;

        // Convert SKColor to hex string
        Color = $"#{skColor.Red:X2}{skColor.Green:X2}{skColor.Blue:X2}";

        // Calculate percentage with NaN safety
        Percentage = (grandTotal > 0 && !double.IsNaN(grandTotal) && !double.IsNaN(value))
            ? (value / grandTotal) * 100
            : 0;

        // Set IsTotal to true for all items (since they're all category totals)
        IsTotal = true;

        // Format percentage label with NaN check
        Formatter = point => double.IsNaN(Percentage) ? "0.0%" : $"{Percentage:F1}%";

        // Format tooltip with NaN check
        ToolTipFormatter = point =>
        {
            var percentStr = double.IsNaN(Percentage) ? "0.0" : $"{Percentage:F1}";
            var valueStr = double.IsNaN(Value) ? "0" : $"{Value:N0}";
            return $"{Name}: ₱{valueStr} ({percentStr}%)";
        };
    }
}