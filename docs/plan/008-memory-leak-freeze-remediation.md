# 008 — Memory Leak & Freeze-Under-Memory-Pressure Remediation

**Status:** In progress
**Branch:** `feature/fix-memory-leak-freeze`
**Date:** 2026-06-02

## Problem

"Blink Twice EyeRest (Not Responding)" — the macOS app freezes at 0% CPU after a system
memory-pressure episode and never recovers (observed: PID 92329, frozen ~28h, 25.9 MB Real /
2.59 GB virtual / 1,554 Mach ports). Prior fix `e57a045` targeted the wrong layer (timer
recovery + explicit GC blocking) and did not help.

## Root cause (evidence-based)

A live `sample` of the frozen process showed the main thread idle in the **normal** Cocoa
run loop (`__CFRunLoopServiceMachPort` → `mach_msg`), **no GC in progress**, all 26 threads
quiescent, process state `S`. So the persistent hang is **not** a GC stall and **not** a
managed deadlock. All logging (incl. threadpool `System.Threading.Timer`) stopped
simultaneously → the **whole process was OS-suspended** under memory pressure. The App Nap
activity token (`NSActivityLatencyCritical`) does not cover memory-pressure suspension.

**Why it became a suspension target — an unbounded managed memory leak:**
PerformanceMonitor stats show GCHeap **18 → 116 MB over 32h, monotonic, through 40 gen2 GCs**.
Reproduced on a fresh instance (1-min eye-rest cycles, 5 monitors): two `dotnet-gcdump`s six
cycles apart → heap **8.2 → 12.3 MB / +49,556 objects** (~8,260 objects, ~0.68 MB **per cycle**),
*after* forced GC. Two distinct leaks identified by type-delta:

- **Leak A — per-call `JsonSerializerOptions`.** `ConfigurationService.LoadConfigurationAsync`
  → `ConfigurationMigrator.MigrateFromJson` deserializes the whole `AppConfiguration` graph
  with a **new `JsonSerializerOptions` every load**; config is loaded on every popup cycle.
  Heap shows the System.Reflection.Emit cluster (`DynamicMethod`, `DynamicILGenerator`,
  `DynamicScope`, `ScopeTree`, `SignatureHelper`, `DynamicResolver`) all growing ~164/cycle.
  Same anti-pattern in `TimerConfigurationService` and `UIConfigurationService`.
- **Leak B — dim-overlay windows.** `AvaloniaNotificationService.ShowDimOverlays` creates a
  raw `new Window` per monitor per cycle. Heap shows `Avalonia.Controls.Window` 0→12 plus
  visual-tree types (`ValueStore` +729, `WeakReference<AvaloniaObject>` +829,
  `EventHandler<AvaloniaPropertyChangedEventArgs>` +1404). Popups themselves do NOT leak.

**Chain:** leaks → heap & port bloat → heavy background process → macOS evicts + suspends it
under system memory pressure → never rescheduled → "Not Responding" forever.

## Remediation (full)

1. **Leak A:** hoist each `JsonSerializerOptions` to `static readonly` (mirrors the correct
   pattern already in `ConfigurationService.cs:13` and `AudioRecentsService.cs:18`).
2. **Leak B:** pool & reuse overlay windows (Hide, not Close; recolor/resize/reposition on
   reuse; trim/grow on screen-count change) — eliminates per-cycle `Window` allocation.
3. **Regression test:** deterministic heap/alloc-stability test for both paths.
4. **Defense — native memory-pressure handler:** `DISPATCH_SOURCE_TYPE_MEMORYPRESSURE` →
   proactive trim, so the app is a good citizen and less likely to be suspended.
5. **Defense — external watchdog:** app writes a heartbeat file; a launchd watchdog agent
   restarts a frozen instance (only out-of-process recovery can revive a suspended process).

## Verification

- `dotnet build` clean; `dotnet test` green.
- Re-run gcdump diff on a fresh instance: heap/object growth across N cycles ≈ flat.
- Mandatory final audits: Technical Architect (perf/lifecycle/leak) + Spec-vs-Implementation.
