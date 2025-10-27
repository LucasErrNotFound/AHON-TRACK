using AHON_TRACK.Models;
using AHON_TRACK.Services.Events;
using AHON_TRACK.Services.Interface;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AHON_TRACK.Services;
using Notification = AHON_TRACK.Models.Notification;

namespace AHON_TRACK.ViewModels;

[Page("dashboard")]
public sealed partial class DashboardViewModel : ViewModelBase, INotifyPropertyChanged, INavigable
{
    #region Private Fields

    private readonly IDashboardService _dashboardService;
    private readonly IInventoryService _inventoryService;
    private readonly IProductService _productService;
    private readonly IMemberService _memberService;
    private readonly DashboardModel _dashboardModel;
    private readonly ILogger _logger;
    
    private CancellationTokenSource? _autoRefreshCts;
    private Timer? _autoRefreshTimer;
    
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
                _ = UpdateChartDataAsync();
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

    public Axis[] XAxes { get; set; } = Array.Empty<Axis>();
    public Axis[] YAxes { get; set; } = Array.Empty<Axis>();

    #endregion

    #region Constructor

    public DashboardViewModel(
        DashboardModel dashboardModel, 
        IDashboardService dashboardService, 
        IInventoryService inventoryService, 
        IProductService productService, 
        IMemberService memberService,
        ILogger logger)
    {
        _dashboardModel = dashboardModel ?? throw new ArgumentNullException(nameof(dashboardModel));
        _dashboardService = dashboardService ?? throw new ArgumentNullException(nameof(dashboardService));
        _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
        _productService = productService ?? throw new ArgumentNullException(nameof(productService));
        _memberService = memberService ?? throw new ArgumentNullException(nameof(memberService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Register notification callbacks
        _inventoryService.RegisterNotificationCallback(AddNotification);
        _productService.RegisterNotificationCallback(AddNotification);
        _memberService.RegisterNotificationCallback(AddNotification);
        
        // Subscribe to events
        DashboardEventService.Instance.RecentLogsUpdated += OnRecentLogsUpdated;
        DashboardEventService.Instance.ChartDataUpdated += OnChartDataUpdated;
        DashboardEventService.Instance.SalesUpdated += OnSalesUpdated;
        DashboardEventService.Instance.TrainingSessionsUpdated += OnTrainingSessionsUpdated;
    }

    // Design-time constructor
    public DashboardViewModel()
    {
        _dashboardModel = new DashboardModel();
        _dashboardService = null!;
        _inventoryService = null!;
        _productService = null!;
        _memberService = null!;
        _logger = null!;
        
        // Design-time sample data
        RecentSales.Add(new SalesItem { ProductName = "Protein Powder", Amount = 1500 });
        UpcomingTrainingSessions.Add(new TrainingSession { TrainingType = "Cardio", Date = DateTime.Now.AddDays(1) });
    }

    #endregion

    #region INavigable Implementation

    [AvaloniaHotReload]
    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        _logger.LogInformation("Initializing DashboardViewModel");
        
        try
        {
            // Create linked cancellation token
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                LifecycleToken, cancellationToken);
            var token = linkedCts.Token;

            InitializeAxes();
            
            // Load all data in parallel for faster startup
            await Task.WhenAll(
                InitializeAvailableYearsAsync(token),
                InitializeChartAsync(token),
                LoadSalesFromDatabaseAsync(token),
                LoadTrainingSessionsFromDatabaseAsync(token),
                LoadRecentLogsFromDatabaseAsync(token)
            ).ConfigureAwait(false);
            
            // Start auto-refresh timer (every 5 minutes)
            StartAutoRefresh();
            
            _logger.LogInformation("Dashboard initialized successfully");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Dashboard initialization cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing dashboard");
        }
    }

    public ValueTask OnNavigatingFromAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Navigating away from Dashboard");
        
        // Stop auto-refresh
        StopAutoRefresh();
        
