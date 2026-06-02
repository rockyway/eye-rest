# Popup Window Pooling Implementation Plan (rev 2 — post-review)

> **For agentic workers:** Steps use checkbox (`- [ ]`) syntax. This refactors a fragile subsystem (popup lifecycle — see CLAUDE.md lessons-learned). Make changes surgically; verify with the gcdump repro in Task 7.

**Goal:** Eliminate the residual popup-window memory leak by reusing a small pool of `PopupWindow` shells (Hide + return-to-pool) instead of creating and `Close()`-ing a new window every cycle.

**Architecture:** `gcroot` proved every closed `PopupWindow` on macOS is pinned forever by Avalonia.Native's `WindowAutomationPeer → AvnAutomationPeer → MicroComShadow` **strong GC handle**, not released on `Window.Close()` (framework bug; both reviewers verified `Window.Close() → PlatformImpl.Dispose()` destroys the impl + its leaked peer, whereas `Hide()` keeps it). Pooling makes the peer get created **once per shell** and reused. Content controls are recreated per show but freed when the shell's content is cleared (they are not strong-handled).

**Tech Stack:** .NET 8, Avalonia 11.3, C#. No headless UI test infra exists, so behavior is verified via a `dotnet-gcdump` before/after diff (Task 7), not unit tests.

---

## Review outcome (2 reviewers — 1 internal Claude, 1 external codex/gpt-5.4)

Both reached **APPROVE-WITH-CHANGES** conditioned on three blocking fixes, now incorporated:

- **[FIXED] B1 — missed `Close()` site:** `BreakPopup.axaml.cs:459` `window.Close()` runs on the DEFAULT confirmed-break path (`RequireConfirmationAfterBreak` defaults `true`). It fires `ActionSelected` (→ factory `ReleaseToPool`, which pools the shell) then calls real `Close()` on that just-pooled shell → disposes it → next `Rent` re-shows a disposed window → **crash**. Fix: remove the direct `window.Close()`; let the factory/service own teardown (Task 4).
- **[FIXED] B2 — wrong "OnOpened fires once" premise:** Both reviewers verified against Avalonia 11.3.0 source that `WindowBase.Show()` calls `OnOpened` on **every** `Show()`, and `FrameSize` is valid inside `OnOpened` (ShowCore runs layout first). So the original plan's deferred `Show() → Dispatcher.Post(ApplyShowState)` is **redundant and racy** — removed. `OnOpened → ApplyShowState` alone is correct and sufficient.
- **[FIXED] B3 — cross-cycle stale-release race:** A `_released`-only guard is insufficient under shell reuse. A deferred `CloseSpecificPopup(myPopup)` posted by cycle N (service close paths at lines ~138/223/314/467) can run AFTER the shell was pooled and re-rented by cycle N+1, releasing the new popup. Fix: a per-rent **lease (generation) token** — `ReleaseToPool(expectedLease)` no-ops unless `expectedLease == _lease`. Reset the lease only in `Rent()` (NOT in `Show()`).

Non-blocking items folded in: defensive `ReleaseToPool` (snapshot `Closed`, clear before invoke), gcdump must also check content types stay flat (Task 7), manual ESC/focus check on the 2nd+ cycle (Task 6), optional `ClearPool()` on shutdown (Task 8, optional).

---

## File structure

- **Modify** `EyeRest.UI/Views/PopupWindow.axaml.cs` — add pool + lease (`Rent`/`ReleaseToPool(lease)`), extract `OnOpened` body into `ApplyShowState()`, revert `Show()` to minimal.
- **Modify** `EyeRest.UI/Views/BreakPopup.axaml.cs` — remove the direct `window.Close()` (B1).
- **Modify** `EyeRest.UI/Services/AvaloniaPopupWindowFactory.cs` — `PopupWindow.Rent()`, capture lease, wire content events to `ReleaseToPool(lease)`.
- **Modify** `EyeRest.UI/Services/AvaloniaNotificationService.cs` — capture lease per show; route the 3 cycle close call sites through `ReleaseToPool(lease)`; `CloseSpecificPopup` takes a lease.

No `IPopupWindow` change: `Close()` stays for real teardown; the cycle never calls it.

---

## Task 1: Add pool + lease + release semantics to PopupWindow

**Files:** Modify `EyeRest.UI/Views/PopupWindow.axaml.cs`

- [ ] **Step 1:** Ensure `using System.Collections.Generic;` is at the top (for `Stack<>`). Add if missing.

