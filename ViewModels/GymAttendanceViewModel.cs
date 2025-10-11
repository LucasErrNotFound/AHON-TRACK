using System;
using System.Collections.Generic;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using HotAvalonia;
using LiveChartsCore;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using ShadUI;
using SkiaSharp;

namespace AHON_TRACK.ViewModels;

public partial class GymAttendanceViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty]
    private DateTime _attendanceGroupSelectedFromDate = DateTime.Today;
    
    [ObservableProperty]
    private DateTime _attendanceGroupSelectedToDate = DateTime.Today.AddMonths(1);
    
    [ObservableProperty]
    private CustomerPieData[] _customerTypePieDataCollection;
    
    [ObservableProperty] 
    private ISeries[] _attendanceSeriesCollection;

    [ObservableProperty] 
    private Axis[] _attendanceLineChartXAxes;
    
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;

    public GymAttendanceViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        
        UpdateAttendanceChart();
        UpdateCustomerTypeGroupChart();
    }

    public GymAttendanceViewModel()
    {
        _dialogManager =  new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
    }
    
    private void UpdateAttendanceChart()
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
    }
    
    private void UpdateCustomerTypeGroupChart()
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
        UpdateAttendanceChart();
        UpdateCustomerTypeGroupChart();
    }

    partial void OnAttendanceGroupSelectedToDateChanged(DateTime value)
    {
        UpdateAttendanceChart();
        UpdateCustomerTypeGroupChart();
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