using AHON_TRACK.Services;
using AHON_TRACK.Services.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using Dapper;
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
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace AHON_TRACK.ViewModels;

public partial class GymAttendanceViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty]
    private DateTime _attendanceGroupSelectedFromDate = DateTime.Today.AddMonths(-1);

    [ObservableProperty]
    private DateTime _attendanceGroupSelectedToDate = DateTime.Today;

    [ObservableProperty]
    private CustomerPieData[] _customerTypePieDataCollection;

    [ObservableProperty]
    private ISeries[] _attendanceSeriesCollection;

    [ObservableProperty]
    private Axis[] _attendanceLineChartXAxes;

    [ObservableProperty]
    private bool _isLoading;

    // === Totals & Percentages ===
    [ObservableProperty] private int _totalWalkIns;
    [ObservableProperty] private int _totalMembers;
    [ObservableProperty] private int _totalAttendance;

    [ObservableProperty] private double _walkInChangePercent;
    [ObservableProperty] private double _memberChangePercent;
    [ObservableProperty] private double _attendanceChangePercent;

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly DataCountingService _data;

    public GymAttendanceViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager, DataCountingService data)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _data = data;

        SubscribeToEvent();
        _ = LoadDataAsync();
    }

    public GymAttendanceViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        SubscribeToEvent();
        _ = LoadDataAsync();
    }

    private void SubscribeToEvent()
    {
        var eventService = DashboardEventService.Instance;
        eventService.CheckinAdded += OnAttendanceDataChanged;

    }

    private async void OnAttendanceDataChanged(object? sender, EventArgs e)
    {
        await UpdateAttendanceChartAsync();
        await UpdateCustomerTypeGroupChartAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;
            await UpdateAttendanceChartAsync();
            await UpdateCustomerTypeGroupChartAsync();
        }
        catch (Exception ex)
        {
            _toastManager?.CreateToast($"Failed to load attendance data: {ex.Message}");
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
            // Fallback to mock data for design-time
            UpdateAttendanceChartMock();
            return;
        }

        var attendanceData = await _data.GetAttendanceDataAsync(
            AttendanceGroupSelectedFromDate,
            AttendanceGroupSelectedToDate
        );

        var days = new List<string>();
        var walkIns = new List<double>();
        var gymMembers = new List<double>();

        foreach (var data in attendanceData)
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

            var prevWalkIns = previousData.Sum(x => x.WalkIns);
            var prevMembers = previousData.Sum(x => x.Members);
            var prevTotal = prevWalkIns + prevMembers;

            WalkInChangePercent = ComputeChangePercent(TotalWalkIns, prevWalkIns);
            MemberChangePercent = ComputeChangePercent(TotalMembers, prevMembers);
            AttendanceChangePercent = ComputeChangePercent(TotalAttendance, prevTotal);
        }
        catch (Exception ex)
        {
            _toastManager?.CreateToast($"Error calculating attendance percentage: {ex.Message}");
            // Set to 0 if calculation fails
            WalkInChangePercent = 0;
            MemberChangePercent = 0;
            AttendanceChangePercent = 0;
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
        if (previous == 0 && current > 0)
            return 100.0;     // 100% increase
        else if (previous == 0 && current == 0)
            return 0;         // no change at all
        else
            return Math.Round(((current - previous) / previous) * 100.0, 2);
    }

    private async Task UpdateCustomerTypeGroupChartAsync()
    {
        if (_data == null)
        {
            // Fallback to mock data for design-time
            UpdateCustomerTypeGroupChartMock();
            return;
        }

        var percentages = await _data.GetCustomerTypePercentagesAsync(
            AttendanceGroupSelectedFromDate,
            AttendanceGroupSelectedToDate
        );

        CustomerTypePieDataCollection =
        [
            new CustomerPieData("Gym Members", [null, null, percentages.MembersPercentage], "#1976D2"),
            new CustomerPieData("Walk-Ins", [null, null, percentages.WalkInsPercentage], "#D32F2F")
        ];
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
        _ = LoadDataAsync();
    }

    partial void OnAttendanceGroupSelectedToDateChanged(DateTime value)
    {
        _ = LoadDataAsync();
    }
}

public class CustomerPieData(string name, double?[] values, string color)
{
    public string Name { get; set; } = name;
    public double?[] Values { get; set; } = values;
    public string Color { get; set; } = color;
    public bool IsTotal => Name is "Walk-Ins" or "Gym Members";
    public Func<ChartPoint, string> Formatter { get; } = point =>
        $"{name}{Environment.NewLine}{point.StackedValue!.Share:P2}";
}