- [ ] **Step 2:** Add pool/lease fields (after the existing `_activePopupCount` field, ~line 35)

```csharp
        /// <summary>
        /// Pool of reusable shells. Avalonia.Native pins each shown Window forever via its
        /// accessibility peer (docs/plan/009); reusing shells makes that peer created once per
        /// shell instead of leaked per cycle. Bounded — at most a couple of shells are ever live.
        /// All access is on the UI thread.
        /// </summary>
        private static readonly Stack<PopupWindow> s_pool = new();
        private const int MaxPoolSize = 4;
        private static long s_leaseCounter;

        // Generation token: a deferred close from a prior cycle must not release a shell that
        // has since been re-rented for a new popup. Each Rent() assigns a fresh lease; release
        // only acts when the caller's captured lease still matches.
        private long _lease;
        private bool _released = true; // a shell not currently rented is "released"

        /// <summary>The lease of the current rental. Capture this when scheduling a deferred close.</summary>
        public long Lease => _lease;
```

- [ ] **Step 3:** Add `Rent` + `ReleaseToPool` (near `Show()`/`OnClosed`)

```csharp
        /// <summary>
        /// Rents a shell from the pool (or creates one) with a fresh lease. Returned shell has
        /// no content and no Closed subscribers. Caller must SetPopupContent + Show. UI-thread only.
        /// </summary>
        public static PopupWindow Rent()
        {
            var w = s_pool.Count > 0 ? s_pool.Pop() : new PopupWindow();
            w._lease = ++s_leaseCounter;
            w._released = false;
            return w;
        }

        /// <summary>
        /// "Soft close": hide the shell and return it to the pool for reuse instead of destroying
        /// it (real Close() leaks the native accessibility peer on macOS). No-op if already
        /// released this cycle, or if <paramref name="expectedLease"/> is stale (the shell was
        /// re-rented). Raises <see cref="Closed"/> exactly once, then clears subscribers + content.
        /// UI-thread only.
        /// </summary>
        public void ReleaseToPool(long expectedLease)
        {
            if (_released || _lease != expectedLease) return;
            _released = true;

            try { base.Hide(); } catch { /* best effort */ }
            _activePopupCount = Math.Max(0, _activePopupCount - 1);

            // Snapshot + clear subscribers BEFORE invoking so a re-entrant release sees none,
            // and clear per-cycle references so the pooled shell retains nothing.
            var handlers = Closed;
            Closed = null;
            ContentArea.Content = null;   // release the heavy content visual tree
            PopupContent = null;
            _pendingPlacement = null;
            DataContext = null;

            try { handlers?.Invoke(this, EventArgs.Empty); }
            finally
            {
                if (s_pool.Count < MaxPoolSize)
                    s_pool.Push(this);
            }
        }
```

- [ ] **Step 4:** Revert `Show()` to minimal (remove any `_released`/deferred-post logic — `OnOpened` re-fires every Show and handles state). Replace the existing `Show()` (~lines 61-65):

```csharp
        public new void Show()
        {
            _activePopupCount++;
            base.Show();
        }
```

- [ ] **Step 5:** Extract `OnOpened` body into `ApplyShowState()` (replace lines ~74-108)

```csharp
        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            // OnOpened re-fires on every Show() (incl. pooled reuse) in Avalonia 11.3, and
            // FrameSize is already valid here — so this is the single per-show setup path.
            ApplyShowState();
        }

        private void ApplyShowState()
        {
            if (_pendingPlacement.HasValue && FrameSize.HasValue)
            {
                RepositionWithActualSize(_pendingPlacement.Value);
                _pendingPlacement = null;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                MacOSNativeWindowHelper.SetWindowLevel(this, 3); // NSFloatingWindowLevel
                MacOSNativeWindowHelper.OrderFront(this);
                MacOSNativeWindowHelper.MakeKeyWindow(this);
            }
            else
            {
                Activate();
            }

            Focus();
            Focusable = true;
        }
```

- [ ] **Step 6: Build** — `dotnet build EyeRest.UI/EyeRest.UI.csproj -v q` → `Build succeeded. 0 Warning(s) 0 Error(s)`.

---

## Task 2: Update the factory to Rent + wire content events to ReleaseToPool(lease)

**Files:** Modify `EyeRest.UI/Services/AvaloniaPopupWindowFactory.cs`

- [ ] **Step 1:** Replace the whole factory body

