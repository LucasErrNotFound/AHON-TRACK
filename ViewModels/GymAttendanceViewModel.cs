using AHON_TRACK.Services;
using AHON_TRACK.Services.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using HotAvalonia;
using LiveChartsCore;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using ShadUI;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AHON_TRACK.Services.Interface;
using Microsoft.Extensions.Logging;

namespace AHON_TRACK.ViewModels;

public partial class GymAttendanceViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty] private DateTime _attendanceGroupSelectedFromDate = DateTime.Today.AddMonths(-1);
    [ObservableProperty] private DateTime _attendanceGroupSelectedToDate = DateTime.Today;
    [ObservableProperty] private CustomerPieData[] _customerTypePieDataCollection;
    [ObservableProperty] private ISeries[] _attendanceSeriesCollection;
    [ObservableProperty] private Axis[] _attendanceLineChartXAxes;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isInitialized;

    // === Totals & Percentages ===
    [ObservableProperty] private int _totalWalkIns;
    [ObservableProperty] private int _totalMembers;
    [ObservableProperty] private int _totalAttendance;

    [ObservableProperty] private double _walkInChangePercent;
    [ObservableProperty] private double _memberChangePercent;
    [ObservableProperty] private double _attendanceChangePercent;

    private readonly ToastManager _toastManager;
    private readonly DataCountingService _data;
    private readonly ILogger _logger;

    public GymAttendanceViewModel(ToastManager toastManager, DataCountingService data, ILogger logger)
    {
        _toastManager = toastManager;
        _logger = logger;
        _data = data;

        SubscribeToEvent();
        _ = LoadDataAsync();
    }

    public GymAttendanceViewModel()
    {
        _toastManager = new ToastManager();
        _logger = null!;
    }

    /*
    [AvaloniaHotReload]
    public void Initialize()
    {
        SubscribeToEvent();
        _ = LoadDataAsync();
    }
    */
    
    #region INavigable Implementation

    [AvaloniaHotReload]
    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
    
        if (IsInitialized)
        {
            _logger.LogDebug("GymAttendanceViewModel already initialized");
            return;
        }

        _logger.LogInformation("Initializing GymAttendanceViewModel");

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                LifecycleToken, cancellationToken);

            await LoadDataAsync().ConfigureAwait(false);

            IsInitialized = true;
            _logger.LogInformation("GymAttendanceViewModel initialized successfully");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("GymAttendanceViewModel initialization cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing GymAttendanceViewModel");
        }
    }

    public ValueTask OnNavigatingFromAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Navigating away from GymAttendance");
        return ValueTask.CompletedTask;
    }

    #endregion

    private void SubscribeToEvent()
    {
        var eventService = DashboardEventService.Instance;
        eventService.CheckinAdded += OnAttendanceDataChanged;

    }
    
    private void UnsubscribeFromEvents()
    {
        var eventService = DashboardEventService.Instance;
        eventService.CheckinAdded -= OnAttendanceDataChanged;
    }

    private async void OnAttendanceDataChanged(object? sender, EventArgs e)
    {
        try
        {
            _logger.LogDebug("Detected attendance data change — refreshing");
            await UpdateAttendanceChartAsync().ConfigureAwait(false);
            await UpdateCustomerTypeGroupChartAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected during disposal
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing attendance data after change event");
        }
    }

    private async Task LoadDataAsync()
    {
        try
        {
            _logger.LogDebug("Loading attendance data for date range {From} to {To}", 
                AttendanceGroupSelectedFromDate, AttendanceGroupSelectedToDate);
            
            IsLoading = true;
            await UpdateAttendanceChartAsync().ConfigureAwait(false);
            await UpdateCustomerTypeGroupChartAsync().ConfigureAwait(false);
        
            _logger.LogDebug("Attendance data loaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading attendance data");
            _toastManager?.CreateToast("Error Loading Data")
                .WithContent($"Failed to load attendance data: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task UpdateAttendanceChartAsync()
    {
        if (_data == null)
        {
            _logger.LogWarning("DataCountingService is null, using mock data");
            UpdateAttendanceChartMock();
            return;
        }

        try
        {
            _logger.LogDebug("Updating attendance chart");
        
            var attendanceData = await _data.GetAttendanceDataAsync(
                AttendanceGroupSelectedFromDate,
                AttendanceGroupSelectedToDate
            );

            var dataList = attendanceData.ToList();
            var days = new List<string>();
            var walkIns = new List<double>();
            var gymMembers = new List<double>();

            foreach (var data in dataList)
            {
                days.Add(data.AttendanceDate.ToString("MMM dd"));
                walkIns.Add(data.WalkIns);
                gymMembers.Add(data.Members);
            }

            AttendanceSeriesCollection =
            [
                new LineSeries<double>
                {
                    Name = "Walk-Ins",
                    Values = walkIns,
                    ShowDataLabels = false,
                    Fill = new SolidColorPaint(SKColors.DodgerBlue.WithAlpha(100)),
                    Stroke = new SolidColorPaint(SKColors.DodgerBlue) { StrokeThickness = 2 },
                    GeometryFill = null,
                    GeometryStroke = null,
                    LineSmoothness = 0.3
                },

                new LineSeries<double>
                {
                    Name = "Gym Members",
                    Values = gymMembers,
                    ShowDataLabels = false,
                    Fill = new SolidColorPaint(SKColors.Red.WithAlpha(100)),
                    Stroke = new SolidColorPaint(SKColors.Red) { StrokeThickness = 2 },
                    GeometryFill = null,
                    GeometryStroke = null,
                    LineSmoothness = 0.3
                }
            ];

            AttendanceLineChartXAxes =
            [
                new Axis
                {
                    Labels = days.ToArray(),
                    LabelsPaint = new SolidColorPaint { Color = SKColors.Gray },
                    TextSize = 12,
                    MinStep = 1
                }
            ];

            // === Compute Totals ===
            TotalWalkIns = (int)walkIns.Sum();
            TotalMembers = (int)gymMembers.Sum();
            TotalAttendance = TotalWalkIns + TotalMembers;

            // === Compute Comparison with Past 30 Days ===
            var prevTo = AttendanceGroupSelectedFromDate.AddDays(-1);
            var prevFrom = prevTo.AddDays(-30);

            try
            {
                var previousData = await _data.GetAttendanceDataAsync(prevFrom, prevTo);
                var prevDataList = previousData.ToList();

                var prevWalkIns = prevDataList.Sum(x => x.WalkIns);
                var prevMembers = prevDataList.Sum(x => x.Members);
                var prevTotal = prevWalkIns + prevMembers;

                WalkInChangePercent = ComputeChangePercent(TotalWalkIns, prevWalkIns);
                MemberChangePercent = ComputeChangePercent(TotalMembers, prevMembers);
                AttendanceChangePercent = ComputeChangePercent(TotalAttendance, prevTotal);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error calculating attendance percentage");
                _toastManager?.CreateToast("Calculation Error")
                    .WithContent($"Error calculating attendance percentage: {ex.Message}")
                    .DismissOnClick()
                    .ShowWarning();
            
                // Set to 0 if calculation fails
                WalkInChangePercent = 0;
                MemberChangePercent = 0;
                AttendanceChangePercent = 0;
            }
        
            _logger.LogDebug("Attendance chart updated successfully with {Days} days of data", days.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating attendance chart");
            _toastManager?.CreateToast("Chart Error")
                .WithContent($"Failed to update attendance chart: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    private void UpdateAttendanceChartMock()
    {
        var days = new List<string>();
        var walkIns = new List<double>();
        var gymMembers = new List<double>();
        var random = new Random();

        var currentDate = AttendanceGroupSelectedFromDate;
        while (currentDate <= AttendanceGroupSelectedToDate)
        {
            days.Add(currentDate.ToString("MMM dd"));
            walkIns.Add(random.Next(1, 100));
            gymMembers.Add(random.Next(1, 100));
            currentDate = currentDate.AddDays(1);
        }

        AttendanceSeriesCollection =
        [
            new LineSeries<double>
        {
            Name = "Walk-Ins",
            Values = walkIns,
            ShowDataLabels = false,
            Fill = new SolidColorPaint(SKColors.DodgerBlue.WithAlpha(100)),
            Stroke = new SolidColorPaint(SKColors.DodgerBlue) { StrokeThickness = 2 },
            GeometryFill = null,
            GeometryStroke = null,
            LineSmoothness = 0.3
        },

        new LineSeries<double>
        {
            Name = "Gym Members",
            Values = gymMembers,
            ShowDataLabels = false,
            Fill = new SolidColorPaint(SKColors.Red.WithAlpha(100)),
            Stroke = new SolidColorPaint(SKColors.Red) { StrokeThickness = 2 },
            GeometryFill = null,
            GeometryStroke = null,
            LineSmoothness = 0.3
        }
        ];

        AttendanceLineChartXAxes =
        [
            new Axis
        {
            Labels = days.ToArray(),
            LabelsPaint = new SolidColorPaint { Color = SKColors.Gray },
            TextSize = 12,
            MinStep = 1
        }
        ];

        // === Compute Totals ===
        TotalWalkIns = (int)walkIns.Sum();
        TotalMembers = (int)gymMembers.Sum();
        TotalAttendance = TotalWalkIns + TotalMembers;

        // === Mock: Generate random percentages for design-time ===
        WalkInChangePercent = random.Next(-50, 50);
        MemberChangePercent = random.Next(-50, 50);
        AttendanceChangePercent = random.Next(-50, 50);
    }

    // Helper method to compute percentage change
    private static double ComputeChangePercent(double current, double previous)
    {
        return previous switch
        {
            0 when current > 0 => 100.0,
            0 when current == 0 => 0,
            _ => Math.Round(((current - previous) / previous) * 100.0, 2)
        };
    }

    private async Task UpdateCustomerTypeGroupChartAsync()
    {
        if (_data == null)
        {
            _logger.LogWarning("DataCountingService is null, using mock data for customer types");
            UpdateCustomerTypeGroupChartMock();
            return;
        }

        try
        {
            _logger.LogDebug("Updating customer type chart");
        
            var percentages = await _data.GetCustomerTypePercentagesAsync(
                AttendanceGroupSelectedFromDate,
                AttendanceGroupSelectedToDate
            );

            CustomerTypePieDataCollection =
            [
                new CustomerPieData("Gym Members", [null, null, percentages.MembersPercentage], "#1976D2"),
                new CustomerPieData("Walk-Ins", [null, null, percentages.WalkInsPercentage], "#D32F2F")
            ];
        
            _logger.LogDebug("Customer type chart updated: Members {Members}%, Walk-Ins {WalkIns}%", 
                percentages.MembersPercentage, percentages.WalkInsPercentage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating customer type chart");
            _toastManager?.CreateToast("Chart Error")
                .WithContent($"Failed to update customer type chart: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    private void UpdateCustomerTypeGroupChartMock()
    {
        var random = new Random();
        var walkInsPercentage = random.Next(30, 70);
        var gymMembersPercentage = 100 - walkInsPercentage;

        CustomerTypePieDataCollection =
        [
            new CustomerPieData("Gym Members", [null, null, gymMembersPercentage], "#1976D2"),
            new CustomerPieData("Walk-Ins", [null, null, walkInsPercentage], "#D32F2F")
        ];
    }

    partial void OnAttendanceGroupSelectedFromDateChanged(DateTime value)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await LoadDataAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading data after From date change");
            }
        }, LifecycleToken);
    }

    partial void OnAttendanceGroupSelectedToDateChanged(DateTime value)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await LoadDataAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading data after To date change");
            }
        }, LifecycleToken);
    }
    
    #region Disposal

    protected override async ValueTask DisposeAsyncCore()
    {
        _logger.LogInformation("Disposing GymAttendanceViewModel");

        // Unsubscribe from events
        UnsubscribeFromEvents();

        // Clear collections
        CustomerTypePieDataCollection = [];
        AttendanceSeriesCollection = [];

        await base.DisposeAsyncCore().ConfigureAwait(false);
    }

    #endregion
}

public class CustomerPieData(string name, double?[] values, string color)
{
    public string Name { get; set; } = name;
    public double?[] Values { get; set; } = values.Select(v => 
        v.HasValue ? Math.Round(v.Value, 1) : v).ToArray();
    public string Color { get; set; } = color;
    public bool IsTotal => Name is "Walk-Ins" or "Gym Members";
    public Func<ChartPoint, string> Formatter { get; } = point =>
        $"{name}{Environment.NewLine}{point.StackedValue!.Share:P2}";
}