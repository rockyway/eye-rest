# Eye-rest Application - Default Timer Values

## 📋 **Default Configuration Values**

When you start the Eye-rest application for the first time, these are the default timer values that should appear in the settings interface:

### 👁️ **Eye Rest Settings**
- **Remind every**: `20` minutes
- **Rest for**: `20` seconds  
- **Play sound at start**: ✅ **Enabled** (checked)
- **Play sound at end**: ✅ **Enabled** (checked)

### ☕ **Break Time Settings**
- **Work for**: `55` minutes
- **Break for**: `5` minutes
- **Show warning before break**: ✅ **Enabled** (checked)
- **Warning time**: `30` seconds

### 🔊 **Audio Settings**
- **Enable audio notifications**: ✅ **Enabled** (checked)
- **Volume**: `50` (slider position at 50%)
- **Custom sound**: *(empty/blank)*

### ⚙️ **Application Settings**
- **Start with Windows**: ❌ **Disabled** (unchecked)
- **Minimize to system tray**: ✅ **Enabled** (checked)
- **Show in taskbar**: ❌ **Disabled** (unchecked)

## 🔄 **Configuration Loading Process**

1. **First Launch**: If no config file exists, `GetDefaultConfiguration()` creates these values
2. **Subsequent Launches**: Values are loaded from `%APPDATA%/EyeRest/config.json`
3. **UI Binding**: `MainWindowViewModel.UpdatePropertiesFromConfiguration()` sets the UI controls

## 📍 **Where Values Are Defined**

### Primary Source: `Models/AppConfiguration.cs`
```csharp
public class EyeRestSettings
{
    public int IntervalMinutes { get; set; } = 20;        // 20 minutes
    public int DurationSeconds { get; set; } = 20;        // 20 seconds
    public bool StartSoundEnabled { get; set; } = true;   // Enabled
    public bool EndSoundEnabled { get; set; } = true;     // Enabled
}

public class BreakSettings
{
    public int IntervalMinutes { get; set; } = 55;        // 55 minutes
    public int DurationMinutes { get; set; } = 5;         // 5 minutes
    public bool WarningEnabled { get; set; } = true;      // Enabled
    public int WarningSeconds { get; set; } = 30;         // 30 seconds
}
```

### Backup Source: `Services/ConfigurationService.GetDefaultConfiguration()`
This method creates the same values programmatically if needed.

## 🎯 **Expected Behavior**

### **Eye Rest Timer**
- Triggers every **20 minutes**
- Shows popup for **20 seconds**
- Plays start sound (if enabled)
- Plays end sound (if enabled)

### **Break Timer**  
- Triggers every **55 minutes** of work
- Shows **30-second warning** before break
- Break lasts for **5 minutes**
- Provides delay/skip options

### **Audio System**
- All sounds enabled by default
- Volume at 50% level
- Uses system sounds (no custom audio initially)

## 🔍 **How to Verify Default Values**

When you start the application, check that the settings window shows:

1. **Eye Rest Settings Section**:
   - "Remind every: [20] minutes"
   - "Rest for: [20] seconds"
   - Both sound checkboxes checked ✅

2. **Break Time Settings Section**:
   - "Work for: [55] minutes"  
   - "Break for: [5] minutes"
   - "Show warning before break" checked ✅
   - "Warning time: [30] seconds"

3. **Audio Settings Section**:
   - "Enable audio notifications" checked ✅
   - Volume slider at 50%
   - Custom sound field empty

4. **Application Settings Section**:
   - "Start with Windows" unchecked ❌
   - "Minimize to system tray" checked ✅
   - "Show in taskbar" unchecked ❌

## 🚨 **If Values Are Different**

If you see different values, it could indicate:

1. **Existing Configuration**: A config file already exists with different values
2. **Configuration Error**: The loading process encountered an issue
3. **Validation Issues**: Invalid values were corrected to defaults

## 📂 **Configuration File Location**

Default values are saved to:
```
%APPDATA%/EyeRest/config.json
```

You can delete this file to reset to defaults, or check its contents to see current values.

## ✅ **Summary**

The default configuration implements the **20-20-20 rule** for eye health:
- **20 minutes** of work
- **20 seconds** of eye rest (looking at something 20+ feet away)
- Plus **55-minute work periods** with **5-minute physical breaks**

These values are based on ergonomic best practices for computer users and can be customized through the settings interface.