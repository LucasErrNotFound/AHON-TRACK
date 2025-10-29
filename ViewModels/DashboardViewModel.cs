using AHON_TRACK.Models;
using AHON_TRACK.Services;
using AHON_TRACK.Services.Events;
using AHON_TRACK.Services.Interface;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using Microsoft.Identity.Client;
using ShadUI;
using SkiaSharp;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Notification = AHON_TRACK.Models.Notification;

namespace AHON_TRACK.ViewModels;

[Page("dashboard")]
public sealed partial class DashboardViewModel : ViewModelBase, INotifyPropertyChanged, INavigable
{
    #region Private Fields

    private readonly PageManager _pageManager;
    private readonly IDashboardService _dashboardService;
    private readonly IInventoryService _inventoryService;
    private readonly IProductService _productService;
    private readonly IMemberService _memberService;
    private readonly ToastManager _toastManager;
    private readonly DashboardModel _dashboardModel;
    private int _selectedYearIndex;
    private ISeries[] _series = [];
    private ObservableCollection<int> _availableYears = [];
    private ObservableCollection<SalesItem> _recentSales = [];
    private ObservableCollection<TrainingSession> _upcomingTrainingSessions = [];
    private ObservableCollection<RecentLog> _recentLogs = [];
    private ObservableCollection<Notification> _notifications = [];
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

    public ObservableCollection<Notification> Notifications
    {
        get => _notifications;
        set
        {
            _notifications = value;
            OnPropertyChanged();
        }
    }

    public Axis[] XAxes { get; set; } = [];
    public Axis[] YAxes { get; set; } = [];

    private double _totalRevenue;
    public double TotalRevenue
    {
        get => _totalRevenue;
        set
        {
            _totalRevenue = value;
            OnPropertyChanged();
        }
    }

    private int _memberSubscriptions;
    public int MemberSubscriptions
    {
        get => _memberSubscriptions;
        set
        {
            _memberSubscriptions = value;
            OnPropertyChanged();
        }
    }

    private int _salesCount;
    public int SalesCount
    {
        get => _salesCount;
        set
        {
            _salesCount = value;
            OnPropertyChanged();
        }
    }

    private int _activeNow;
    public int ActiveNow
    {
        get => _activeNow;
        set
        {
            _activeNow = value;
            OnPropertyChanged();
        }
    }

    // Growth percentages
    private double _revenueGrowth;
    public double RevenueGrowth
    {
        get => _revenueGrowth;
        set
        {
            _revenueGrowth = value;
            OnPropertyChanged();
        }
    }

    private double _subscriptionsGrowth;
    public double SubscriptionsGrowth
    {
        get => _subscriptionsGrowth;
        set
        {
            _subscriptionsGrowth = value;
            OnPropertyChanged();
        }
    }

    private double _salesGrowth;
    public double SalesGrowth
    {
        get => _salesGrowth;
        set
        {
            _salesGrowth = value;
            OnPropertyChanged();
        }
    }

    private double _activeNowGrowth;
    public double ActiveNowGrowth
    {
        get => _activeNowGrowth;
        set
        {
            _activeNowGrowth = value;
            OnPropertyChanged();
        }
    }

    public string ActiveNowGrowthFormatted
    {
        get
        {
            if (ActiveNowGrowth >= 0)
                return $"+{ActiveNowGrowth}% an hour ago";
            else
                return $"{ActiveNowGrowth}% an hour ago";
        }
    }

    #endregion

    #region Constructor

    public DashboardViewModel(ToastManager toastManager, PageManager pageManager, DashboardModel dashboardModel,
        IDashboardService dashboardService, IInventoryService inventoryService, IProductService productService,
        IMemberService memberService)
    {
        _toastManager = toastManager ?? throw new ArgumentNullException(nameof(toastManager));
        _pageManager = pageManager ?? throw new ArgumentNullException(nameof(pageManager));
        _dashboardModel = dashboardModel ?? throw new ArgumentNullException(nameof(dashboardModel));
        _dashboardService = dashboardService ?? throw new ArgumentNullException(nameof(dashboardService));
        _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
        _productService = productService ?? throw new ArgumentNullException(nameof(productService));
        _memberService = memberService ?? throw new ArgumentNullException(nameof(memberService));

        _inventoryService.RegisterNotificationCallback(AddNotification);
        _productService.RegisterNotificationCallback(AddNotification);
        _memberService.RegisterNotificationCallback(AddNotification);

        _ = InitializeViewModel();
    }