```csharp
using EyeRest.UI.Views;

namespace EyeRest.Services
{
    public class AvaloniaPopupWindowFactory : IPopupWindowFactory
    {
        public IPopupWindow CreateEyeRestWarningPopup()
        {
            var popup = PopupWindow.Rent();
            var lease = popup.Lease;
            var content = new EyeRestWarningPopup();
            popup.SetPopupContent(content, 300, 400);
            content.WarningCompleted += (s, e) => popup.ReleaseToPool(lease);
            return popup;
        }

        public IPopupWindow CreateEyeRestPopup()
        {
            var popup = PopupWindow.Rent();
            var lease = popup.Lease;
            var content = new EyeRestPopup();
            popup.SetPopupContent(content, 500, 500);
            content.Completed += (s, e) => popup.ReleaseToPool(lease);
            return popup;
        }

        public IPopupWindow CreateBreakWarningPopup()
        {
            var popup = PopupWindow.Rent();
            var lease = popup.Lease;
            var content = new BreakWarningPopup();
            popup.SetPopupContent(content, 280, 400);
            content.Completed += (s, e) => popup.ReleaseToPool(lease);
            return popup;
        }

        public IPopupWindow CreateBreakPopup()
        {
            var popup = PopupWindow.Rent();
            var lease = popup.Lease;
            var content = new BreakPopup();
            popup.SetPopupContent(content, 700, 700);
            content.ActionSelected += (s, action) =>
            {
                if (content.CanClose())
                    popup.ReleaseToPool(lease);
            };
            return popup;
        }
    }
}
```

- [ ] **Step 2: Build** → `Build succeeded`.

---

## Task 3: Route the service's cycle close paths through ReleaseToPool(lease)

**Files:** Modify `EyeRest.UI/Services/AvaloniaNotificationService.cs`

- [ ] **Step 1: `CloseCurrentPopup`** (~line 658-670) — synchronous, so its lease is always current:

```csharp
        private void CloseCurrentPopup()
        {
            lock (_lockObject)
            {
                if (_currentPopup != null)
                {
                    try { _currentPopup.ReleaseToPool(_currentPopup.Lease); }
                    catch (Exception ex) { _logger.LogWarning(ex, "Error releasing popup"); }
                    _currentPopup = null;
                }
            }
        }
```

- [ ] **Step 2: `CloseSpecificPopup`** — add a `lease` parameter (~line 678-695):

```csharp
        private void CloseSpecificPopup(PopupWindow? popup, long lease)
        {
            if (popup == null) return;
            lock (_lockObject)
            {
                try { popup.ReleaseToPool(lease); }
                catch (Exception ex) { _logger.LogWarning(ex, "Error releasing specific popup"); }
                if (_currentPopup == popup)
                    _currentPopup = null;
            }
        }
```

- [ ] **Step 3:** In each of the 4 real show methods (`ShowEyeRestWarningInternalAsync` ~line 99, `ShowEyeRestReminderInternalAsync` ~line 177, `ShowBreakWarningInternalAsync` ~line 271, `ShowBreakReminderInternalAsync` ~line 403), capture the lease right after creating `myPopup`, e.g.:

```csharp
                    myPopup = (PopupWindow)_popupWindowFactory.CreateEyeRestWarningPopup();
                    var myLease = myPopup.Lease;
                    _currentPopup = myPopup;
```

and change each deferred cleanup `CloseSpecificPopup(myPopup);` to `CloseSpecificPopup(myPopup, myLease);`.

> NOTE: `myLease` is declared inside the `Dispatcher.UIThread.Post(...)` lambda where `myPopup` is assigned, but the cleanup `CloseSpecificPopup(myPopup, myLease)` runs in a SECOND, later `Dispatcher.UIThread.Post` after the `await`. Capture the lease into the outer scope so both lambdas see it: declare `long myLease = 0;` next to `PopupWindow? myPopup = null;` at method top, assign `myLease = myPopup.Lease;` in the show lambda, and read it in the cleanup lambda.

- [ ] **Step 4: Test popup** (`ShowBreakReminderTestAsync` ~line 344-374): capture `long testLease = 0;` at top, set `testLease = testPopup.Lease;` after create, and change `try { testPopup?.Close(); } catch { }` to `try { testPopup?.ReleaseToPool(testLease); } catch { }`.

- [ ] **Step 5: Verify no cycle `.Close()` remains** — `grep -n "\.Close()" EyeRest.UI/Services/AvaloniaNotificationService.cs` → only the overlay `extra.Close()` (intentional, rare) should remain.

- [ ] **Step 6: Build** → `Build succeeded`.