        return ValueTask.CompletedTask;
    }

    #endregion

    #region Initialization Methods

    private async Task InitializeAvailableYearsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var years = await _dashboardService.GetAvailableYearsAsync()
                .ConfigureAwait(false);
            
            AvailableYears = new ObservableCollection<int>(years);

            if (AvailableYears.Count > 0)
            {
                SelectedYearIndex = 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading available years");
        }
    }

    private void InitializeAxes()
    {
        XAxes = new[]
        {
            new Axis
            {
                Name = "Months",
                NamePaint = new SolidColorPaint(SKColors.Red),
                Labels = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" },
                LabelsPaint = new SolidColorPaint(SKColors.DarkGray),
                TextSize = 13,
                MinStep = 1,
            }
        };

        YAxes = new[]
        {
            new Axis
            {
                Name = "Profit",
                NamePaint = new SolidColorPaint(SKColors.Green),
                LabelsPaint = new SolidColorPaint(SKColors.DarkGray),
                TextSize = 13,
                SeparatorsPaint = new SolidColorPaint(SKColors.Gray)
                {
                    StrokeThickness = 2,
                    PathEffect = new DashEffect(new[] { 3f, 3f })
                },
                Labeler = value => Labelers.FormatCurrency(value, ",", ".", "₱"),
            }
        };
    }

    private async Task InitializeChartAsync(CancellationToken cancellationToken = default)
    {
        await UpdateChartDataAsync(cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Data Loading Methods

    public async Task LoadSalesFromDatabaseAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var salesFromDb = await _dashboardService.GetSalesAsync(5)
                .ConfigureAwait(false);

            RecentSales.Clear();
            foreach (var sale in salesFromDb)
            {
                RecentSales.Add(sale);
            }

            // Update summary using service
            var summary = await _dashboardService.GenerateSalesSummaryAsync(5)
                .ConfigureAwait(false);
            SalesSummary = summary;
            
            _logger.LogDebug("Loaded {Count} recent sales", salesFromDb.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading sales data");
        }
    }

    public async Task LoadTrainingSessionsFromDatabaseAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var sessionsFromDb = await _dashboardService.GetTrainingSessionsAsync(5)
                .ConfigureAwait(false);

            UpcomingTrainingSessions.Clear();
            foreach (var session in sessionsFromDb)
            {
                UpcomingTrainingSessions.Add(session);
            }

            // Update summary using service
            var summary = await _dashboardService.GenerateTrainingSessionsSummaryAsync(5)
                .ConfigureAwait(false);
            TrainingSessionsSummary = summary;
            
            _logger.LogDebug("Loaded {Count} training sessions", sessionsFromDb.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading training sessions data");
        }
    }

    public async Task LoadRecentLogsFromDatabaseAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var logsFromDb = await _dashboardService.GetRecentLogsAsync(5)
                .ConfigureAwait(false);

            RecentLogs.Clear();
            foreach (var log in logsFromDb)
            {
                RecentLogs.Add(log);
            }

            // Update summary using service
            var summary = await _dashboardService.GenerateRecentLogSummaryAsync(5)
                .ConfigureAwait(false);
            RecentLogsSummary = summary;
            
            _logger.LogDebug("Loaded {Count} recent logs", logsFromDb.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading recent logs data");
        }
    }

    #endregion

    #region Auto-Refresh

    private void StartAutoRefresh()
    {
        StopAutoRefresh();
        
        _autoRefreshCts = CancellationTokenSource.CreateLinkedTokenSource(LifecycleToken);
        
        // Refresh every 5 minutes
        _autoRefreshTimer = new Timer(
            async _ => await RefreshDashboardDataAsync().ConfigureAwait(false),
            null,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(5));
        
        _logger.LogDebug("Auto-refresh started (5 minute interval)");
    }

    private void StopAutoRefresh()
    {
        _autoRefreshTimer?.Dispose();
        _autoRefreshTimer = null;
        
        _autoRefreshCts?.Cancel();
        _autoRefreshCts?.Dispose();
        _autoRefreshCts = null;
        
        _logger.LogDebug("Auto-refresh stopped");
    }

    private async Task RefreshDashboardDataAsync()
    {
        if (_autoRefreshCts?.Token.IsCancellationRequested == true)
            return;

        try
        {
            await Task.WhenAll(
                LoadSalesFromDatabaseAsync(_autoRefreshCts!.Token),
                LoadTrainingSessionsFromDatabaseAsync(_autoRefreshCts.Token),
                LoadRecentLogsFromDatabaseAsync(_autoRefreshCts.Token),
                UpdateChartDataAsync(_autoRefreshCts.Token)
            ).ConfigureAwait(false);
            
            _logger.LogDebug("Dashboard data auto-refreshed");
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelled
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during auto-refresh");
        }
    }

    #endregion

    #region Event Handlers

    private async void OnRecentLogsUpdated(object? sender, EventArgs e)
    {
        try
        {
            await LoadRecentLogsFromDatabaseAsync(LifecycleToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing logs after update event");
        }
    }

    private async void OnChartDataUpdated(object? sender, EventArgs e)
    {
        try
        {
            await UpdateChartDataAsync(LifecycleToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing chart after update event");
        }
    }

    private async void OnSalesUpdated(object? sender, EventArgs e)
    {
        try
        {
            await LoadSalesFromDatabaseAsync(LifecycleToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing sales after update event");
        }
    }

    private async void OnTrainingSessionsUpdated(object? sender, EventArgs e)
    {
        try
        {
            await LoadTrainingSessionsFromDatabaseAsync(LifecycleToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing training sessions after update event");
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

    private async Task UpdateChartDataAsync(CancellationToken cancellationToken = default)
    {
        if (_selectedYearIndex >= 0 && _selectedYearIndex < AvailableYears.Count)
        {
            try
            {
                int selectedYear = AvailableYears[_selectedYearIndex];
                var data = await _dashboardService.GetSalesDataForYearAsync(selectedYear)
                    .ConfigureAwait(false);

                Series = new ISeries[]
                {
                    new ColumnSeries<int>
                    {
                        Values = data,
                        Fill = new SolidColorPaint(SKColors.DarkSlateBlue),
                        MaxBarWidth = 45,
                        Name = $"{selectedYear} Sales"
                    }
                };
                
                _logger.LogDebug("Chart updated for year {Year}", selectedYear);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading chart data");
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
        var notificationKey = _dashboardModel.GenerateNotificationKey(
            newNotification.Title, 
            newNotification.Message);
        newNotification.NotificationKey = notificationKey;
    
        if (_dashboardModel.IsNotificationAlreadyShown(notificationKey))
        {
            _logger.LogDebug("Skipping duplicate notification: {Key}", notificationKey);
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

    #region Commands

    private RelayCommand<Notification>? _deleteNotificationCommand;
    public RelayCommand<Notification> DeleteNotificationCommand =>
        _deleteNotificationCommand ??= new RelayCommand<Notification>(DeleteNotification);
    
    private RelayCommand? _clearAllNotificationsCommand;
    public RelayCommand ClearAllNotificationsCommand =>
        _clearAllNotificationsCommand ??= new RelayCommand(ClearAllNotifications);

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

    /*
    [AvaloniaHotReload]
    public void Initialize()
    {
        // Hot reload support - no-op
    }
    */

    #region PropertyChanged

    public new event PropertyChangedEventHandler? PropertyChanged;

    private new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    #endregion

    #region Disposal

    protected override async ValueTask DisposeAsyncCore()
    {
        _logger.LogInformation("Disposing DashboardViewModel");

        // Stop auto-refresh
        StopAutoRefresh();

        // Unsubscribe from events
        DashboardEventService.Instance.RecentLogsUpdated -= OnRecentLogsUpdated;
        DashboardEventService.Instance.ChartDataUpdated -= OnChartDataUpdated;
        DashboardEventService.Instance.SalesUpdated -= OnSalesUpdated;
        DashboardEventService.Instance.TrainingSessionsUpdated -= OnTrainingSessionsUpdated;

        // Clear collections
        RecentSales?.Clear();
        UpcomingTrainingSessions?.Clear();
        RecentLogs?.Clear();
        Notifications?.Clear();
        AvailableYears?.Clear();

        await base.DisposeAsyncCore().ConfigureAwait(false);
    }

    #endregion
}