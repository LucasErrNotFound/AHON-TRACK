using AHON_TRACK.Models;
using AHON_TRACK.Services.Events;
using HotAvalonia;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using SkiaSharp;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace AHON_TRACK.ViewModels;

[Page("dashboard")]
public sealed class DashboardViewModel : ViewModelBase, INotifyPropertyChanged, INavigable
{
    #region Private Fields

    private readonly PageManager _pageManager;
    private readonly DashboardModel _dashboardModel;
    private int _selectedYearIndex;
    private ISeries[] _series = [];
    private ObservableCollection<int> _availableYears = [];
    private ObservableCollection<SalesItem> _recentSales = [];
    private ObservableCollection<TrainingSession> _upcomingTrainingSessions = [];
    private ObservableCollection<RecentLog> _recentLogs = [];
    private string _salesSummary = "You made 0 sales this month.";
    private string _trainingSessionsSummary = "You have 0 upcoming training schedules this week";
    private string _recentLogsSummary = "You have 0 recent action logs today";
    public event EventHandler RecentLogsUpdated;
    public void NotifyRecentLogsUpdated() => RecentLogsUpdated?.Invoke(this, EventArgs.Empty);

    #endregion

    #region Public Properties

    public ObservableCollection<int> AvailableYears
    {
        get => _availableYears;
        set
        {
            _availableYears = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<SalesItem> RecentSales
    {
        get => _recentSales;
        set
        {
            _recentSales = value;
            OnPropertyChanged();
            UpdateSalesSummary();
        }
    }

    public ObservableCollection<TrainingSession> UpcomingTrainingSessions
    {
        get => _upcomingTrainingSessions;
        set
        {
            _upcomingTrainingSessions = value;
            OnPropertyChanged();
            UpdateTrainingSessionsSummary();
        }
    }

    public ObservableCollection<RecentLog> RecentLogs
    {
        get => _recentLogs;
        set
        {
            _recentLogs = value;
            OnPropertyChanged();
            UpdateRecentLogsSummary();
        }
    }

    public string SalesSummary
    {
        get => _salesSummary;
        set
        {
            _salesSummary = value;
            OnPropertyChanged();
        }
    }

    public string TrainingSessionsSummary
    {
        get => _trainingSessionsSummary;
        set
        {
            _trainingSessionsSummary = value;
            OnPropertyChanged();
        }
    }

    public string RecentLogsSummary
    {
        get => _recentLogsSummary;
        set
        {
            _recentLogsSummary = value;
            OnPropertyChanged();
        }
    }

    public int SelectedYearIndex
    {
        get => _selectedYearIndex;
        set
        {
            if (_selectedYearIndex != value)
            {
                _selectedYearIndex = value;
                OnPropertyChanged();
                UpdateChartData();
            }
        }
    }

    public ISeries[] Series
    {
        get => _series;
        set
        {
            _series = value;
            OnPropertyChanged();
        }
    }

    public Axis[] XAxes { get; set; } = [];
    public Axis[] YAxes { get; set; } = [];

    #endregion

    #region Constructor

    public DashboardViewModel()
    {
        _pageManager = new PageManager(new ServiceProvider());
        _dashboardModel = new DashboardModel();
        InitializeViewModel();
    }

    // Constructor for dependency injection (if needed)
    public DashboardViewModel(DashboardModel dashboardModel, PageManager pageManager)
    {
        _pageManager = pageManager;
        _dashboardModel = dashboardModel ?? throw new ArgumentNullException(nameof(dashboardModel));
        InitializeViewModel();
    }

    // Constructor for dependency injection without DashboardModel (if DashboardModel is not registered)
    public DashboardViewModel(PageManager pageManager)
    {
        _pageManager = pageManager;
        _dashboardModel = new DashboardModel();
        InitializeViewModel();
    }

    #endregion

    #region Initialization Methods

    private async Task InitializeViewModel()
    {
        InitializeAvailableYears();
        InitializeAxes();
        InitializeChart();
        InitializeSalesData();
        InitializeTrainingSessionsData();
        await RefreshRecentLogs();

        DashboardEventService.Instance.RecentLogsUpdated += async (s, e) =>
        {
            await RefreshRecentLogs();
        };
        DashboardEventService.Instance.SalesUpdated += async (s, e) => await LoadSalesFromDatabaseAsync();
        DashboardEventService.Instance.TrainingSessionsUpdated += async (s, e) => await LoadTrainingSessionsFromDatabaseAsync();
    }

    private void InitializeAvailableYears()
    {
        var years = _dashboardModel.GetAvailableYears();
        AvailableYears = new ObservableCollection<int>(years);
    }

    private void InitializeSalesData()
    {
        var salesData = _dashboardModel.GetSampleSalesData();
        RecentSales = new ObservableCollection<SalesItem>(salesData);
    }

    private void InitializeTrainingSessionsData()
    {
        var sessionsData = _dashboardModel.GetSampleTrainingSessionsData();
        UpcomingTrainingSessions = new ObservableCollection<TrainingSession>(sessionsData);
    }

    public async Task RefreshRecentLogs()
    {
        await LoadRecentLogsFromDatabaseAsync();
    }

    private void InitializeAxes()
    {
        XAxes =
        [
            new Axis
                {
                    Name = "Months",
                    NamePaint = new SolidColorPaint(SKColors.Red),
                    Labels = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"],
                    LabelsPaint = new SolidColorPaint(SKColors.DarkGray),
                    TextSize = 13,
                    MinStep = 1,
                }
        ];

        YAxes =
        [
            new Axis
                {
                    Name = "Profit",
                    NamePaint = new SolidColorPaint(SKColors.Green),
                    LabelsPaint = new SolidColorPaint(SKColors.DarkGray),
                    TextSize = 13,
                    SeparatorsPaint = new SolidColorPaint(SKColors.Gray)
                    {
                        StrokeThickness = 2,
                        PathEffect = new DashEffect([3, 3])
                    },
                    Labeler = value => Labelers.FormatCurrency(value, ",", ".", "₱"),
                }
        ];
    }

    private void InitializeChart()
    {
        UpdateChartData();
    }

    #endregion

    #region Data Loading Methods

    public async Task LoadSalesFromDatabaseAsync()
    {
        try
        {
            var salesFromDb = await _dashboardModel.GetSalesFromDatabaseAsync();

            RecentSales.Clear();
            foreach (var sale in salesFromDb)
            {
                RecentSales.Add(sale);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading sales data: {ex.Message}"); // Don't ask me why this is a Console-based error handling :)
        }
    }

    public async Task LoadTrainingSessionsFromDatabaseAsync()
    {
        try
        {
            var sessionsFromDb = await _dashboardModel.GetTrainingSessionsFromDatabaseAsync();

            UpcomingTrainingSessions.Clear();
            foreach (var session in sessionsFromDb)
            {
                UpcomingTrainingSessions.Add(session);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading training sessions data: {ex.Message}"); // Don't ask me why this is a Console-based error handling :)
        }
    }

    public async Task LoadRecentLogsFromDatabaseAsync()
    {
        try
        {
            var logsFromDb = await _dashboardModel.GetRecentLogsFromDatabaseAsync(
                DashboardModel.connectionString // Pass it here
            );

            RecentLogs.Clear();
            foreach (var log in logsFromDb)
            {
                RecentLogs.Add(log);
            }

            UpdateRecentLogsSummary(); // This will call the GenerateRecentLogsSummary method
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading recent logs data: {ex.Message}");
        }
    }

    #endregion

    #region CRUD Operations for Sales

    public void AddSale(SalesItem newSale)
    {
        RecentSales.Insert(0, newSale); // Add to the beginning for "recent" sales
        UpdateSalesSummary();
    }

    public void RemoveSale(SalesItem sale)
    {
        RecentSales.Remove(sale);
        UpdateSalesSummary();
    }

    #endregion

    #region CRUD Operations for Training Sessions

    public void AddTrainingSession(TrainingSession newSession)
    {
        // Use model to find the correct insertion index
        var sessionsList = UpcomingTrainingSessions.ToList();
        var insertIndex = _dashboardModel.FindInsertionIndex(sessionsList, newSession);

        UpcomingTrainingSessions.Insert(insertIndex, newSession);
        UpdateTrainingSessionsSummary();
    }

    public void RemoveTrainingSession(TrainingSession session)
    {
        UpcomingTrainingSessions.Remove(session);
        UpdateTrainingSessionsSummary();
    }

    #endregion

    #region CRUD Operations for Recent Logs

    public void AddRecentLog(RecentLog newLog)
    {
        RecentLogs.Insert(0, newLog); // Add to the beginning for "recent" logs
        UpdateRecentLogsSummary();
    }

    public void RemoveRecentLog(RecentLog log)
    {
        RecentLogs.Remove(log);
        UpdateRecentLogsSummary();
    }
    #endregion

    #region Chart Operations

    private void UpdateChartData()
    {
        if (_selectedYearIndex >= 0 && _selectedYearIndex < AvailableYears.Count)
        {
            int selectedYear = AvailableYears[_selectedYearIndex];
            var data = _dashboardModel.GetDataForYear(selectedYear);

            Series =
            [
                new ColumnSeries<int>
                    {
                        Values = data,
                        Fill = new SolidColorPaint(SKColors.DarkSlateBlue),
                        MaxBarWidth = 50,
                        Name = $"{selectedYear} Sales"
                    }
            ];
        }
    }

    public void AddYear(int year)
    {
        if (!AvailableYears.Contains(year))
        {
            var insertIndex = AvailableYears.Count;
            for (int i = 0; i < AvailableYears.Count; i++)
            {
                if (AvailableYears[i] > year)
                {
                    insertIndex = i;
                    break;
                }
            }

            AvailableYears.Insert(insertIndex, year);

            // Use model to add year data
            _dashboardModel.AddYearData(year);
        }
    }

    #endregion

    #region Summary Update Methods

    private void UpdateSalesSummary()
    {
        var count = RecentSales?.Count ?? 0;
        SalesSummary = _dashboardModel.GenerateSalesSummary(count);
    }

    private void UpdateTrainingSessionsSummary()
    {
        var count = UpcomingTrainingSessions?.Count ?? 0;
        TrainingSessionsSummary = _dashboardModel.GenerateTrainingSessionsSummary(count);
    }

    private void UpdateRecentLogsSummary()
    {
        var count = RecentLogs?.Count ?? 0;
        RecentLogsSummary = _dashboardModel.GenerateRecentLogsSummary(count);
    }

    #endregion

    #region HotAvalonia and PropertyChanged

    [AvaloniaHotReload]
    public void Initialize()
    {
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    private new void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    #endregion
}