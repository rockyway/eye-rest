# User Presence Detection and Meeting Detection Documentation

This document describes how the EyeRest application detects when users are away from their PC (idle/away detection) and how it detects meetings to automatically pause/resume timers.

## Table of Contents

1. [User Presence Detection System](#user-presence-detection-system)
2. [Meeting Detection System](#meeting-detection-system)
3. [Integration and Orchestration](#integration-and-orchestration)
4. [Configuration and Settings](#configuration-and-settings)

---

## User Presence Detection System

### Overview

The User Presence Detection system monitors user activity and automatically pauses/resumes eye rest and break timers when the user is away from their computer. This prevents interrupting users with break reminders when they're not at their desk.

### User Presence States

The system recognizes four distinct user presence states:

```csharp
public enum UserPresenceState
{
    Present,        // User actively using computer
    Idle,          // User inactive but session unlocked  
    Away,          // Session locked or monitor off
    SystemSleep    // System in sleep/hibernate mode
}
```

### Detection Methods

#### 1. **Idle Time Detection**
- **API Used**: `GetLastInputInfo()` Windows API
- **Threshold**: 5 minutes of inactivity
- **Method**: Monitors mouse and keyboard input activity
- **Frequency**: Checked every 15 seconds

```csharp
private const int IdleThresholdMinutes = 5; // Consider user idle after 5 minutes
```

#### 2. **Session Lock/Unlock Detection**
- **API Used**: `WTSRegisterSessionNotification()` Windows Terminal Services API
- **Events Monitored**:
  - `WTS_SESSION_LOCK` - User locks workstation (Ctrl+Alt+Del → Lock)
  - `WTS_SESSION_UNLOCK` - User unlocks workstation
  - `WTS_CONSOLE_DISCONNECT` - User switches users or RDP disconnects
  - `WTS_CONSOLE_CONNECT` - User reconnects to console

#### 3. **Monitor Power State Detection**
- **API Used**: `RegisterPowerSettingNotification()` Windows Power Management API
- **Events Monitored**:
  - Monitor turned off (sleep/power saving)
  - Monitor turned on (user returns)
- **GUID Used**: `GUID_MONITOR_POWER_ON` (02731015-4510-4526-99e6-e5a17ebd1aea)

#### 4. **Screen Saver Detection**
- **API Used**: `SystemParametersInfo()` with `SPI_GETSCREENSAVERRUNNING`
- **Purpose**: Detects when screen saver is active

#### 5. **Workstation Lock Detection**
- **API Used**: `OpenInputDesktop()` 
- **Purpose**: Fallback method to detect locked workstation

### Grace Period and Anti-Flapping

To prevent false triggers and rapid state changes:

```csharp
private const int AwayGracePeriodSeconds = 30; // Grace period before marking user as away
```

- **30-second grace period** before transitioning from Present → Away
- Prevents brief inactivity from triggering state changes
- Only applies to Away state transitions

### Auto-Pause/Resume Logic

#### When User Goes Away
```csharp
case UserPresenceState.Away:
case UserPresenceState.SystemSleep:
    if (_timerService.IsRunning && !_timerService.IsSmartPaused)
    {
        var reason = $"User {e.CurrentState.ToString().ToLower()}";
        await _timerService.SmartPauseAsync(reason);
        _systemTrayService.UpdateTrayIcon(TrayIconState.UserAway);
        _systemTrayService.UpdateTimerStatus($"Paused ({reason})");
    }
    break;
```

#### When User Returns
```csharp
case UserPresenceState.Present:
    if (_timerService.IsSmartPaused)
    {
        await _timerService.SmartResumeAsync();
        _systemTrayService.UpdateTrayIcon(TrayIconState.Active);
        _systemTrayService.UpdateTimerStatus("Running");
    }
    break;
```

---

## Meeting Detection System

### Overview

The Meeting Detection system automatically detects when users are in video conferences or meetings and pauses break timers to avoid interrupting important calls.

### Supported Meeting Applications

The system detects the following meeting platforms:

#### 1. **Microsoft Teams**
- **Process Names**: `ms-teams`, `teams`, `msteams`, `teams2`, `msteamsupdate`, `msedgewebview2`*
- **Window Title Patterns**: `Microsoft Teams`, `Teams Meeting`, `| Microsoft Teams`, `| Teams`, `Meeting in`, `Calendar | Microsoft Teams`
- **Call Indicators**: `Meeting`, `Call`, `Calling`, `In a call`, `Connected`, `Join`, `Present`, `Share`, `Mute`, `Unmute`, `People`, `Chat`
- **Note**: *`msedgewebview2` is only considered Teams when window title contains "Teams"

#### 2. **Zoom**
- **Process Names**: `zoom`, `zoomwebinar`
- **Window Title Patterns**: `Zoom Meeting`, `Zoom Webinar`, `Zoom Cloud Meetings`
- **Call Indicators**: `Meeting`, `Webinar`, `You are muted`, `Participants`

#### 3. **Cisco Webex**
- **Process Names**: `ciscowebex`, `webexmta`, `webex`
- **Window Title Patterns**: `Cisco Webex`, `Webex Meeting`, `Webex Events`
- **Call Indicators**: `Meeting`, `Mute`, `Video`, `Share Screen`

#### 4. **Google Meet**
- **Process Names**: `chrome`, `msedge`, `firefox` (browser-based)
- **Window Title Patterns**: `Google Meet`, `meet.google.com`
- **Call Indicators**: `Meet`, `Join`, `Camera`, `Microphone`
- **Special Detection**: Requires `meet.google.com` in URL

#### 5. **Skype/Skype for Business**
- **Process Names**: `skype`, `lync`
- **Window Title Patterns**: `Skype`, `Microsoft Lync`, `Skype for Business`
- **Call Indicators**: `Call`, `Video call`, `Audio call`, `Conference`

### Detection Algorithm

#### 1. **Process Scanning**
```csharp
private async Task<List<MeetingApplication>> ScanForActiveMeetingsAsync()
{
    var activeMeetings = new List<MeetingApplication>();
    var processes = Process.GetProcesses();
    
    foreach (var process in processes)
    {
        var meetingApp = AnalyzeProcess(process);
        if (meetingApp != null)
        {
            activeMeetings.Add(meetingApp);
        }
    }
    return activeMeetings;
}
```

#### 2. **Multi-Layer Validation**
For each running process, the system checks:

1. **Process Name Match**: Does the process name match known meeting applications?
2. **Window Title Analysis**: Does the window title contain meeting platform indicators?
3. **Call State Detection**: Does the window title indicate an active call/meeting?
4. **Exclusion Rules**: Is the window title in the excluded list?

#### 3. **Meeting ID Extraction**
The system attempts to extract meeting identifiers:
- **Teams**: Meeting name from window title
- **Zoom**: Meeting ID from "Meeting ID: XXXXXXXXX" pattern
- **Webex**: Meeting name after dash or pipe separator
- **Google Meet**: Meet code from URL (12 characters max)

### Monitoring and State Management

#### Monitoring Frequency
```csharp
// Default monitoring interval
private const int DefaultMonitoringIntervalSeconds = 10;
```

#### State Change Detection
```csharp
public async Task RefreshMeetingStateAsync()
{
    var activeMeetings = await ScanForActiveMeetingsAsync();
    var isMeetingActive = activeMeetings.Any();
    
    if (stateChanged)
    {
        _logger.LogInformation($"🎥 Meeting state changed: {_isMeetingActive} → {isMeetingActive}");
        
        var eventArgs = new MeetingStateEventArgs
        {
            IsMeetingActive = isMeetingActive,
            ActiveMeetings = activeMeetings,
            StateChangedAt = DateTime.Now,
            Reason = isMeetingActive ? "Meeting detected" : "Meeting ended"
        };
        
        MeetingStateChanged?.Invoke(this, eventArgs);
    }
}
```

### Auto-Pause/Resume for Meetings

#### When Meeting Starts
```csharp
if (e.IsMeetingActive && e.ActiveMeetings.Count > 0)
{
    var primaryMeeting = e.ActiveMeetings[0];
    
    if (_timerService.IsRunning && !_timerService.IsSmartPaused)
    {
        var reason = $"Meeting detected ({primaryMeeting.Type})";
        await _timerService.SmartPauseAsync(reason);
        _systemTrayService.SetMeetingMode(true, primaryMeeting.Type.ToString());
        _systemTrayService.UpdateTimerStatus($"Paused (Meeting - {primaryMeeting.Type})");
    }
}
```

#### When Meeting Ends
```csharp
else
{
    // Meeting ended - resume timers
    if (_timerService.IsSmartPaused)
    {
        await _timerService.SmartResumeAsync();
        _systemTrayService.SetMeetingMode(false);
        _systemTrayService.UpdateTimerStatus("Running");
    }
}
```

---

## Integration and Orchestration

### ApplicationOrchestrator Integration

The `ApplicationOrchestrator` class coordinates both presence detection and meeting detection:

```csharp
// Wire up advanced service events
_userPresenceService.UserPresenceChanged += OnUserPresenceChanged;
_meetingDetectionService.MeetingStateChanged += OnMeetingStateChanged;

// Start advanced services
await _userPresenceService.StartMonitoringAsync();
await _meetingDetectionService.StartMonitoringAsync();
```

### Smart Pause System

The system uses "Smart Pause" functionality that:

1. **Preserves Timer State**: Maintains current timer progress when pausing
2. **Prevents Double-Pausing**: Checks if timers are already paused before applying pause
3. **Automatic Resume**: Resumes timers when conditions clear
4. **Reason Tracking**: Records why timers were paused for analytics

### Priority Handling

When multiple conditions are present:

1. **Manual Pause**: User manual pause takes precedence
2. **Meeting Pause**: Meeting detection pauses override presence away
3. **Presence Away**: User away detection is lowest priority
4. **Resume**: Only resumes if the pause reason matches the resume trigger

### Visual Feedback

#### System Tray Icons
- **Active**: Normal timer operation
- **UserAway**: Special icon when user is away
- **MeetingMode**: Special icon during meetings
- **SmartPaused**: Indicates automatic pause

#### Tooltip Information
```csharp
// Example tooltip during meeting
"EyeRest - Meeting Mode (Teams)
Timers paused during meeting"
```

---

## Configuration and Settings

### User Presence Settings

```csharp
private const int IdleThresholdMinutes = 5; // Configurable in future versions
private const int AwayGracePeriodSeconds = 30; // Anti-flapping protection
```

### Meeting Detection Settings

```csharp
public class MeetingDetectionSettings
{
    public bool EnableTeamsDetection { get; set; } = true;
    public bool EnableZoomDetection { get; set; } = true;
    public bool EnableWebexDetection { get; set; } = true;
    public bool EnableGoogleMeetDetection { get; set; } = true;
    public bool EnableSkypeDetection { get; set; } = true;
    
    public int MonitoringIntervalSeconds { get; set; } = 10;
    
    public List<string> CustomProcessNames { get; set; } = new();
    public List<string> ExcludedWindowTitles { get; set; } = new();
}
```

### Analytics Integration

Both systems record events for analytics:

```csharp
// Presence changes
await _analyticsService.RecordPresenceChangeAsync(e.PreviousState, e.CurrentState, e.IdleDuration);

// Meeting events
await _analyticsService.RecordMeetingEventAsync(primaryMeeting, true);
```

### Error Handling and Fallbacks

#### Windows API Failures
- Graceful degradation when API calls fail
- Timer-based monitoring continues even if advanced detection fails
- Comprehensive logging for debugging

#### Process Access Issues
- Handles permission denied errors when scanning processes
- Skips inaccessible processes without breaking functionality
- Logs trace-level warnings for diagnostic purposes

#### Resource Management
- Proper cleanup of Windows API handles
- Timer disposal on service shutdown
- Memory leak prevention through proper resource management

---

## Testing and Validation

### Manual Testing Scenarios

1. **Lock workstation** → Verify timers pause
2. **Unlock workstation** → Verify timers resume
3. **Start Teams meeting** → Verify meeting mode activates
4. **End Teams meeting** → Verify meeting mode deactivates
5. **Monitor sleep** → Verify presence detection
6. **Idle timeout** → Verify idle state transition

### Logging and Diagnostics

All state changes are logged with timestamps and reasons:

```
👤 User presence changed: Present → Away
🎥 Meeting state changed - Active: True, Meetings: 1
🔒 Session locked - user marked as away
🖥️ Monitor turned off - user marked as away
```

### Performance Considerations

- **CPU Usage**: Minimal impact through efficient process scanning
- **Memory Usage**: Small footprint with proper resource cleanup
- **Network Usage**: No network calls required
- **Battery Impact**: Optimized polling intervals to minimize power consumption

This comprehensive system ensures that break reminders never interrupt users when they're away from their desk or in important meetings, while automatically resuming when they return to work.