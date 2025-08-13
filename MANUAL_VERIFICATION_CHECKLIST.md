# EyeRest Manual Verification Checklist

## Pre-Test Setup
- [ ] Application is built successfully (`dotnet build --configuration Debug`)
- [ ] No compilation errors present
- [ ] EyeRest.exe exists in `bin\Debug\net8.0-windows\`

## Test Execution Instructions

### 1. Single System Tray Icon Test
**Objective**: Verify only ONE tray icon appears (not two)

**Steps**:
1. [ ] Launch EyeRest.exe
2. [ ] Check system tray area (bottom-right corner)
3. [ ] Count EyeRest icons in system tray
4. [ ] Right-click on tray icon
5. [ ] Verify context menu shows "Restore" and "Exit" options

**Expected Results**:
- [ ] ✅ Exactly ONE EyeRest tray icon visible
- [ ] ✅ Context menu displays correctly
- [ ] ✅ No duplicate icons present

**Test Result**: PASS / FAIL

---

### 2. Countdown Timer Display Test
**Objective**: Verify countdown shows when running and updates in real-time

**Steps**:
1. [ ] Double-click tray icon to open main window
2. [ ] Observe the countdown display below the status indicator
3. [ ] Wait 10 seconds and observe countdown changes
4. [ ] Click "Stop Timers" button
5. [ ] Observe countdown display behavior

**Expected Results**:
- [ ] ✅ Countdown shows format like "Next eye rest: 19m 45s"
- [ ] ✅ Countdown decreases every second
- [ ] ✅ Countdown disappears or shows "Timers not running" when stopped

**Test Result**: PASS / FAIL

---

### 3. Auto-Start Functionality Test
**Objective**: Verify timers start automatically when app opens

**Steps**:
1. [ ] Close EyeRest application completely
2. [ ] Launch EyeRest.exe again
3. [ ] Immediately check status indicator (should be green "Running")
4. [ ] Check countdown display (should show time immediately)

**Expected Results**:
- [ ] ✅ Status shows "Running" (green) immediately on startup
- [ ] ✅ Countdown display appears immediately
- [ ] ✅ No manual "Start Timers" click required

**Test Result**: PASS / FAIL

---

### 4. Icon Integration Test
**Objective**: Verify icon consistency across window and tray

**Steps**:
1. [ ] Open main window (double-click tray icon)
2. [ ] Check window title bar for application icon
3. [ ] Check system tray for application icon
4. [ ] Verify both icons appear consistent

**Expected Results**:
- [ ] ✅ Window title bar displays application icon
- [ ] ✅ System tray displays application icon
- [ ] ✅ Icons appear consistent (same design/source)

**Test Result**: PASS / FAIL

---

### 5. Timer Status Indicator Test
**Objective**: Verify green/red status updates correctly

**Steps**:
1. [ ] Open main window
2. [ ] Observe status indicator (circle next to "Running"/"Stopped")
3. [ ] Note the color and text
4. [ ] Click "Stop Timers" button
5. [ ] Observe status indicator changes
6. [ ] Click "Start Timers" button
7. [ ] Observe status indicator changes again

**Expected Results**:
- [ ] ✅ Shows green circle with "Running" when timers active
- [ ] ✅ Shows red circle with "Stopped" when timers paused
- [ ] ✅ Status updates immediately when Start/Stop buttons clicked
- [ ] ✅ Window title changes to include status ("Eye-rest Settings - Running")

**Test Result**: PASS / FAIL

---

### 6. Minimize to Tray Test
**Objective**: Verify window minimizes to tray instead of closing

**Steps**:
1. [ ] Open main window
2. [ ] Click the X (close) button on window
3. [ ] Check if application is still running (tray icon present)
4. [ ] Double-click tray icon to restore window

**Expected Results**:
- [ ] ✅ Window hides when X button clicked
- [ ] ✅ Application continues running (tray icon still visible)
- [ ] ✅ Double-click tray icon restores window

**Test Result**: PASS / FAIL

---

### 7. Application Shutdown Test
**Objective**: Verify clean shutdown removes tray icon

**Steps**:
1. [ ] Right-click tray icon
2. [ ] Click "Exit" from context menu
3. [ ] Wait 3 seconds
4. [ ] Check system tray area

**Expected Results**:
- [ ] ✅ Application closes completely
- [ ] ✅ Tray icon is removed from system tray
- [ ] ✅ No EyeRest processes remain running

**Test Result**: PASS / FAIL

---

## Overall Test Summary

**Tests Passed**: ___/7
**Tests Failed**: ___/7

**Overall Result**: PASS / FAIL

### Critical Issues Found:
- [ ] None
- [ ] Issue 1: ________________________________
- [ ] Issue 2: ________________________________
- [ ] Issue 3: ________________________________

### Minor Issues Found:
- [ ] None
- [ ] Issue 1: ________________________________
- [ ] Issue 2: ________________________________

## Deployment Readiness

Based on manual verification results:

- [ ] ✅ **READY FOR DEPLOYMENT** - All tests passed
- [ ] ⚠️ **NEEDS MINOR FIXES** - Minor issues identified
- [ ] ❌ **NOT READY** - Critical issues found

**Tester Signature**: _________________ **Date**: _________________

**Comments**:
_________________________________________________________________________
_________________________________________________________________________
_________________________________________________________________________