    public DashboardViewModel(PageManager pageManager, IDashboardService dashboardService)
    {
        _pageManager = pageManager ?? throw new ArgumentNullException(nameof(pageManager));
        _dashboardService = dashboardService ?? throw new ArgumentNullException(nameof(dashboardService));
        _ = InitializeViewModel();
    }

    #endregion

    #region Initialization Methods

    private async Task InitializeViewModel()
    {
        InitializeAxes();

        await InitializeAvailableYears();
        await InitializeChart();
        await InitializeSalesData();
        await InitializeTrainingSessionsData();
        await RefreshRecentLogs();
        await LoadDashboardSummary();

        // await _inventoryService.ShowEquipmentAlertsAsync();
        // await _productService.ShowProductAlertsAsync();

        DashboardEventService.Instance.RecentLogsUpdated += async (s, e) =>
        {
            await RefreshRecentLogs();
        };
        DashboardEventService.Instance.ChartDataUpdated += async (s, e) =>
        {
            await UpdateChartData(); // Direct chart refresh
        };
        DashboardEventService.Instance.SalesUpdated += async (s, e) =>
        {
            await LoadSalesFromDatabaseAsync();
            await LoadDashboardSummary();

        };
        DashboardEventService.Instance.CheckinAdded += async (s, e) => await LoadDashboardSummary();
        DashboardEventService.Instance.CheckoutAdded += async (s, e) => await LoadDashboardSummary();
        DashboardEventService.Instance.TrainingSessionsUpdated += async (s, e) => await LoadTrainingSessionsFromDatabaseAsync();
    }

