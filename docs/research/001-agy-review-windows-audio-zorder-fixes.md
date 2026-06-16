# Research: agy code review — Windows audio + z-order fixes
_Date: 2026-06-16_
_Reviewer: agy (Antigravity / Gemini 3.5 Flash High)_
_Scope: commits 467fcf6, 2ac8f62_

## Summary
agy reviewed the PortAudio `WindowsWavePlayer` audio fix and the `RaisePopupAboveOverlays`
z-order fix. The z-order fix was judged robust and correct (no regressions, proper
cross-platform no-op guards). The audio fix drew 7 findings, mostly around the
`WindowsWavePlayer` lifecycle/disposal and real-time-callback hygiene.

## Findings

| ID | Severity | Location | Description |
|----|----------|----------|-------------|
| F-01 | Critical* | AudioService.cs | Class doesn't declare `IDisposable` → DI never calls `Dispose()` → PortAudio + event-subscription cleanup never runs. (*Overstated: the singleton lives for the whole process; native cleanup happens at process exit anyway. Real severity ~Low/Med, but the fix is trivial and correct.) |
| F-02 | High | WindowsWavePlayer (Play vs Dispose) | If `Dispose()` calls `PortAudio.Terminate()` while a `Play()` is mid-stream on a thread-pool thread, native access violation. Only reachable once F-01 is fixed (Dispose is currently never called). |
| F-03 | Medium | PlayViaPortAudio callback | `new float[]` allocated inside the real-time audio callback to pad trailing silence → potential GC jitter. (Inherited from the inkspoke reference; only fires on the final partial callback.) |
| F-04 | Medium | `_paInitLock` | Instance-level lock guards the *global*, non-thread-safe `PortAudio.Initialize/Terminate`. Should be static. |
| F-05 | Medium | EnsurePortAudioInitialized | No `_disposed` check → a `Play()` racing after `Dispose()` could re-`Initialize()` PortAudio. |
| F-06 | Low | ResolveOutputDevice | `Pa_GetHostApiInfo` native pointer marshalled without a null check (caught by try/catch, but sloppy). |
| F-07 | Low | ConvertToFloat | Little-endian byte-shift assumptions are undocumented (fine: Windows-only assembly, all LE). |

## Z-order fix verdict
Robust and correct:
- `SWP_NOACTIVATE` keeps it above topmost overlays without stealing focus.
- Immediate + deferred (Background priority) re-raise resolves native-handle/positioning timing.
- Null-handle check + try/catch guards disposed/closed popups.
- `!OperatingSystem.IsWindows()` guard keeps it a no-op on macOS/Linux.

## Disposition (this session)
Applied: F-01 (implement IDisposable), F-02 (active-playback guard so Terminate is skipped
while playing), F-04 (static init lock), F-05 (`_disposed` check in EnsurePortAudioInitialized),
F-06 (null-pointer check), F-03 (reuse a cached silence buffer instead of per-callback alloc).
F-07: left as-is with a clarifying comment (Windows-only, little-endian).

## Files Examined
- EyeRest.Platform.Windows/Services/WindowsWavePlayer.cs
- EyeRest.Platform.Windows/Services/AudioService.cs
- EyeRest.Core/Services/AudioServiceBase.cs
- EyeRest.Abstractions/Services/IAudioService.cs
- EyeRest.UI/Services/AvaloniaNotificationService.cs
- EyeRest.Platform.Windows/EyeRest.Platform.Windows.csproj
