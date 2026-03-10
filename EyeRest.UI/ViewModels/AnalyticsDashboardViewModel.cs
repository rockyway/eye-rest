using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using EyeRest.Models;
using EyeRest.Services;
using EyeRest.ViewModels;
using Microsoft.Extensions.Logging;

namespace EyeRest.UI.ViewModels
{
    /// <summary>
    /// ViewModel for the comprehensive analytics dashboard with data visualization.
    /// Cross-platform Avalonia port - no WPF dependencies.
    /// </summary>
    public class AnalyticsDashboardViewModel : ViewModelBase
    {
        private readonly IAnalyticsService _analyticsService;
        private readonly IConfigurationService _configurationService;
        private readonly ILogger<AnalyticsDashboardViewModel> _logger;
        private AppConfiguration _currentConfiguration;
        private CancellationTokenSource? _loadCts;

        // Health Metrics Properties
        private HealthMetrics? _currentHealthMetrics;
        private double _complianceRate;
        private int _totalBreaksCompleted;
        private int _totalBreaksSkipped;
        private TimeSpan _totalActiveTime;
        private TimeSpan _averageBreakDuration;

        // Dashboard State Properties
        private bool _isLoading;
        private string _errorMessage = string.Empty;
        private DateTime? _selectedStartDate = DateTime.Now.AddDays(-30);
        private DateTime? _selectedEndDate = DateTime.Now;
        private int _selectedPeriodDays = 30;

        // Chart Data Properties
        private ObservableCollection<ChartDataPoint> _complianceChartData = new();
        private ObservableCollection<ChartDataPoint> _breakPatternData = new();
        private ObservableCollection<ChartDataPoint> _weeklyTrendData = new();
        private ObservableCollection<ChartDataPoint> _timeOfDayData = new();
        private string _totalBreaksText = "No Data";

        // Daily, Weekly, Monthly Metrics Properties
        private ObservableCollection<DailyMetricViewModel> _dailyMetrics = new();
        private ObservableCollection<WeeklyMetrics> _weeklyMetrics = new();
        private ObservableCollection<MonthlyMetrics> _monthlyMetrics = new();

        // Export and Privacy Properties
        private bool _allowDataExport = true;
        private bool _enableAnalytics = true;
        private int _dataRetentionDays = 90;

        // Weekly/Monthly Summary Properties
        private string _currentWeekComplianceText = "--";
        private string _lastWeekComplianceText = "--";
        private string _bestWeekComplianceText = "--";
        private string _weeklyAverageComplianceText = "--";
        private string _currentWeekStatusColor = "#666666";

        private string _currentMonthComplianceText = "--";
        private string _lastMonthComplianceText = "--";
        private string _bestMonthComplianceText = "--";
        private string _monthlyAverageComplianceText = "--";
        private string _currentMonthStatusColor = "#666666";

        // Database info properties
        private string _databasePath = "";
        private string _databaseSizeText = "--";

        // Compliance display properties
        private double _complianceRateValue;
        private string _completedBreaksPercentageText = "";
        private string _skippedBreaksPercentageText = "";
        private string _dailyAverageActiveTimeText = "";
        private string _healthStatusText = "";

        public AnalyticsDashboardViewModel(
            IAnalyticsService analyticsService,
            IConfigurationService configurationService,
            ILogger<AnalyticsDashboardViewModel> logger)
        {
            _analyticsService = analyticsService;
            _configurationService = configurationService;
            _logger = logger;

            // Initialize with default configuration
            _currentConfiguration = new AppConfiguration();

            InitializeCommands();

            // Initialize database info with default values
            DatabasePath = "Loading...";
            DatabaseSizeText = "Calculating...";

            // Initialize data asynchronously with proper error handling
            _ = InitializeViewModelAsync();
        }

        private async Task InitializeViewModelAsync()
        {
            try
            {
                _logger.LogInformation("Initializing Analytics Dashboard ViewModel");

                // Ensure analytics database is initialized first
                if (!await _analyticsService.IsDatabaseInitializedAsync())
                {
                    _logger.LogInformation("Analytics database not initialized, initializing now...");
                    await _analyticsService.InitializeDatabaseAsync();
                }

                // Load configuration first
                await LoadConfigurationAsync();

                // Then load dashboard data
                await LoadDashboardDataAsync();

                _logger.LogInformation("Analytics Dashboard ViewModel initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Analytics Dashboard ViewModel");
                ErrorMessage = "Failed to initialize analytics dashboard. Please check the logs for details.";

                // Set safe defaults to prevent further errors
                ComplianceRate = 0;
                TotalBreaksCompleted = 0;
                TotalBreaksSkipped = 0;
                TotalActiveTime = TimeSpan.Zero;
                AverageBreakDuration = TimeSpan.Zero;
            }
        }

        #region Properties

        public HealthMetrics? CurrentHealthMetrics
        {
            get => _currentHealthMetrics;
            set => SetProperty(ref _currentHealthMetrics, value);
        }

        public double ComplianceRate
        {
            get => _complianceRate;
            set => SetProperty(ref _complianceRate, value);
        }

        public int TotalBreaksCompleted
        {
            get => _totalBreaksCompleted;
            set => SetProperty(ref _totalBreaksCompleted, value);
        }

        public int TotalBreaksSkipped
        {
            get => _totalBreaksSkipped;
            set => SetProperty(ref _totalBreaksSkipped, value);
        }

        public TimeSpan TotalActiveTime
        {
            get => _totalActiveTime;
            set => SetProperty(ref _totalActiveTime, value);
        }