    private async Task InitializeAvailableYears()
    {
        try
        {
            var years = await _dashboardService.GetAvailableYearsAsync();
            AvailableYears = new ObservableCollection<int>(years);

            if (AvailableYears.Count > 0)
            {
                SelectedYearIndex = 0;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading available years: {ex.Message}");
        }
    }

    private async Task InitializeSalesData()
    {
        await LoadSalesFromDatabaseAsync();
    }

    private async Task InitializeTrainingSessionsData()
    {
        await LoadTrainingSessionsFromDatabaseAsync();
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

    private async Task InitializeChart()
    {
        await UpdateChartData();
    }

    #endregion

    #region Data Loading Methods

    public async Task LoadSalesFromDatabaseAsync()
    {
        try
        {
            var salesFromDb = await _dashboardService.GetSalesAsync(5);

            RecentSales.Clear();
            foreach (var sale in salesFromDb)
            {
                RecentSales.Add(sale);
            }

            // Update summary using service
            var summary = await _dashboardService.GenerateSalesSummaryAsync(5);
            SalesSummary = summary;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading sales data: {ex.Message}");
        }
    }

    public async Task LoadTrainingSessionsFromDatabaseAsync()
    {
        try
        {
            var sessionsFromDb = await _dashboardService.GetTrainingSessionsAsync(5);

            UpcomingTrainingSessions.Clear();
            foreach (var session in sessionsFromDb)
            {
                UpcomingTrainingSessions.Add(session);
            }

            // Update summary using service
            var summary = await _dashboardService.GenerateTrainingSessionsSummaryAsync(5);
            TrainingSessionsSummary = summary;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading training sessions data: {ex.Message}");
        }
    }

    public async Task LoadRecentLogsFromDatabaseAsync()
    {
        try
        {
            var logsFromDb = await _dashboardService.GetRecentLogsAsync(5);

            RecentLogs.Clear();
            foreach (var log in logsFromDb)
            {
                RecentLogs.Add(log);
            }

            // Update summary using service
            var summary = await _dashboardService.GenerateRecentLogSummaryAsync(5);
            RecentLogsSummary = summary;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading recent logs data: {ex.Message}");
        }
    }

    #endregion

    #region SUMMARY CARDS
    private async Task LoadDashboardSummary()
    {
        try
        {
            Console.WriteLine("=== Loading Dashboard Summary ===");

            // Load current month's data
            var currentMonth = DateTime.Now;
            var fromDate = new DateTime(currentMonth.Year, currentMonth.Month, 1);
            var toDate = currentMonth;

            Console.WriteLine($"Date Range: {fromDate:yyyy-MM-dd} to {toDate:yyyy-MM-dd}");

            // Get summary data
            var summary = await _dashboardService.GetDashboardSummaryAsync(fromDate, toDate);
            TotalRevenue = summary.TotalRevenue;
            MemberSubscriptions = summary.MemberSubscriptions;
            SalesCount = summary.SalesCount;

            // Get active now with growth (using Option 2 - yesterday comparison)
            var (activeCount, activeGrowth) = await _dashboardService.GetActiveNowWithGrowthAsync();
            ActiveNow = activeCount;
            ActiveNowGrowth = activeGrowth;

            Console.WriteLine($"TotalRevenue: ₱{TotalRevenue:N2}");
            Console.WriteLine($"MemberSubscriptions: {MemberSubscriptions}");
            Console.WriteLine($"SalesCount: {SalesCount}");
            Console.WriteLine($"ActiveNow: {ActiveNow} (Growth: {ActiveNowGrowth}%)");

            // Get growth data for other metrics
            var growth = await _dashboardService.GetDashboardGrowthAsync(fromDate, toDate);
            RevenueGrowth = growth.RevenueGrowth;
            SubscriptionsGrowth = growth.SubscriptionsGrowth;
            SalesGrowth = growth.SalesGrowth;
            // Don't overwrite ActiveNowGrowth here since we calculated it above

            Console.WriteLine($"RevenueGrowth: {RevenueGrowth}%");
            Console.WriteLine($"SubscriptionsGrowth: {SubscriptionsGrowth}%");
            Console.WriteLine($"SalesGrowth: {SalesGrowth}%");

            Console.WriteLine("=== Dashboard Summary Loaded Successfully ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"!!! Error loading dashboard summary: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        }
    }
    #endregion

    #region CRUD Operations for Sales

    public void AddSale(SalesItem newSale)
    {
        RecentSales.Insert(0, newSale);
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
        var sessionsList = UpcomingTrainingSessions.ToList();
        var insertIndex = 0;

        for (int i = 0; i < sessionsList.Count; i++)
        {
            if (newSession.Date < sessionsList[i].Date)
            {
                insertIndex = i;
                break;
            }
            insertIndex = i + 1;
        }

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
        RecentLogs.Insert(0, newLog);
        UpdateRecentLogsSummary();
    }

    public void RemoveRecentLog(RecentLog log)
    {
        RecentLogs.Remove(log);
        UpdateRecentLogsSummary();
    }

    #endregion

    #region Chart Operations

    private async Task UpdateChartData()
    {
        if (_selectedYearIndex >= 0 && _selectedYearIndex < AvailableYears.Count)
        {
            try
            {
                int selectedYear = AvailableYears[_selectedYearIndex];
                var data = await _dashboardService.GetSalesDataForYearAsync(selectedYear);

                Series =
                [
                    new ColumnSeries<int>
                    {
                        Values = data,
                        Fill = new SolidColorPaint(SKColors.DarkSlateBlue),
                        MaxBarWidth = 45,
                        Name = $"{selectedYear} Sales"
                    }
                ];
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading chart data: {ex.Message}");
            }
        }
    }

    public async Task AddYear(int year)
    {
        if (!AvailableYears.Contains(year))
        {
            var insertIndex = AvailableYears.Count;
            for (int i = 0; i < AvailableYears.Count; i++)
            {
                if (AvailableYears[i] < year)
                {
                    insertIndex = i;
                    break;
                }
            }

            AvailableYears.Insert(insertIndex, year);
        }
    }

    #endregion

    #region Summary Update Methods

    private void UpdateSalesSummary()
    {
        var count = RecentSales?.Count ?? 0;
        var total = RecentSales?.Sum(s => s.Amount) ?? 0;
        SalesSummary = $"Total Sales: {count} orders, ₱{total:N2}";
    }

    private void UpdateTrainingSessionsSummary()
    {
        var count = UpcomingTrainingSessions?.Count ?? 0;
        TrainingSessionsSummary = $"Upcoming Training Sessions: {count}";
    }

    private void UpdateRecentLogsSummary()
    {
        var count = RecentLogs?.Count ?? 0;
        RecentLogsSummary = $"You have {count} recent action logs today";
    }

    #endregion

    #region CRUD Operations for Notifications

    public void AddNotification(Notification newNotification)
    {
        var notificationKey = _dashboardModel.GenerateNotificationKey(newNotification.Title, newNotification.Message);
        newNotification.NotificationKey = notificationKey;

        if (_dashboardModel.IsNotificationAlreadyShown(notificationKey))
        {
            Console.WriteLine($"[AddNotification] Skipping duplicate notification: {notificationKey}");
            return;
        }

        _dashboardModel.MarkNotificationAsShown(notificationKey);
        Notifications.Insert(0, newNotification);
    }

    public void RemoveNotification(Notification notification)
    {
        if (!string.IsNullOrEmpty(notification.NotificationKey))
        {
            _dashboardModel.RemoveNotificationTracking(notification.NotificationKey);
        }

        Notifications.Remove(notification);
    }

    #endregion

    #region Delete Notification

    private void DeleteNotification(Notification? notification)
    {
        if (notification != null)
        {
            RemoveNotification(notification);
        }
    }

    private void ClearAllNotifications()
    {
        _dashboardModel.ClearAllNotificationTracking();
        Notifications.Clear();
    }

    #endregion

    #region HotAvalonia and PropertyChanged

    [AvaloniaHotReload]
    public void Initialize()
    {
    }

    private RelayCommand<Notification>? _deleteNotificationCommand;
    public RelayCommand<Notification> DeleteNotificationCommand =>
        _deleteNotificationCommand ??= new RelayCommand<Notification>(DeleteNotification);

    private RelayCommand? _clearAllNotificationsCommand;
    public RelayCommand ClearAllNotificationsCommand =>
        _clearAllNotificationsCommand ??= new RelayCommand(ClearAllNotifications);

    public new event PropertyChangedEventHandler? PropertyChanged;

    private new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    #endregion

    // Add to DashboardViewModel class

    protected override void DisposeManagedResources()
    {
        // CRITICAL: Unsubscribe from events FIRST
        var eventService = DashboardEventService.Instance;
        eventService.RecentLogsUpdated -= async (s, e) => await RefreshRecentLogs();
        eventService.ChartDataUpdated -= async (s, e) => await UpdateChartData();
        eventService.SalesUpdated -= async (s, e) => { 
            await LoadSalesFromDatabaseAsync();
            await LoadDashboardSummary();
        };
        eventService.CheckinAdded -= async (s, e) => await LoadDashboardSummary();
        eventService.CheckoutAdded -= async (s, e) => await LoadDashboardSummary();
        eventService.TrainingSessionsUpdated -= async (s, e) => await LoadTrainingSessionsFromDatabaseAsync();

        // Unregister notification callbacks
        _inventoryService.UnregisterNotificationCallback();
        _productService.UnRegisterNotificationCallback();
        _memberService.UnRegisterNotificationCallback();
        
        (_inventoryService as IDisposable)?.Dispose();
        (_productService as IDisposable)?.Dispose();
        (_memberService as IDisposable)?.Dispose();
        (_dashboardService as IDisposable)?.Dispose();

        // Clear observable collections
        RecentSales?.Clear();
        UpcomingTrainingSessions?.Clear();
        RecentLogs?.Clear();
        Notifications?.Clear();
        AvailableYears?.Clear();

        // Clear chart data
        Series = Array.Empty<ISeries>();
        XAxes = Array.Empty<Axis>();
        YAxes = Array.Empty<Axis>();

        base.DisposeManagedResources();
        ForceGarbageCollection();
    }
}