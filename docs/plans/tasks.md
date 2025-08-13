# Implementation Plan

- [x] 1. Set up project structure and core interfaces



  - Create .NET 8 WPF project with MVVM structure
  - Set up dependency injection container with Microsoft.Extensions.DependencyInjection
  - Define core service interfaces (ITimerService, INotificationService, IConfigurationService, IAudioService, ISystemTrayService)
  - Create basic project folders: Models, ViewModels, Views, Services, Infrastructure
  - _Requirements: 7.1, 8.1, 8.2_

- [x] 2. Implement configuration system and data models



  - Create configuration data models (AppConfiguration, EyeRestSettings, BreakSettings, AudioSettings, ApplicationSettings)
  - Implement IConfigurationService with JSON serialization using System.Text.Json
  - Add configuration validation and default value handling
  - Create unit tests for configuration serialization and validation
  - _Requirements: 3.1, 3.2, 6.6_

- [x] 3. Create timer management system


  - Implement ITimerService with dual DispatcherTimer system for eye rest and break reminders
  - Add timer state management with async start/stop/reset methods
  - Implement timer event handling with proper EventArgs classes
  - Create unit tests for timer functionality using time manipulation
  - _Requirements: 1.1, 1.2, 2.1, 2.2_

- [x] 4. Build notification popup system


  - Create base popup window class with full-screen overlay capabilities
  - Implement eye rest reminder popup with cartoon character display
  - Create break reminder popup with progress bar animation and action buttons
  - Add multi-monitor support for popup positioning
  - _Requirements: 1.2, 2.2, 2.4, 6.4, 6.5_

- [x] 5. Implement audio notification system


  - Create IAudioService implementation with system sound playback
  - Add support for custom audio files and volume control
  - Implement audio enable/disable functionality based on settings
  - Create unit tests with mocked audio dependencies
  - _Requirements: 1.3, 3.1_

- [x] 6. Create main application window and settings UI


  - Design main settings window XAML with eye rest and break configuration sections
  - Implement settings ViewModel with INotifyPropertyChanged and data binding
  - Add save/cancel/restore defaults functionality
  - Create input validation for numeric settings fields
  - _Requirements: 3.1, 3.2, 6.3, 6.5_

- [x] 7. Implement system tray integration


  - Create ISystemTrayService with NotifyIcon implementation
  - Add system tray context menu with Open App and Exit options
  - Implement minimize to tray behavior and window restoration
  - Add tray icon state management for different application states
  - _Requirements: 4.1, 4.2_

- [x] 8. Add stretching resources integration

  - Implement stretching resource buttons in break popup
  - Add functionality to open external websites in default browser
  - Create configuration for stretching resource URLs
  - Test browser integration and error handling
  - _Requirements: 2.5_

- [x] 9. Implement application lifecycle and startup optimization


  - Create application startup sequence with lazy service initialization
  - Add Windows startup integration (optional setting)
  - Implement proper application shutdown and resource disposal
  - Optimize startup time to meet <3 second requirement
  - _Requirements: 7.1, 7.2_

- [x] 10. Add comprehensive error handling and logging


  - Implement ILoggingService with file-based logging
  - Add global exception handling for WPF application
  - Create error recovery mechanisms for timer and popup failures
  - Add logging for user actions and system events
  - _Requirements: 7.7, 7.8, 7.9_

- [x] 11. Create break warning system

  - Implement pre-break warning popup with 30-second countdown
  - Add vertical progress bar animation for warning display
  - Integrate warning system with main break timer
  - Test warning timing and user experience
  - _Requirements: 2.2_

- [x] 12. Implement break delay and skip functionality

  - Add delay buttons (1 minute, 5 minutes) to break popup
  - Implement skip functionality with proper timer reset
  - Add user action logging for analytics
  - Create unit tests for delay and skip scenarios
  - _Requirements: 2.4, 2.7_

- [x] 13. Add visual design elements and theming


  - Create application icon and integrate across all UI elements
  - Design and implement cartoon character graphics for eye rest reminders
  - Add visual feedback (green screen) for break completion
  - Ensure high DPI support and multi-monitor compatibility
  - _Requirements: 5.1, 5.2, 6.1, 6.2, 6.4, 6.5_

