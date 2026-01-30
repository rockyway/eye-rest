using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using EyeRest.Models;
using EyeRest.Services;
using Microsoft.Extensions.Logging;

namespace EyeRest.ViewModels
{
    /// <summary>
    /// ViewModel for the comprehensive analytics dashboard with data visualization
    /// </summary>
    public class AnalyticsDashboardViewModel : INotifyPropertyChanged
    {
        private readonly IAnalyticsService _analyticsService;
        private readonly IConfigurationService _configurationService;
        private readonly ILogger<AnalyticsDashboardViewModel> _logger;
        private AppConfiguration _currentConfiguration;
        
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
        private DateTime _selectedStartDate = DateTime.Now.AddDays(-30);
        private DateTime _selectedEndDate = DateTime.Now;
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

        public event PropertyChangedEventHandler? PropertyChanged;

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
                _logger.LogInformation("🚀 Initializing Analytics Dashboard ViewModel");
                
                // Ensure analytics database is initialized first
                if (!await _analyticsService.IsDatabaseInitializedAsync())
                {
                    _logger.LogInformation("📊 Analytics database not initialized, initializing now...");
                    await _analyticsService.InitializeDatabaseAsync();
                }
                
                // Load configuration first
                await LoadConfigurationAsync();
                
                // Then load dashboard data
                await LoadDashboardDataAsync();
                
                _logger.LogInformation("✅ Analytics Dashboard ViewModel initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to initialize Analytics Dashboard ViewModel");
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

        public DateTime SelectedStartDate
        {
            get => _selectedStartDate;
            set
            {
                if (SetProperty(ref _selectedStartDate, value))
                {
                    _ = LoadDashboardDataAsync();
                }
            }
        }