        public TimeSpan AverageBreakDuration
        {
            get => _averageBreakDuration;
            set => SetProperty(ref _averageBreakDuration, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public bool HasErrorMessage => !string.IsNullOrEmpty(ErrorMessage);

        public DateTime? SelectedStartDate
        {
            get => _selectedStartDate;
            set
            {
                if (SetProperty(ref _selectedStartDate, value))
                {
                    OnPropertyChanged(nameof(SelectedStartDateText));
                    _ = LoadDashboardDataAsync();
                }
            }
        }

        public DateTime? SelectedEndDate
        {
            get => _selectedEndDate;
            set
            {
                if (SetProperty(ref _selectedEndDate, value))
                {
                    OnPropertyChanged(nameof(SelectedEndDateText));
                    _ = LoadDashboardDataAsync();
                }
            }
        }

        public string SelectedStartDateText => _selectedStartDate?.ToString("MMM dd, yyyy") ?? "—";
        public string SelectedEndDateText => _selectedEndDate?.ToString("MMM dd, yyyy") ?? "—";

        public int SelectedPeriodDays
        {
            get => _selectedPeriodDays;
            set
            {
                if (SetProperty(ref _selectedPeriodDays, value))
                {
                    _selectedStartDate = DateTime.Now.AddDays(-value);
                    _selectedEndDate = DateTime.Now;
                    OnPropertyChanged(nameof(SelectedStartDate));
                    OnPropertyChanged(nameof(SelectedEndDate));
                    OnPropertyChanged(nameof(SelectedStartDateText));
                    OnPropertyChanged(nameof(SelectedEndDateText));
                    _ = LoadDashboardDataAsync();
                }
            }
        }

        // Helper properties with null fallback for service calls
        private DateTime StartDateValue => _selectedStartDate ?? DateTime.Now.AddDays(-30);
        private DateTime EndDateValue => _selectedEndDate ?? DateTime.Now;

        public ObservableCollection<ChartDataPoint> ComplianceChartData
        {
            get => _complianceChartData;
            set => SetProperty(ref _complianceChartData, value);
        }

        public ObservableCollection<ChartDataPoint> BreakPatternData
        {
            get => _breakPatternData;
            set => SetProperty(ref _breakPatternData, value);
        }

        public ObservableCollection<ChartDataPoint> WeeklyTrendData
        {
            get => _weeklyTrendData;
            set => SetProperty(ref _weeklyTrendData, value);
        }

        public ObservableCollection<ChartDataPoint> TimeOfDayData
        {
            get => _timeOfDayData;
            set => SetProperty(ref _timeOfDayData, value);
        }

        public ObservableCollection<DailyMetricViewModel> DailyMetrics
        {
            get => _dailyMetrics;
            set => SetProperty(ref _dailyMetrics, value);
        }

        public ObservableCollection<WeeklyMetrics> WeeklyMetrics
        {
            get => _weeklyMetrics;
            set => SetProperty(ref _weeklyMetrics, value);
        }

        public ObservableCollection<MonthlyMetrics> MonthlyMetrics
        {
            get => _monthlyMetrics;
            set => SetProperty(ref _monthlyMetrics, value);
        }

        public string TotalBreaksText
        {
            get => _totalBreaksText;
            set => SetProperty(ref _totalBreaksText, value);
        }

        public bool AllowDataExport
        {
            get => _currentConfiguration?.Analytics?.AllowDataExport ?? _allowDataExport;
            set
            {
                if (_currentConfiguration?.Analytics != null)
                {
                    _currentConfiguration.Analytics.AllowDataExport = value;
                    _ = SaveConfigurationAsync();
                }
                SetProperty(ref _allowDataExport, value);
            }
        }

        public bool EnableAnalytics
        {
            get => _currentConfiguration?.Analytics?.Enabled ?? _enableAnalytics;
            set
            {
                if (_currentConfiguration?.Analytics != null)
                {
                    _currentConfiguration.Analytics.Enabled = value;
                    _ = SaveConfigurationAsync();
                }
                SetProperty(ref _enableAnalytics, value);
            }
        }

        public int DataRetentionDays
        {
            get => _currentConfiguration?.Analytics?.DataRetentionDays ?? _dataRetentionDays;
            set
            {
                if (_currentConfiguration?.Analytics != null && value >= 7 && value <= 7300)
                {
                    var oldValue = _currentConfiguration.Analytics.DataRetentionDays;
                    _currentConfiguration.Analytics.DataRetentionDays = value;
                    _ = SaveConfigurationAsync();

                    var years = value / 365.0;
                    var confirmationMessage = years >= 2
                        ? $"Data retention updated to {value} days ({years:F1} years)"
                        : $"Data retention updated to {value} days";

                    ErrorMessage = confirmationMessage;
                    OnPropertyChanged(nameof(HasErrorMessage));

                    _logger.LogInformation($"Data retention changed from {oldValue} to {value} days");
                }
                SetProperty(ref _dataRetentionDays, value);
            }
        }

        // Weekly Summary Properties
        public string CurrentWeekComplianceText
        {
            get => _currentWeekComplianceText;
            set => SetProperty(ref _currentWeekComplianceText, value);
        }

        public string LastWeekComplianceText
        {
            get => _lastWeekComplianceText;
            set => SetProperty(ref _lastWeekComplianceText, value);
        }

        public string BestWeekComplianceText
        {
            get => _bestWeekComplianceText;
            set => SetProperty(ref _bestWeekComplianceText, value);
        }

        public string WeeklyAverageComplianceText
        {
            get => _weeklyAverageComplianceText;
            set => SetProperty(ref _weeklyAverageComplianceText, value);
        }

        public string CurrentWeekStatusColor
        {
            get => _currentWeekStatusColor;
            set => SetProperty(ref _currentWeekStatusColor, value);
        }

        // Monthly Summary Properties
        public string CurrentMonthComplianceText
        {
            get => _currentMonthComplianceText;
            set => SetProperty(ref _currentMonthComplianceText, value);
        }

        public string LastMonthComplianceText
        {
            get => _lastMonthComplianceText;
            set => SetProperty(ref _lastMonthComplianceText, value);
        }

        public string BestMonthComplianceText
        {
            get => _bestMonthComplianceText;
            set => SetProperty(ref _bestMonthComplianceText, value);
        }

        public string MonthlyAverageComplianceText
        {
            get => _monthlyAverageComplianceText;
            set => SetProperty(ref _monthlyAverageComplianceText, value);
        }

        public string CurrentMonthStatusColor
        {
            get => _currentMonthStatusColor;
            set => SetProperty(ref _currentMonthStatusColor, value);
        }

        // Database Info Properties
        public string DatabasePath
        {
            get => _databasePath;
            set => SetProperty(ref _databasePath, value);
        }

        public string DatabaseSizeText
        {
            get => _databaseSizeText;
            set => SetProperty(ref _databaseSizeText, value);
        }

        public double ComplianceRateValue
        {
            get => _complianceRateValue;
            set => SetProperty(ref _complianceRateValue, value);
        }

        public string CompletedBreaksPercentageText
        {
            get => _completedBreaksPercentageText;
            set => SetProperty(ref _completedBreaksPercentageText, value);
        }

        public string SkippedBreaksPercentageText
        {
            get => _skippedBreaksPercentageText;
            set => SetProperty(ref _skippedBreaksPercentageText, value);
        }

        public string DailyAverageActiveTimeText
        {
            get => _dailyAverageActiveTimeText;
            set => SetProperty(ref _dailyAverageActiveTimeText, value);
        }

        public string HealthStatusText
        {
            get => _healthStatusText;
            set => SetProperty(ref _healthStatusText, value);
        }

        // Formatted Display Properties
        public string ComplianceRateText => $"{ComplianceRate:P1}";
        public string TotalActiveTimeText => $"{TotalActiveTime.TotalHours:F1}h";
        public string AverageBreakDurationText => $"{AverageBreakDuration.TotalMinutes:F1}min";
        public string HealthScoreText => GetHealthScoreText();
        public string HealthStatusColor => GetHealthStatusColor();

        // Period Selection Options
        public List<PeriodOption> PeriodOptions { get; } = new()
        {
            new PeriodOption { Days = 7, DisplayName = "Last 7 Days" },
            new PeriodOption { Days = 14, DisplayName = "Last 14 Days" },
            new PeriodOption { Days = 30, DisplayName = "Last 30 Days" },
            new PeriodOption { Days = 60, DisplayName = "Last 60 Days" },
            new PeriodOption { Days = 90, DisplayName = "Last 90 Days" }
        };

        #endregion

        #region Commands

        public ICommand RefreshDataCommand { get; private set; } = null!;
        public ICommand ExportDataCommand { get; private set; } = null!;
        public ICommand ClearDataCommand { get; private set; } = null!;
        public ICommand GenerateReportCommand { get; private set; } = null!;
        public ICommand SelectPeriodCommand { get; private set; } = null!;

        private void InitializeCommands()
        {
            RefreshDataCommand = new CrossPlatformRelayCommand(async () => await LoadDashboardDataAsync());
            ExportDataCommand = new CrossPlatformRelayCommand(
                param => { _ = ExportDataAsync(param?.ToString()); });
            ClearDataCommand = new CrossPlatformRelayCommand(async () => await ClearAnalyticsDataAsync());
            GenerateReportCommand = new CrossPlatformRelayCommand(async () => await GenerateHealthReportAsync());
            SelectPeriodCommand = new CrossPlatformRelayCommand(
                param => { if (param is int days) SelectedPeriodDays = days; });
        }

        #endregion

        #region Public Methods

        public async Task LoadConfigurationAsync()
        {
            try
            {
                _currentConfiguration = await _configurationService.LoadConfigurationAsync();

                // Update local properties to trigger UI updates
                OnPropertyChanged(nameof(EnableAnalytics));
                OnPropertyChanged(nameof(AllowDataExport));
                OnPropertyChanged(nameof(DataRetentionDays));

                _logger.LogInformation("Analytics configuration loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading analytics configuration");
                ErrorMessage = $"Failed to load configuration: {ex.Message}";
                OnPropertyChanged(nameof(HasErrorMessage));
            }
        }

        private async Task SaveConfigurationAsync()
        {
            try
            {
                await _configurationService.UpdateConfigurationAsync(config =>
                {
                    if (_currentConfiguration?.Analytics != null)
                    {
                        config.Analytics.Enabled = _currentConfiguration.Analytics.Enabled;
                        config.Analytics.AllowDataExport = _currentConfiguration.Analytics.AllowDataExport;
                        config.Analytics.DataRetentionDays = _currentConfiguration.Analytics.DataRetentionDays;
                    }
                });
                _logger.LogInformation("Analytics configuration saved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving analytics configuration");
                ErrorMessage = $"Failed to save configuration: {ex.Message}";
                OnPropertyChanged(nameof(HasErrorMessage));
            }
        }

        public async Task LoadDashboardDataAsync()
        {
            // Cancel any previous in-flight load
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;
                OnPropertyChanged(nameof(HasErrorMessage));

                _logger.LogInformation($"Loading analytics dashboard data for period {StartDateValue:yyyy-MM-dd} to {EndDateValue:yyyy-MM-dd}");

                ErrorMessage = "Refreshing analytics data...";
                OnPropertyChanged(nameof(HasErrorMessage));

                // Load health metrics
                var healthMetrics = await _analyticsService.GetHealthMetricsAsync(StartDateValue, EndDateValue);
                token.ThrowIfCancellationRequested();

                CurrentHealthMetrics = healthMetrics;

                // Update summary properties
                ComplianceRate = healthMetrics.ComplianceRate;
                TotalBreaksCompleted = healthMetrics.BreaksCompleted;
                TotalBreaksSkipped = healthMetrics.BreaksSkipped;
                TotalActiveTime = healthMetrics.TotalActiveTime;
                AverageBreakDuration = healthMetrics.AverageBreakDuration;

                // Update computed display values
                ComplianceRateValue = ComplianceRate * 100;
                var totalBreaks = TotalBreaksCompleted + TotalBreaksSkipped;
                CompletedBreaksPercentageText = totalBreaks > 0
                    ? $"{(double)TotalBreaksCompleted / totalBreaks:P0} of total"
                    : "No data";
                SkippedBreaksPercentageText = totalBreaks > 0
                    ? $"{(double)TotalBreaksSkipped / totalBreaks:P0} of total"
                    : "No data";
                DailyAverageActiveTimeText = SelectedPeriodDays > 0
                    ? $"~{TotalActiveTime.TotalHours / SelectedPeriodDays:F1}h/day avg"
                    : "";
                HealthStatusText = GetHealthScoreText();

                // Generate chart data
                await GenerateChartDataAsync(healthMetrics);
                token.ThrowIfCancellationRequested();

                // Load daily, weekly, and monthly metrics
                await LoadDailyMetricsAsync();
                token.ThrowIfCancellationRequested();

                await LoadWeeklyMetricsAsync();
                token.ThrowIfCancellationRequested();

                await LoadMonthlyMetricsAsync();
                token.ThrowIfCancellationRequested();

                // Load database info
                await LoadDatabaseInfoAsync();
                token.ThrowIfCancellationRequested();

                // Notify UI of property changes
                OnPropertyChanged(nameof(ComplianceRateText));
                OnPropertyChanged(nameof(TotalActiveTimeText));
                OnPropertyChanged(nameof(AverageBreakDurationText));
                OnPropertyChanged(nameof(HealthScoreText));
                OnPropertyChanged(nameof(HealthStatusColor));

                // Update weekly/monthly summary texts
                UpdateWeeklySummaryTexts();
                UpdateMonthlySummaryTexts();

                _logger.LogInformation("Analytics dashboard data loaded successfully");

                ErrorMessage = $"Data refreshed successfully at {DateTime.Now:HH:mm:ss}";
                OnPropertyChanged(nameof(HasErrorMessage));
            }
            catch (OperationCanceledException)
            {
                // Previous load was cancelled by a newer one — expected behavior
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading analytics dashboard data");
                ErrorMessage = $"Failed to load analytics data: {ex.Message}";
                OnPropertyChanged(nameof(HasErrorMessage));
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task ExportDataAsync(string? format)
        {
            try
            {
                if (!AllowDataExport)
                {
                    ErrorMessage = "Data export is disabled in privacy settings";
                    OnPropertyChanged(nameof(HasErrorMessage));
                    return;
                }

                IsLoading = true;
                _logger.LogInformation($"Exporting analytics data in {format} format");

                var exportFormat = Enum.Parse<ExportFormat>(format ?? "JSON", true);

                // Generate the export data
                var exportData = await _analyticsService.ExportDataAsync(exportFormat, StartDateValue, EndDateValue);

                // Save to a default location (cross-platform - no file dialog dependency)
                var fileExtension = format?.ToLower() ?? "json";
                var fileName = $"EyeRest_Analytics_{DateTime.Now:yyyyMMdd_HHmmss}.{fileExtension}";
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var filePath = Path.Combine(documentsPath, fileName);

                await File.WriteAllTextAsync(filePath, exportData);

                _logger.LogInformation($"Analytics data exported to {filePath}");
                ErrorMessage = $"Data exported to {filePath}";
                OnPropertyChanged(nameof(HasErrorMessage));

                // Try to open the containing folder
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = documentsPath,
                        UseShellExecute = true
                    });
                }
                catch (Exception openEx)
                {
                    _logger.LogWarning(openEx, "Could not open export folder");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting analytics data");
                ErrorMessage = $"Export failed: {ex.Message}";
                OnPropertyChanged(nameof(HasErrorMessage));
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task ClearAnalyticsDataAsync()
        {
            try
            {
                // In cross-platform mode, proceed with clearing (confirmation is handled by the view)
                IsLoading = true;
                _logger.LogWarning("User confirmed: Clearing all analytics data");

                await _analyticsService.DeleteAllDataAsync();

                // Clear local data immediately
                ComplianceRate = 0;
                TotalBreaksCompleted = 0;
                TotalBreaksSkipped = 0;
                TotalActiveTime = TimeSpan.Zero;
                AverageBreakDuration = TimeSpan.Zero;

                DailyMetrics.Clear();
                WeeklyMetrics.Clear();
                MonthlyMetrics.Clear();
                ComplianceChartData.Clear();
                BreakPatternData.Clear();
                WeeklyTrendData.Clear();
                TimeOfDayData.Clear();

                // Reload fresh data
                await LoadDashboardDataAsync();

                _logger.LogInformation("Analytics data cleared successfully");
                ErrorMessage = "All analytics data has been permanently cleared";
                OnPropertyChanged(nameof(HasErrorMessage));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing analytics data");
                ErrorMessage = $"Failed to clear data: {ex.Message}";
                OnPropertyChanged(nameof(HasErrorMessage));
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task GenerateHealthReportAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = "Generating comprehensive health report...";
                OnPropertyChanged(nameof(HasErrorMessage));
                _logger.LogInformation("Generating comprehensive health report");

                var report = await _analyticsService.GenerateComplianceReportAsync(SelectedPeriodDays);

                // Create comprehensive report content
                var reportContent = GenerateReportContent(report);

                // Save to documents folder (cross-platform)
                var fileName = $"EyeRest_HealthReport_{DateTime.Now:yyyyMMdd_HHmmss}.html";
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var filePath = Path.Combine(documentsPath, fileName);

                await File.WriteAllTextAsync(filePath, reportContent);

                _logger.LogInformation($"Health report saved to {filePath}");
                ErrorMessage = $"Health report generated and saved to {filePath}";
                OnPropertyChanged(nameof(HasErrorMessage));

                // Try to open the report
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                }
                catch (Exception openEx)
                {
                    _logger.LogWarning(openEx, "Could not open report file");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating health report");
                ErrorMessage = $"Report generation failed: {ex.Message}";
                OnPropertyChanged(nameof(HasErrorMessage));
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region Private Methods

        private string GenerateReportContent(ComplianceReport report)
        {
            var reportId = Guid.NewGuid().ToString("N")[..8].ToUpper();
            var generatedOn = DateTime.Now.ToString("MMMM dd, yyyy 'at' HH:mm:ss");
            var reportPeriod = $"{StartDateValue:MMM dd, yyyy} - {EndDateValue:MMM dd, yyyy} ({SelectedPeriodDays} days)";
            var healthStatusClass = GetHealthStatusClass();
            var healthScore = GetHealthScoreText();
            var complianceAnalysis = GetComplianceAnalysis();
            var recommendation = GetRecommendation();
            var complianceRate = ComplianceRate.ToString("P1");
            var totalBreaks = TotalBreaksCompleted + TotalBreaksSkipped;
            var skipStatus = TotalBreaksSkipped > TotalBreaksCompleted ? "High" : "Low";
            var avgDailyTime = (TotalActiveTime.TotalHours / Math.Max(SelectedPeriodDays, 1)).ToString("F1");
            var bestDay = GetBestPerformanceDay();
            var activePeriod = GetMostActivePeriod();
            var improvementAreas = GetImprovementAreas();
            var consistencyScore = GetConsistencyScore();
            var recommendations = GetPersonalizedRecommendations();
            var analyticsEnabled = EnableAnalytics ? "Yes" : "No";
            var exportAllowed = AllowDataExport ? "Yes" : "No";

            var html = @$"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Eye-rest Health Report - {DateTime.Now:yyyy-MM-dd}</title>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 40px; background: #f8f9fa; }}
        .header {{ background: #0078d4; color: white; padding: 30px; border-radius: 8px; margin-bottom: 30px; }}
        .header h1 {{ margin: 0; font-size: 2.5em; }}
        .header .subtitle {{ opacity: 0.9; margin-top: 10px; font-size: 1.1em; }}
        .metrics-grid {{ display: grid; grid-template-columns: repeat(auto-fit, minmax(250px, 1fr)); gap: 20px; margin-bottom: 30px; }}
        .metric-card {{ background: white; padding: 25px; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        .metric-value {{ font-size: 2.5em; font-weight: bold; margin-bottom: 10px; }}
        .metric-label {{ color: #666; font-size: 1.1em; }}
        .excellent {{ color: #4CAF50; }}
        .good {{ color: #8BC34A; }}
        .fair {{ color: #FFC107; }}
        .poor {{ color: #F44336; }}
        .section {{ background: white; padding: 25px; margin-bottom: 20px; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        .section h2 {{ color: #0078d4; border-bottom: 2px solid #e9ecef; padding-bottom: 10px; }}
        .summary-table {{ width: 100%; border-collapse: collapse; margin-top: 15px; }}
        .summary-table th, .summary-table td {{ padding: 12px; text-align: left; border-bottom: 1px solid #e9ecef; }}
        .summary-table th {{ background: #f8f9fa; font-weight: 600; }}
        .footer {{ text-align: center; margin-top: 40px; color: #666; font-size: 0.9em; }}
        .health-score {{ text-align: center; font-size: 3em; font-weight: bold; padding: 20px; border-radius: 8px; margin: 20px 0; }}
    </style>
</head>
<body>
    <div class=""header"">
        <h1>Eye-rest Health Report</h1>
        <div class=""subtitle"">Generated on {generatedOn}</div>
        <div class=""subtitle"">Report Period: {reportPeriod}</div>
    </div>

    <div class=""health-score {healthStatusClass}"">
        Overall Health Score: {healthScore}
    </div>

    <div class=""metrics-grid"">
        <div class=""metric-card"">
            <div class=""metric-value {healthStatusClass}"">{complianceRate}</div>
            <div class=""metric-label"">Compliance Rate</div>
        </div>
        <div class=""metric-card"">
            <div class=""metric-value excellent"">{TotalBreaksCompleted}</div>
            <div class=""metric-label"">Breaks Completed</div>
        </div>
        <div class=""metric-card"">
            <div class=""metric-value poor"">{TotalBreaksSkipped}</div>
            <div class=""metric-label"">Breaks Skipped</div>
        </div>
        <div class=""metric-card"">
            <div class=""metric-value good"">{TotalActiveTimeText}</div>
            <div class=""metric-label"">Total Active Time</div>
        </div>
    </div>

    <div class=""section"">
        <h2>Compliance Analysis</h2>
        <p><strong>Overall Assessment:</strong> {complianceAnalysis}</p>
        <p><strong>Recommendation:</strong> {recommendation}</p>

        <table class=""summary-table"">
            <tr><th>Metric</th><th>Value</th><th>Status</th></tr>
            <tr><td>Compliance Rate</td><td>{complianceRate}</td><td>{healthScore}</td></tr>
            <tr><td>Total Breaks Due</td><td>{totalBreaks}</td><td>-</td></tr>
            <tr><td>Breaks Completed</td><td>{TotalBreaksCompleted}</td><td>Good</td></tr>
            <tr><td>Breaks Skipped</td><td>{TotalBreaksSkipped}</td><td>{skipStatus}</td></tr>
            <tr><td>Average Daily Active Time</td><td>{avgDailyTime}h</td><td>-</td></tr>
        </table>
    </div>

    <div class=""section"">
        <h2>Key Insights</h2>
        <ul>
            <li><strong>Best Performance Day:</strong> {bestDay}</li>
            <li><strong>Most Active Period:</strong> {activePeriod}</li>
            <li><strong>Improvement Areas:</strong> {improvementAreas}</li>
            <li><strong>Consistency Score:</strong> {consistencyScore}</li>
        </ul>
    </div>

    <div class=""section"">
        <h2>Personalized Recommendations</h2>
        <ol>
            {recommendations}
        </ol>
    </div>

    <div class=""section"">
        <h2>Privacy Notice</h2>
        <p><strong>Data Protection:</strong> This report contains only your personal analytics data, stored locally on your device. No data is transmitted to external servers or shared with third parties.</p>
        <p><strong>Report Details:</strong></p>
        <ul>
            <li>Database Size: {DatabaseSizeText}</li>
            <li>Data Retention: {DataRetentionDays} days</li>
            <li>Analytics Enabled: {analyticsEnabled}</li>
            <li>Export Allowed: {exportAllowed}</li>
        </ul>
    </div>

    <div class=""footer"">
        <p>Generated by Eye-rest v1.0 | Report ID: {reportId}</p>
        <p>For questions about this report, please refer to the application documentation.</p>
    </div>
</body>
</html>";

            return html;
        }

        private string GetHealthStatusClass()
        {
            if (ComplianceRate >= 0.9) return "excellent";
            if (ComplianceRate >= 0.8) return "good";
            if (ComplianceRate >= 0.7) return "fair";
            return "poor";
        }

        private string GetComplianceAnalysis()
        {
            var totalBreaks = TotalBreaksCompleted + TotalBreaksSkipped;
            var hasMinimalData = totalBreaks < 5 || SelectedPeriodDays < 3 || TotalActiveTime.TotalHours < 2;

            if (hasMinimalData)
            {
                return "Welcome to Eye-rest! You're just getting started - keep using the app to track your progress and build healthy break habits.";
            }

            if (ComplianceRate >= 0.9) return "Excellent compliance rate! You're doing great at taking regular breaks.";
            if (ComplianceRate >= 0.8) return "Good compliance rate with room for minor improvement.";
            if (ComplianceRate >= 0.7) return "Fair compliance rate. Consider setting more reminders to improve your routine.";
            if (ComplianceRate >= 0.6) return "You're building good habits! Focus on taking more regular breaks to improve your eye health.";
            return "You're learning to use Eye-rest! Try taking more breaks - your eyes will thank you as you develop this healthy routine.";
        }

        private string GetRecommendation()
        {
            var totalBreaks = TotalBreaksCompleted + TotalBreaksSkipped;
            var hasMinimalData = totalBreaks < 5 || SelectedPeriodDays < 3 || TotalActiveTime.TotalHours < 2;

            if (hasMinimalData)
            {
                return "Continue using Eye-rest regularly to establish a healthy break routine. Every break you take is a step towards better eye health!";
            }

            if (ComplianceRate >= 0.9) return "Keep up the excellent work! Continue your current break routine.";
            if (ComplianceRate >= 0.8) return "Try to reduce break skipping by 10-15% for optimal eye health.";
            if (ComplianceRate >= 0.7) return "Set more frequent reminders and try shorter, more frequent breaks.";
            if (ComplianceRate >= 0.6) return "Consider adjusting break intervals and using audio reminders to build consistency.";
            return "Focus on establishing a regular break routine. Start with shorter breaks and gradually build the habit - you're on the right track!";
        }

        private string GetBestPerformanceDay()
        {
            var bestDay = DailyMetrics.OrderByDescending(d => d.ComplianceRate).FirstOrDefault();
            return bestDay != null ? $"{bestDay.DateText} ({bestDay.ComplianceRateText} compliance)" : "No data available";
        }

        private string GetMostActivePeriod()
        {
            return TotalActiveTime.TotalHours > 8 ? "High activity periods" : "Moderate activity periods";
        }

        private string GetImprovementAreas()
        {
            var areas = new List<string>();
            if (TotalBreaksSkipped > TotalBreaksCompleted) areas.Add("Reduce break skipping");
            if (ComplianceRate < 0.8) areas.Add("Increase break compliance");
            if (TotalActiveTime.TotalHours / SelectedPeriodDays > 10) areas.Add("Reduce daily screen time");
            return areas.Any() ? string.Join(", ", areas) : "Maintain current excellent habits";
        }

        private string GetConsistencyScore()
        {
            var variance = DailyMetrics.Any() ? DailyMetrics.Select(d => d.ComplianceRate).StandardDeviation() : 0;
            if (variance < 0.1) return "Excellent (Very consistent)";
            if (variance < 0.2) return "Good (Mostly consistent)";
            if (variance < 0.3) return "Fair (Somewhat inconsistent)";
            return "Poor (Highly inconsistent)";
        }

        private string GetPersonalizedRecommendations()
        {
            var recommendations = new List<string>();

            if (ComplianceRate < 0.8)
                recommendations.Add("<li>Enable audio notifications to reduce missed breaks</li>");

            if (TotalBreaksSkipped > TotalBreaksCompleted)
                recommendations.Add("<li>Try shorter break durations (15-30 seconds) to reduce skipping</li>");

            if (TotalActiveTime.TotalHours / SelectedPeriodDays > 8)
                recommendations.Add("<li>Consider the 20-20-20 rule: Every 20 minutes, look at something 20 feet away for 20 seconds</li>");

            if (WeeklyMetrics.Any() && WeeklyMetrics.Take(2).All(w => w.ComplianceRate < 0.7))
                recommendations.Add("<li>Set calendar reminders for break times to build consistency</li>");

            recommendations.Add("<li>Consider adjusting break intervals based on your work patterns</li>");
            recommendations.Add("<li>Use the analytics data to identify your most productive break times</li>");

            return string.Join("\n            ", recommendations);
        }

        private async Task GenerateChartDataAsync(HealthMetrics healthMetrics)
        {
            try
            {
                // Generate compliance trend chart data
                ComplianceChartData.Clear();
                if (healthMetrics.DailyBreakdown != null && healthMetrics.DailyBreakdown.Count > 0)
                {
                    for (int i = 0; i < healthMetrics.DailyBreakdown.Count; i++)
                    {
                        var daily = healthMetrics.DailyBreakdown[i];
                        ComplianceChartData.Add(new ChartDataPoint
                        {
                            X = i,
                            Y = daily.ComplianceRate * 100,
                            Label = daily.Date.ToString("MM/dd"),
                            Category = "Compliance"
                        });
                    }
                }
                else
                {
                    var demoStartDate = StartDateValue;
                    var demoDays = Math.Min((EndDateValue - StartDateValue).Days, 14);

                    for (int i = 0; i < demoDays; i++)
                    {
                        var date = demoStartDate.AddDays(i);
                        var random = new Random(date.DayOfYear);
                        var compliance = 70 + (random.NextDouble() * 25);

                        ComplianceChartData.Add(new ChartDataPoint
                        {
                            X = i,
                            Y = compliance,
                            Label = date.ToString("MM/dd"),
                            Category = "Demo"
                        });
                    }

                    _logger.LogInformation($"No daily breakdown data found - showing {demoDays} days of demo compliance data");
                }

                // Generate break pattern data (pie chart data)
                BreakPatternData.Clear();

                var completed = Math.Max(TotalBreaksCompleted, 0);
                var skipped = Math.Max(TotalBreaksSkipped, 0);
                var delayed = Math.Max(healthMetrics.BreaksDelayed, 0);

                var total = completed + skipped + delayed;

                if (completed == 0 && skipped == 0 && delayed == 0)
                {
                    completed = 15; skipped = 3; delayed = 2;
                    total = 20;
                    TotalBreaksText = "20 Demo Breaks";

                    GeneratePieChartData(completed, skipped, delayed, total, isDemo: true);
                    _logger.LogInformation("No break data found - showing demo data in analytics charts");
                }
                else
                {
                    TotalBreaksText = $"{total} Total Breaks";
                    GeneratePieChartData(completed, skipped, delayed, total, isDemo: false);
                }

                // Generate weekly trend data
                WeeklyTrendData.Clear();
                if (healthMetrics.DailyBreakdown != null)
                {
                    var weeklyGroups = healthMetrics.DailyBreakdown
                        .GroupBy(d => GetWeekOfYear(d.Date))
                        .ToList();

                    for (int i = 0; i < weeklyGroups.Count; i++)
                    {
                        var week = weeklyGroups[i];
                        var avgCompliance = week.Average(d => d.ComplianceRate);
                        WeeklyTrendData.Add(new ChartDataPoint
                        {
                            X = i,
                            Y = avgCompliance * 100,
                            Label = $"Week {week.Key}",
                            Category = "Weekly"
                        });
                    }
                }

                // Generate time-of-day data (simulated for now)
                TimeOfDayData.Clear();
                var timeSlots = new[] { "Morning", "Afternoon", "Evening" };
                var complianceByTime = new[] { 0.85, 0.75, 0.65 };

                for (int i = 0; i < timeSlots.Length; i++)
                {
                    TimeOfDayData.Add(new ChartDataPoint
                    {
                        X = i,
                        Y = complianceByTime[i] * 100,
                        Label = timeSlots[i],
                        Category = "TimeOfDay"
                    });
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating chart data");
            }
        }

        private async Task LoadDailyMetricsAsync()
        {
            try
            {
                DailyMetrics.Clear();

                if (CurrentHealthMetrics?.DailyBreakdown != null)
                {
                    foreach (var daily in CurrentHealthMetrics.DailyBreakdown.OrderByDescending(d => d.Date))
                    {
                        DailyMetrics.Add(new DailyMetricViewModel
                        {
                            Date = daily.Date,
                            BreaksDue = daily.BreaksDue,
                            BreaksCompleted = daily.BreaksCompleted,
                            BreaksSkipped = daily.BreaksSkipped,
                            ComplianceRate = daily.ComplianceRate,
                            TotalBreakTime = daily.TotalBreakTime,
                            EyeRestsCompleted = daily.EyeRestsCompleted
                        });
                    }

                    // Free the duplicate daily breakdown data now that DailyMetrics is populated.
                    // Chart data has already been generated from DailyBreakdown in GenerateChartDataAsync,
                    // so this list is no longer needed and can be released to reduce memory usage.
                    CurrentHealthMetrics.DailyBreakdown = null!;
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading daily metrics");
            }
        }

        private async Task LoadWeeklyMetricsAsync()
        {
            try
            {
                WeeklyMetrics.Clear();
                var weeklyData = await _analyticsService.GetWeeklyMetricsAsync(StartDateValue, EndDateValue);

                foreach (var weekly in weeklyData)
                {
                    WeeklyMetrics.Add(weekly);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading weekly metrics");
            }
        }

        private async Task LoadMonthlyMetricsAsync()
        {
            try
            {
                MonthlyMetrics.Clear();
                var monthlyData = await _analyticsService.GetMonthlyMetricsAsync(StartDateValue, EndDateValue);

                foreach (var monthly in monthlyData)
                {
                    MonthlyMetrics.Add(monthly);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading monthly metrics");
            }
        }

        private async Task LoadDatabaseInfoAsync()
        {
            try
            {
                DatabasePath = _analyticsService.GetDatabasePath();
                var sizeBytes = await _analyticsService.GetDatabaseSizeAsync();

                if (sizeBytes > 1024 * 1024)
                {
                    DatabaseSizeText = $"{sizeBytes / (1024.0 * 1024.0):F2} MB";
                }
                else if (sizeBytes > 1024)
                {
                    DatabaseSizeText = $"{sizeBytes / 1024.0:F1} KB";
                }
                else
                {
                    DatabaseSizeText = $"{sizeBytes} bytes";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading database info");
                DatabaseSizeText = "Unknown";
            }
        }

        private void UpdateWeeklySummaryTexts()
        {
            try
            {
                if (WeeklyMetrics.Count == 0)
                {
                    CurrentWeekComplianceText = "--";
                    LastWeekComplianceText = "--";
                    BestWeekComplianceText = "--";
                    WeeklyAverageComplianceText = "--";
                    CurrentWeekStatusColor = "#666666";
                    return;
                }

                var currentWeek = WeeklyMetrics.FirstOrDefault();
                if (currentWeek != null)
                {
                    CurrentWeekComplianceText = $"{currentWeek.ComplianceRate:P0}";
                    CurrentWeekStatusColor = currentWeek.ComplianceStatusColor;
                }

                var lastWeek = WeeklyMetrics.Skip(1).FirstOrDefault();
                if (lastWeek != null)
                {
                    LastWeekComplianceText = $"{lastWeek.ComplianceRate:P0}";
                }

                var bestWeek = WeeklyMetrics.OrderByDescending(w => w.ComplianceRate).FirstOrDefault();
                if (bestWeek != null)
                {
                    BestWeekComplianceText = $"{bestWeek.ComplianceRate:P0}";
                }

                var averageCompliance = WeeklyMetrics.Average(w => w.ComplianceRate);
                WeeklyAverageComplianceText = $"{averageCompliance:P0}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating weekly summary texts");
            }
        }

        private void UpdateMonthlySummaryTexts()
        {
            try
            {
                if (MonthlyMetrics.Count == 0)
                {
                    CurrentMonthComplianceText = "--";
                    LastMonthComplianceText = "--";
                    BestMonthComplianceText = "--";
                    MonthlyAverageComplianceText = "--";
                    CurrentMonthStatusColor = "#666666";
                    return;
                }

                var currentMonth = MonthlyMetrics.FirstOrDefault();
                if (currentMonth != null)
                {
                    CurrentMonthComplianceText = $"{currentMonth.ComplianceRate:P0}";
                    CurrentMonthStatusColor = currentMonth.ComplianceStatusColor;
                }

                var lastMonth = MonthlyMetrics.Skip(1).FirstOrDefault();
                if (lastMonth != null)
                {
                    LastMonthComplianceText = $"{lastMonth.ComplianceRate:P0}";
                }

                var bestMonth = MonthlyMetrics.OrderByDescending(m => m.ComplianceRate).FirstOrDefault();
                if (bestMonth != null)
                {
                    BestMonthComplianceText = $"{bestMonth.ComplianceRate:P0}";
                }

                var averageCompliance = MonthlyMetrics.Average(m => m.ComplianceRate);
                MonthlyAverageComplianceText = $"{averageCompliance:P0}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating monthly summary texts");
            }
        }

        private string GetHealthScoreText()
        {
            var totalBreaks = TotalBreaksCompleted + TotalBreaksSkipped;
            var hasMinimalData = totalBreaks < 5 || SelectedPeriodDays < 3 || TotalActiveTime.TotalHours < 2;

            if (hasMinimalData)
            {
                return "Getting Started";
            }

            if (ComplianceRate >= 0.9) return "Excellent";
            if (ComplianceRate >= 0.8) return "Good";
            if (ComplianceRate >= 0.7) return "Fair";
            if (ComplianceRate >= 0.6) return "Improving";
            return "Building Habits";
        }

        private string GetHealthStatusColor()
        {
            if (ComplianceRate >= 0.9) return "#4CAF50";
            if (ComplianceRate >= 0.8) return "#8BC34A";
            if (ComplianceRate >= 0.7) return "#FFC107";
            if (ComplianceRate >= 0.6) return "#FF9800";
            return "#F44336";
        }

        private int GetWeekOfYear(DateTime date)
        {
            var dayOfYear = date.DayOfYear;
            return (dayOfYear - 1) / 7 + 1;
        }

        private void GeneratePieChartData(int completed, int skipped, int delayed, int total, bool isDemo)
        {
            if (total == 0) return;

            var colors = new[] { "#4CAF50", "#F44336", "#FFC107" };
            var values = new[] { completed, skipped, delayed };
            var labels = isDemo ?
                new[] { "Completed (Demo)", "Skipped (Demo)", "Delayed (Demo)" } :
                new[] { "Completed", "Skipped", "Delayed" };
            var categories = new[] { "Completed", "Skipped", "Delayed" };

            double startAngle = -90;
            double currentAngle = startAngle;

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == 0) continue;

                double percentage = (double)values[i] / total;
                double sweepAngle = percentage * 360;

                var dataPoint = new ChartDataPoint
                {
                    Y = values[i],
                    Label = labels[i],
                    Category = categories[i],
                    Color = colors[i],
                    Tooltip = $"{labels[i]}: {values[i]} ({percentage:P0})",
                    PathData = GenerateArcPathData(60, 60, 50, currentAngle, sweepAngle)
                };

                BreakPatternData.Add(dataPoint);
                currentAngle += sweepAngle;
            }
        }

        private string GenerateArcPathData(double centerX, double centerY, double radius, double startAngle, double sweepAngle)
        {
            double startRad = startAngle * Math.PI / 180;
            double endRad = (startAngle + sweepAngle) * Math.PI / 180;

            double startX = centerX + radius * Math.Cos(startRad);
            double startY = centerY + radius * Math.Sin(startRad);
            double endX = centerX + radius * Math.Cos(endRad);
            double endY = centerY + radius * Math.Sin(endRad);

            int largeArcFlag = sweepAngle > 180 ? 1 : 0;

            if (sweepAngle >= 360)
            {
                return $"M {centerX - radius},{centerY} A {radius},{radius} 0 1,1 {centerX + radius},{centerY} A {radius},{radius} 0 1,1 {centerX - radius},{centerY} Z";
            }
            else
            {
                return $"M {centerX},{centerY} L {startX:F2},{startY:F2} A {radius},{radius} 0 {largeArcFlag},1 {endX:F2},{endY:F2} Z";
            }
        }

        #endregion
    }

    #region Helper Classes

    public class ChartDataPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public string Label { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string PathData { get; set; } = string.Empty;
        public string Tooltip { get; set; } = string.Empty;
    }

    public class DailyMetricViewModel
    {
        public DateTime Date { get; set; }
        public int BreaksDue { get; set; }
        public int BreaksCompleted { get; set; }
        public int BreaksSkipped { get; set; }
        public int EyeRestsCompleted { get; set; }
        public double ComplianceRate { get; set; }
        public TimeSpan TotalBreakTime { get; set; }

        public string DateText => Date.ToString("MM/dd/yyyy");
        public string ComplianceRateText => $"{ComplianceRate:P0}";
        public string TotalBreakTimeText => $"{TotalBreakTime.TotalMinutes:F0}min";
        public string ComplianceStatusColor => ComplianceRate >= 0.8 ? "#4CAF50" : ComplianceRate >= 0.6 ? "#FFC107" : "#F44336";
    }

    public class PeriodOption
    {
        public int Days { get; set; }
        public string DisplayName { get; set; } = string.Empty;

        public override string ToString() => DisplayName;
    }

    #endregion
}

/// <summary>
/// Extension methods for statistical calculations
/// </summary>
public static class AnalyticsStatisticalExtensions
{
    public static double StandardDeviation(this IEnumerable<double> values)
    {
        var enumerable = values as double[] ?? values.ToArray();
        if (!enumerable.Any()) return 0;

        var average = enumerable.Average();
        var sum = enumerable.Sum(v => Math.Pow(v - average, 2));
        return Math.Sqrt(sum / enumerable.Length);
    }
}