- [x] 14. Implement memory and performance optimizations


  - Add weak event handlers to prevent memory leaks
  - Implement popup window reuse instead of recreation
  - Optimize resource loading and disposal patterns
  - Verify memory usage stays under 50MB requirement
  - _Requirements: 7.1, 7.2, 7.3_

- [x] 15. Create comprehensive test suite


  - Write unit tests for all service implementations
  - Create integration tests for timer and notification workflows
  - Add performance tests for startup time and memory usage
  - Test multi-monitor scenarios and edge cases
  - _Requirements: 7.7, 7.8, 7.9, 6.4_

- [x] 16. Final integration and polish



  - Integrate all components through dependency injection
  - Test complete user workflows from startup to shutdown
  - Verify all requirements are met through end-to-end testing
  - Add final error handling and user experience improvements
  - _Requirements: All requirements verification_

- [ ] 17. Add application icon to title bar and system tray

  - Set application icon in MainWindow title bar
  - Ensure icon appears in system tray and taskbar
  - Update app.manifest and project settings for icon display
  - Test icon visibility across different Windows versions
  - _Requirements: 6.1_

- [ ] 18. Implement timer status indicator in title bar

  - Add status indicator to MainWindow showing timer state
  - Display "Running" status with green indicator when timers are active
  - Display "Stopped" status with red indicator when timers are inactive
  - Update status indicator in real-time based on timer service state
  - _Requirements: 6.6, 6.7, 6.8_

- [ ] 19. Create eye rest warning popup system

  - Implement eye rest warning popup with 30-second countdown
  - Add visual progress indicator for warning countdown
  - Create smooth transition from warning to full eye rest reminder
  - Add configuration options for warning enable/disable and duration
  - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6_

- [ ] 20. Update timer service for eye rest warnings

  - Add EyeRestWarning event to ITimerService interface
  - Implement warning timer logic with configurable duration
  - Update timer service to trigger warnings before eye rest reminders
  - Add IsRunning property to track timer state for status indicator
  - _Requirements: 9.1, 9.4, 6.7, 6.8_

- [ ] 21. Update configuration models for new features

  - Add WarningEnabled and WarningSeconds to EyeRestSettings
  - Update configuration service to handle new eye rest warning settings
  - Add validation for warning duration settings
  - Update settings UI to include eye rest warning configuration
  - _Requirements: 9.5, 9.6_

- [x] 22. Fix duplicate system tray icons issue

  - Remove duplicate TrayService registration from App.xaml.cs
  - Keep only SystemTrayService as the single tray icon provider
  - Ensure proper cleanup and disposal of tray resources
  - Test single tray icon functionality with minimize/restore behavior
  - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5_

- [x] 23. Implement real-time countdown timer display

  - Add countdown properties to ITimerService interface (TimeUntilNextEyeRest, TimeUntilNextBreak)
  - Enhance TimerService with real-time countdown calculations and formatting
  - Update MainWindowViewModel with countdown display properties
  - Add countdown timer UI elements to MainWindow.xaml with visibility binding
  - Implement UpdateCountdown method in MainWindow.xaml.cs
  - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 10.6_

- [x] 24. Ensure automatic timer startup

  - Verify timer auto-start functionality in App.xaml.cs background task
  - Test that timers begin running immediately when application opens
  - Ensure countdown displays show immediately upon startup
  - Validate proper integration with ApplicationOrchestrator initialization
  - _Requirements: 10.6, 7.1_

- [ ] 25. Test and integrate all new features

  - Test icon display across different Windows versions and DPI settings
  - Verify status indicator updates correctly with timer state changes
  - Test countdown timer accuracy and real-time updates
  - Verify single tray icon behavior with minimize/restore/exit functions
  - Test eye rest warning system with various configuration settings
  - Perform end-to-end testing of complete warning and reminder flow
  - _Requirements: 6.1, 6.6, 6.7, 6.8, 9.1-9.6, 10.1-10.6, 11.1-11.5_