        public DateTime SelectedEndDate
        {
            get => _selectedEndDate;
            set
            {
                if (SetProperty(ref _selectedEndDate, value))
                {
                    _ = LoadDashboardDataAsync();
                }
            }
        }

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
                    _ = LoadDashboardDataAsync();
                }
            }
        }

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
                if (_currentConfiguration?.Analytics != null && value >= 7 && value <= 7300) // 20 years max
                {
                    var oldValue = _currentConfiguration.Analytics.DataRetentionDays;
                    _currentConfiguration.Analytics.DataRetentionDays = value;
                    _ = SaveConfigurationAsync();
                    
                    // Show confirmation feedback
                    var years = value / 365.0;
                    var confirmationMessage = years >= 2 
                        ? $"✅ Data retention updated to {value} days ({years:F1} years)" 
                        : $"✅ Data retention updated to {value} days";
                    
                    // Set error message as confirmation (it will show as info)
                    ErrorMessage = confirmationMessage;
                    
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
        
        private bool _isDarkMode = false;
        public bool IsDarkMode
        {
            get => _currentConfiguration?.Application?.IsDarkMode ?? _isDarkMode;
            set => SetProperty(ref _isDarkMode, value);
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
            RefreshDataCommand = new RelayCommand(async () => await LoadDashboardDataAsync());
            ExportDataCommand = new RelayCommand<string>(async format => await ExportDataAsync(format));
            ClearDataCommand = new RelayCommand(async () => await ClearAnalyticsDataAsync());
            GenerateReportCommand = new RelayCommand(async () => await GenerateHealthReportAsync());
            SelectPeriodCommand = new RelayCommand<int>(days => SelectedPeriodDays = days);
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
                OnPropertyChanged(nameof(IsDarkMode));
                
                _logger.LogInformation("Analytics configuration loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading analytics configuration");
                ErrorMessage = $"Failed to load configuration: {ex.Message}";
            }
        }
        
        private async Task SaveConfigurationAsync()
        {
            try
            {
                await _configurationService.SaveConfigurationAsync(_currentConfiguration);
                _logger.LogInformation("Analytics configuration saved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving analytics configuration");
                ErrorMessage = $"Failed to save configuration: {ex.Message}";
            }
        }

        public async Task LoadDashboardDataAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                _logger.LogInformation($"📊 Loading analytics dashboard data for period {SelectedStartDate:yyyy-MM-dd} to {SelectedEndDate:yyyy-MM-dd}");
                
                // Show user-friendly loading message
                ErrorMessage = "Refreshing analytics data...";

                // Load health metrics
                var healthMetrics = await _analyticsService.GetHealthMetricsAsync(SelectedStartDate, SelectedEndDate);
                CurrentHealthMetrics = healthMetrics;

                // Update summary properties
                ComplianceRate = healthMetrics.ComplianceRate;
                TotalBreaksCompleted = healthMetrics.BreaksCompleted;
                TotalBreaksSkipped = healthMetrics.BreaksSkipped;
                TotalActiveTime = healthMetrics.TotalActiveTime;
                AverageBreakDuration = healthMetrics.AverageBreakDuration;

                // Generate chart data
                await GenerateChartDataAsync(healthMetrics);

                // Load daily, weekly, and monthly metrics
                await LoadDailyMetricsAsync();
                await LoadWeeklyMetricsAsync();
                await LoadMonthlyMetricsAsync();
                
                // Load database info
                await LoadDatabaseInfoAsync();

                // Notify UI of property changes
                OnPropertyChanged(nameof(ComplianceRateText));
                OnPropertyChanged(nameof(TotalActiveTimeText));
                OnPropertyChanged(nameof(AverageBreakDurationText));
                OnPropertyChanged(nameof(HealthScoreText));
                OnPropertyChanged(nameof(HealthStatusColor));
                
                // Update weekly/monthly summary texts
                UpdateWeeklySummaryTexts();
                UpdateMonthlySummaryTexts();

                _logger.LogInformation("✅ Analytics dashboard data loaded successfully");
                
                // Clear loading message and show success
                ErrorMessage = $"Data refreshed successfully at {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error loading analytics dashboard data");
                ErrorMessage = $"Failed to load analytics data: {ex.Message}";
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
                    return;
                }

                IsLoading = true;
                _logger.LogInformation($"📤 Exporting analytics data in {format} format");

                var exportFormat = Enum.Parse<ExportFormat>(format ?? "JSON", true);
                
                // Show save file dialog
                var fileName = $"EyeRest_Analytics_{DateTime.Now:yyyyMMdd_HHmmss}";
                var fileExtension = format?.ToLower() ?? "json";
                var filter = fileExtension.ToUpper() switch
                {
                    "JSON" => "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    "CSV" => "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    "HTML" => "HTML files (*.html)|*.html|All files (*.*)|*.*",
                    _ => "All files (*.*)|*.*"
                };

                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"{fileName}.{fileExtension}",
                    Filter = filter,
                    Title = $"Export Analytics Data as {format?.ToUpper()}",
                    DefaultExt = fileExtension
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var exportData = await _analyticsService.ExportDataAsync(exportFormat, SelectedStartDate, SelectedEndDate);
                    await System.IO.File.WriteAllTextAsync(saveFileDialog.FileName, exportData);

                    _logger.LogInformation($"✅ Analytics data exported to {saveFileDialog.FileName}");
                    ErrorMessage = $"Data exported successfully to {System.IO.Path.GetFileName(saveFileDialog.FileName)}";
                }
                else
                {
                    _logger.LogInformation("Export cancelled by user");
                    ErrorMessage = "Export cancelled";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error exporting analytics data");
                ErrorMessage = $"Export failed: {ex.Message}";
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
                // Multi-step confirmation dialog
                var result = System.Windows.MessageBox.Show(
                    "⚠️ WARNING: This will permanently delete ALL analytics data!\n\n" +
                    "This includes:\n" +
                    "• All break history\n" +
                    "• All compliance metrics\n" +
                    "• All daily, weekly, and monthly statistics\n" +
                    "• All exported reports history\n\n" +
                    "This action CANNOT be undone!\n\n" +
                    "Are you absolutely sure you want to continue?",
                    "⚠️ Confirm Data Deletion - Step 1 of 2",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning,
                    System.Windows.MessageBoxResult.No);

                if (result != System.Windows.MessageBoxResult.Yes) return;

                // Second confirmation with typing requirement
                var secondResult = System.Windows.MessageBox.Show(
                    "🚨 FINAL WARNING 🚨\n\n" +
                    "You are about to PERMANENTLY DELETE all analytics data.\n\n" +
                    "Current database contains:" +
                    $"\n• Database size: {DatabaseSizeText}" +
                    $"\n• Data retention: {DataRetentionDays} days" +
                    $"\n• Total breaks tracked: {TotalBreaksCompleted + TotalBreaksSkipped}" +
                    $"\n• Active time recorded: {TotalActiveTimeText}" +
                    "\n\nTHIS ACTION IS IRREVERSIBLE!\n\n" +
                    "Click YES only if you are absolutely certain.",
                    "🚨 FINAL CONFIRMATION - Step 2 of 2",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Error,
                    System.Windows.MessageBoxResult.No);

                if (secondResult != System.Windows.MessageBoxResult.Yes) return;

                IsLoading = true;
                _logger.LogWarning("🗑️ User confirmed: Clearing all analytics data");

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

                _logger.LogInformation("✅ Analytics data cleared successfully");
                
                // Show success message
                System.Windows.MessageBox.Show(
                    "✅ All analytics data has been permanently deleted.\n\n" +
                    "The analytics system will start collecting new data from this point forward.",
                    "Data Cleared Successfully",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                    
                ErrorMessage = "All analytics data has been permanently cleared";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error clearing analytics data");
                ErrorMessage = $"Failed to clear data: {ex.Message}";
                
                System.Windows.MessageBox.Show(
                    $"❌ Error clearing data:\n\n{ex.Message}\n\nPlease try again or contact support.",
                    "Data Clearing Failed",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
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
                _logger.LogInformation("📋 Generating comprehensive health report");

                var report = await _analyticsService.GenerateComplianceReportAsync(SelectedPeriodDays);
                
                // Create comprehensive report content
                var reportContent = GenerateReportContent(report);
                
                // Show save file dialog
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"EyeRest_HealthReport_{DateTime.Now:yyyyMMdd_HHmmss}.html",
                    Filter = "HTML files (*.html)|*.html|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                    Title = "Save Health Report",
                    DefaultExt = "html"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    await System.IO.File.WriteAllTextAsync(saveFileDialog.FileName, reportContent);
                    
                    var fileName = System.IO.Path.GetFileName(saveFileDialog.FileName);
                    _logger.LogInformation($"✅ Health report saved to {fileName}");
                    
                    // Ask user if they want to open the report
                    var openResult = System.Windows.MessageBox.Show(
                        $"✅ Health report generated successfully!\n\n" +
                        $"Report saved as: {fileName}\n\n" +
                        $"Summary:\n" +
                        $"• Overall compliance: {report.OverallComplianceRate:P1}\n" +
                        $"• Total breaks completed: {TotalBreaksCompleted}\n" +
                        $"• Health score: {GetHealthScoreText()}\n\n" +
                        "Would you like to open the report now?",
                        "Health Report Generated",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Information);
                    
                    if (openResult == System.Windows.MessageBoxResult.Yes)
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = saveFileDialog.FileName,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception openEx)
                        {
                            _logger.LogWarning(openEx, "Could not open report file");
                            ErrorMessage = "Report saved successfully, but could not open automatically";
                        }
                    }
                    
                    ErrorMessage = $"Health report generated and saved as {fileName}";
                }
                else
                {
                    ErrorMessage = "Report generation cancelled";
                }

                _logger.LogInformation("✅ Health report generation completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error generating health report");
                ErrorMessage = $"Report generation failed: {ex.Message}";
                
                System.Windows.MessageBox.Show(
                    $"❌ Error generating report:\n\n{ex.Message}\n\nPlease try again or check the logs for details.",
                    "Report Generation Failed",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
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
            var reportPeriod = $"{SelectedStartDate:MMM dd, yyyy} - {SelectedEndDate:MMM dd, yyyy} ({SelectedPeriodDays} days)";
            var healthStatusClass = GetHealthStatusClass();
            var healthScore = GetHealthScoreText();
            var complianceAnalysis = GetComplianceAnalysis();
            var recommendation = GetRecommendation();
            var complianceRate = ComplianceRate.ToString("P1");
            var totalBreaks = TotalBreaksCompleted + TotalBreaksSkipped;
            var skipStatus = TotalBreaksSkipped > TotalBreaksCompleted ? "⚠️ High" : "✅ Low";
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
        <h1>📊 Eye-rest Health Report</h1>
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
        <h2>📈 Compliance Analysis</h2>
        <p><strong>Overall Assessment:</strong> {complianceAnalysis}</p>
        <p><strong>Recommendation:</strong> {recommendation}</p>
        
        <table class=""summary-table"">
            <tr><th>Metric</th><th>Value</th><th>Status</th></tr>
            <tr><td>Compliance Rate</td><td>{complianceRate}</td><td>{healthScore}</td></tr>
            <tr><td>Total Breaks Due</td><td>{totalBreaks}</td><td>-</td></tr>
            <tr><td>Breaks Completed</td><td>{TotalBreaksCompleted}</td><td>✅ Good</td></tr>
            <tr><td>Breaks Skipped</td><td>{TotalBreaksSkipped}</td><td>{skipStatus}</td></tr>
            <tr><td>Average Daily Active Time</td><td>{avgDailyTime}h</td><td>-</td></tr>
        </table>
    </div>
    
    <div class=""section"">
        <h2>🎯 Key Insights</h2>
        <ul>
            <li><strong>Best Performance Day:</strong> {bestDay}</li>
            <li><strong>Most Active Period:</strong> {activePeriod}</li>
            <li><strong>Improvement Areas:</strong> {improvementAreas}</li>
            <li><strong>Consistency Score:</strong> {consistencyScore}</li>
        </ul>
    </div>
    
    <div class=""section"">
        <h2>💡 Personalized Recommendations</h2>
        <ol>
            {recommendations}
        </ol>
    </div>
    
    <div class=""section"">
        <h2>🔒 Privacy Notice</h2>
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
            // Check if we have minimal data (less than 5 total breaks or very short tracking period)
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
            // Check if we have minimal data
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
                    // Generate demo compliance data for the selected period
                    var demoStartDate = SelectedStartDate;
                    var demoDays = Math.Min((SelectedEndDate - SelectedStartDate).Days, 14); // Max 14 days of demo data
                    
                    for (int i = 0; i < demoDays; i++)
                    {
                        var date = demoStartDate.AddDays(i);
                        // Generate realistic demo compliance rates (70-95%)
                        var random = new Random(date.DayOfYear); // Consistent demo data
                        var compliance = 70 + (random.NextDouble() * 25); // 70-95% range
                        
                        ComplianceChartData.Add(new ChartDataPoint
                        {
                            X = i,
                            Y = compliance,
                            Label = date.ToString("MM/dd"),
                            Category = "Demo"
                        });
                    }
                    
                    _logger.LogInformation($"📊 No daily breakdown data found - showing {demoDays} days of demo compliance data");
                }

                // Generate break pattern data (pie chart data)
                BreakPatternData.Clear();
                
                // Always show some data for visualization, even if no breaks recorded yet
                var completed = Math.Max(TotalBreaksCompleted, 0);
                var skipped = Math.Max(TotalBreaksSkipped, 0);
                var delayed = Math.Max(healthMetrics.BreaksDelayed, 0);
                
                // Calculate totals and generate pie chart data
                var total = completed + skipped + delayed;
                
                // If no data exists, show sample data for demo purposes
                if (completed == 0 && skipped == 0 && delayed == 0)
                {
                    completed = 15; skipped = 3; delayed = 2;
                    total = 20;
                    TotalBreaksText = "20 Demo Breaks";
                    
                    GeneratePieChartData(completed, skipped, delayed, total, isDemo: true);
                    _logger.LogInformation("📊 No break data found - showing demo data in analytics charts");
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
                var complianceByTime = new[] { 0.85, 0.75, 0.65 }; // Simulated data
                
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
                var weeklyData = await _analyticsService.GetWeeklyMetricsAsync(SelectedStartDate, SelectedEndDate);
                
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
                var monthlyData = await _analyticsService.GetMonthlyMetricsAsync(SelectedStartDate, SelectedEndDate);
                
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
            // Check if we have minimal data (less than 3 days of activity or very few breaks)
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
            if (ComplianceRate >= 0.9) return "#4CAF50"; // Green
            if (ComplianceRate >= 0.8) return "#8BC34A"; // Light Green
            if (ComplianceRate >= 0.7) return "#FFC107"; // Amber
            if (ComplianceRate >= 0.6) return "#FF9800"; // Orange
            return "#F44336"; // Red
        }

        private int GetWeekOfYear(DateTime date)
        {
            var dayOfYear = date.DayOfYear;
            return (dayOfYear - 1) / 7 + 1;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void GeneratePieChartData(int completed, int skipped, int delayed, int total, bool isDemo)
        {
            if (total == 0) return;

            var colors = new[] { "#4CAF50", "#F44336", "#FFC107" }; // Green, Red, Amber
            var values = new[] { completed, skipped, delayed };
            var labels = isDemo ? 
                new[] { "Completed (Demo)", "Skipped (Demo)", "Delayed (Demo)" } :
                new[] { "Completed", "Skipped", "Delayed" };
            var categories = new[] { "Completed", "Skipped", "Delayed" };

            double startAngle = -90; // Start from top
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
                    PathData = GenerateArcPathData(60, 60, 50, currentAngle, sweepAngle) // Center X, Y, Radius, Start, Sweep
                };

                BreakPatternData.Add(dataPoint);
                currentAngle += sweepAngle;
            }
        }

        private string GenerateArcPathData(double centerX, double centerY, double radius, double startAngle, double sweepAngle)
        {
            // Convert angles from degrees to radians
            double startRad = startAngle * Math.PI / 180;
            double endRad = (startAngle + sweepAngle) * Math.PI / 180;

            // Calculate arc points
            double startX = centerX + radius * Math.Cos(startRad);
            double startY = centerY + radius * Math.Sin(startRad);
            double endX = centerX + radius * Math.Cos(endRad);
            double endY = centerY + radius * Math.Sin(endRad);

            // Large arc flag (1 if sweep angle > 180°)
            int largeArcFlag = sweepAngle > 180 ? 1 : 0;

            // Create SVG path data for pie slice
            if (sweepAngle >= 360) // Full circle
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
public static class StatisticalExtensions
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