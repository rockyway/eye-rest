# Advanced Tab Startup and WindowChromium Integration Fix

## Summary
This fix addresses issues with the Advanced tab's "Start with Windows" and "Start minimized to system tray" features, and adds modern Windows 11 styling using ModernWpf.

## Issues Fixed

### 1. "Start with Windows" Enhancement
**Problem:** The registry mechanism was implemented but lacked:
- Command-line argument support for minimized startup
- Verification after registry write
- Proper coordination with "Start minimized" setting

**Solution:**
- Enhanced `IStartupManager` interface with overload: `EnableStartup(bool startMinimized)`
- Added `--minimized` command-line argument to registry entry when both settings are enabled
- Implemented registry write verification in `StartupManager.EnableStartup()`
- Updated `MainWindowViewModel` to pass `StartMinimized` flag when enabling startup
- Both settings now coordinate: changing either updates the registry appropriately

**Files Modified:**
- `Services/IStartupManager.cs` - Added overload for `EnableStartup(bool startMinimized)`
- `Services/StartupManager.cs` - Implemented command-line argument support and verification
- `ViewModels/MainWindowViewModel.cs` - Updated `SaveStartupSettingAsync()` and `SaveStartMinimizedAsync()`

### 2. "Start Minimized to System Tray" Implementation
**Problem:** Setting was saved but never checked at startup - window always showed on app launch.

**Solution:**
- Added command-line argument parsing in `App.xaml.cs` `OnStartup()` method
- Load configuration before showing window
- Check both command-line `--minimized` flag and `config.Application.StartMinimized`
- Only show window if neither condition is true
- Window remains hidden in system tray when starting minimized

**Files Modified:**
- `App.xaml.cs` - Added argument parsing and conditional window display logic

### 3. WindowChromium / Modern Windows 11 Styling
**Problem:** Application lacked modern Windows 11 appearance and styling.

**Solution:**
- Added ModernWpfUI NuGet package (v0.9.6) for native Windows 11 styling
- Integrated ModernWpf resources in `App.xaml`
- Applied `ui:WindowHelper.UseModernWindowStyle="True"` to MainWindow
- Integrated ModernWpf ThemeManager with existing dark/light theme system
- Updated `ApplyThemeGlobally()` to synchronize ModernWpf and custom themes

**Files Modified:**
- `EyeRest.csproj` - Added ModernWpfUI package reference
- `App.xaml` - Added ModernWpf namespaces and resources
- `Views/MainWindow.xaml` - Applied ModernWpf window styling
- `ViewModels/MainWindowViewModel.cs` - Integrated ThemeManager in `ApplyThemeGlobally()`

## Technical Details

### Command-Line Argument Format
When "Start with Windows" and "Start minimized to system tray" are both enabled:
```
Registry Value: "C:\Path\To\EyeRest.exe" --minimized
```

### Startup Flow
1. Windows starts application (with optional `--minimized` argument)
2. `App.OnStartup()` parses command-line arguments
3. Configuration loaded from disk
4. Check `startMinimizedFromArgs || config.Application.StartMinimized`
5. If true: Skip `mainWindow.Show()`, remain in system tray
6. If false: Show main window normally

### Theme Integration
ModernWpf's ThemeManager now synchronizes with the existing dark mode toggle:
1. User toggles Dark Mode checkbox
2. `IsDarkMode` property setter calls `ApplyThemeGlobally()`
3. Updates `ModernWpf.ThemeManager.Current.ApplicationTheme`
4. Applies custom theme resources (DarkTheme.xaml or LightTheme.xaml)
5. Both ModernWpf controls and custom UI update simultaneously

## Testing Checklist

### Manual Testing Required

#### Start with Windows
- [ ] Enable "Start with Windows" → Check registry key created
- [ ] Disable "Start with Windows" → Check registry key removed
- [ ] Restart Windows → Application starts automatically
- [ ] Check registry value format is correct with executable path

#### Start Minimized to System Tray
- [ ] Enable "Start with Windows" + "Start minimized" → Check registry includes `--minimized`
- [ ] Restart Windows → App starts hidden in system tray (no window visible)
- [ ] Click system tray icon → Window restores from tray
- [ ] Enable only "Start minimized" (without "Start with Windows") → Setting saves correctly
- [ ] Change "Start minimized" while "Start with Windows" is enabled → Registry updates immediately

#### Combined Behavior
- [ ] Enable "Start with Windows" first, then enable "Start minimized" → Registry updates
- [ ] Enable "Start minimized" first, then enable "Start with Windows" → Registry includes argument
- [ ] Disable "Start minimized" while "Start with Windows" is enabled → Registry removes argument
- [ ] All combinations save and persist correctly across app restarts

#### ModernWpf Styling
- [ ] Application has modern Windows 11 appearance (rounded corners, modern controls)
- [ ] Title bar has modern chrome styling
- [ ] Dark mode toggle works → Both ModernWpf and custom themes update
- [ ] All windows (Main, Analytics, Popups) reflect theme changes
- [ ] Window resizing and snapping work correctly
- [ ] Modern scrollbars and controls render properly

### Registry Location
Check: `HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`
Key Name: `EyeRest`

## Benefits

1. **Reliable Startup**: Registry writes are now verified, reducing startup failures
2. **True Minimized Start**: Application can start completely hidden in system tray
3. **Coordinated Settings**: "Start with Windows" and "Start minimized" work together seamlessly
4. **Modern Appearance**: Windows 11 native styling improves user experience
5. **Better Theme Integration**: ModernWpf and custom themes work harmoniously

## Compatibility

- Requires .NET 8.0+
- Windows 10 version 1809+ (for ModernWpf)
- Windows 11 for optimal styling (works on Windows 10 with compatible appearance)

## Future Enhancements

- Consider adding command-line help: `--help` to document available arguments
- Add logging for startup arguments to help troubleshooting
- Consider alternative minimized start methods (Windows Task Scheduler) for enterprise scenarios
- Explore additional ModernWpf controls to further modernize UI

## Notes

- All changes maintain backward compatibility with existing configurations
- Settings are auto-saved immediately when changed
- No breaking changes to existing functionality
- ModernWpfUI package adds ~2MB to application size