---

## Task 4: Remove the direct window.Close() from BreakPopup (B1)

**Files:** Modify `EyeRest.UI/Views/BreakPopup.axaml.cs`

- [ ] **Step 1:** Replace `ConfirmCompletion_Click` (~lines 438-474) so it fires `ActionSelected` and lets the factory/service own teardown (no direct `window.Close()`):

```csharp
        private void ConfirmCompletion_Click(object? sender, RoutedEventArgs e)
        {
            Debug.WriteLine("BreakPopup.ConfirmCompletion_Click: User confirmed break completion");

            // Stop forward timer before closing
            StopForwardTimer();

            _waitingForConfirmation = false;  // Clear flag to allow window to close
            _forceClose = true;               // CanClose() now returns true

            // Fire the action; the popup factory's ActionSelected handler releases the pooled
            // shell (ReleaseToPool). Do NOT call window.Close() here — that would destroy a shell
            // the factory just returned to the pool and crash the next reuse (docs/plan/009 B1).
            ActionSelected?.Invoke(this, BreakAction.ConfirmedAfterCompletion);
        }
```

- [ ] **Step 2: Build** → `Build succeeded`. Confirm no other `.Close()` in Views: `grep -rn "\.Close()" EyeRest.UI/Views/*.axaml.cs` → empty.

---

## Task 5: Full test suite + commit

- [ ] **Step 1:** `dotnet test EyeRest.Tests.Avalonia/EyeRest.Tests.Avalonia.csproj -v q` → `Passed! Failed: 0` (206 tests).
- [ ] **Step 2: Commit**

```bash
git add EyeRest.UI/Views/PopupWindow.axaml.cs EyeRest.UI/Views/BreakPopup.axaml.cs EyeRest.UI/Services/AvaloniaPopupWindowFactory.cs EyeRest.UI/Services/AvaloniaNotificationService.cs docs/plan/009-popup-window-pooling.md
git commit -m "fix(macOS): pool popup window shells to stop automation-peer leak"
```

---

## Task 6: Manual functional check (popups still work on REUSE)

- [ ] Run a fresh build at eyeRest.intervalMinutes=1. Over ≥3 cycles confirm: warning popup top-right, reminder popup, dim overlays, countdown, and **specifically on the 2nd+ cycle** (pooled reuse): popup appears on the correct monitor, is focused/topmost, **ESC and the close button still work** (focus path via `OnOpened → ApplyShowState`), and the break confirmation ("Done") closes cleanly and the next break still appears. Watch for any popup that fails to re-show or shows stale content.

---

## Task 7: Verify the leak is gone (gcdump diff — the real proof)

- [ ] Baseline: `dotnet-gcdump ps` → runtime PID; `dotnet-gcdump collect -p <PID> -o /tmp/pool-baseline.gcdump`.
- [ ] Wait ~6 cycles, capture `/tmp/pool-after.gcdump`.
- [ ] Compare — BOTH shell and content types must stay flat:
```bash
for f in /tmp/pool-baseline.gcdump /tmp/pool-after.gcdump; do
  echo "== $f =="
  dotnet-gcdump report "$f" | grep -E "GC Heap bytes|EyeRest.UI.Views.PopupWindow|EyeRest.UI.Views.EyeRestPopup|EyeRest.UI.Views.BreakPopup"
done
```
PASS: `PopupWindow` after ≈ baseline (bounded by MaxPoolSize, NOT +1/cycle); content types flat; GC Heap growth across the 6 cycles near-flat (vs +2.34 MB / +6 PopupWindow before this fix).

---

## Task 8 (optional, non-blocking): Clean pool shutdown

- [ ] If desired, add `PopupWindow.ClearPool()` (pop all, real `Close()` each) and call it from app shutdown (`App.axaml.cs` OnExplicitShutdown path) for tidiness. Not required — pooled hidden shells (≤4) are freed at process exit anyway.

---

## Residual risks accepted

- **FrameSize timing:** reviewers confirmed `FrameSize` is valid in `OnOpened`. If empirical sizing issues appear on reuse (Task 6), add a one-shot `Resized` handler that re-runs `RepositionWithActualSize(_currentPlacement)` once — do NOT add an unconditional per-show deferred post (B2).
- **Event semantics:** steady-state teardown is now a soft release; base `Window.Closed`/`OnClosed` no longer fires during cycles. Verified nothing in-repo subscribes to base `Window.Closed` for these popups (all use the shadowing `PopupWindow.Closed`).
