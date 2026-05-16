# 005 — BL-002 Per-popup Audio + Custom Audio Sources — Design

**Status:** Draft (approved for planning)
**Date:** 2026-05-16
**Backlog item:** BL-002 (`docs/backlog/001-product-backlog.md`)
**Approach:** A — Minimal-disruption extension of existing `IAudioService`

---

## 1. Goal

Refactor audio playback so each of four popup events (eye-rest start, eye-rest end, break start, break end) has its own configurable audio source. Each channel supports four source modes:

- **Off** — no audio, no URL action.
- **Default** — bundled WAV asset specific to that channel.
- **File** — user-selected local audio file.
- **Url** — open a URL (e.g. a YouTube playlist) in the user's default browser.

Source mode is mutually exclusive at runtime, but both `CustomFilePath` and `Url` are persisted independently so the user can switch back and forth without re-selecting inputs.

A global `AudioSettings.Enabled` master toggle remains as the system-wide mute switch.

---

## 2. Scope

### In scope
- New `AudioChannelSource` enum and `AudioChannelConfig` POCO.
- Per-channel config on `EyeRestSettings` and `BreakSettings` (`StartAudio`, `EndAudio`).
- Migration of legacy `StartSoundEnabled`/`EndSoundEnabled` bool toggles and `AudioSettings.CustomSoundPath`.
- Extension of `IAudioService` with `PlayChannelAsync(channel, config, ct)`. Existing methods retained as adapters.
- WAV playback (bundled + custom path) on Windows (`SoundPlayer`) and macOS (`AVAudioPlayer`).
- Four bundled default WAV assets at `EyeRest.UI/Assets/Sounds/<channel>.wav`.
- `IUrlOpener` service implemented with `Process.Start(..., UseShellExecute = true)`.
- Settings UI: a new "Popup Audio" card with four per-channel rows in `MainWindow.axaml`.
- `Meta.SchemaVersion` sentinel to refuse silent corruption by older binaries.
- Unit tests for config migration, source resolution, and disposal.

### Out of scope
- In-app browser / embedded webview for URL mode.
- Per-channel volume (volume stays global on `AudioSettings.Volume`).
- Audio format support beyond WAV (custom MP3/AAC files may work depending on platform codec, but no contract).
- Refactor of the AXAML into a reusable `AudioChannelEditor` UserControl — explicit follow-up item.

---

## 3. Architecture

### 3.1 Types (`EyeRest.Abstractions/Models/AppConfiguration.cs`)

```csharp
public enum AudioChannelSource { Off, Default, File, Url }

public class AudioChannelConfig
{
    public AudioChannelSource Source { get; set; } = AudioChannelSource.Default;
    public string? CustomFilePath { get; set; }
    public string? Url { get; set; }
}

public class EyeRestSettings
{
    // … existing fields …
    public AudioChannelConfig StartAudio { get; set; } = new();
    public AudioChannelConfig EndAudio   { get; set; } = new();
    // REMOVED: StartSoundEnabled, EndSoundEnabled
}

public class BreakSettings
{
    // … existing fields …
    public AudioChannelConfig StartAudio { get; set; } = new();
    public AudioChannelConfig EndAudio   { get; set; } = new();
    // REMOVED: StartSoundEnabled, EndSoundEnabled
}

public class AudioSettings
{
    public bool Enabled { get; set; } = true;
    public int  Volume  { get; set; } = 50;
    // REMOVED: CustomSoundPath
}

public class ConfigMetadata
{
    // … existing fields …
    public int SchemaVersion { get; set; } = 2; // NEW: 1 = pre-BL002, 2 = BL002
}
```

### 3.2 Service surface (`EyeRest.Abstractions/Services/IAudioService.cs`)

```csharp
public enum AudioChannel { EyeRestStart, EyeRestEnd, BreakStart, BreakEnd, BreakWarning }

public interface IAudioService
{
    bool IsAudioEnabled { get; }

    Task PlayChannelAsync(AudioChannel channel, AudioChannelConfig config,
                          CancellationToken cancellationToken = default);

    Task PlayEyeRestStartSound();   // adapter → PlayChannelAsync
    Task PlayEyeRestEndSound();     // adapter → PlayChannelAsync
    Task PlayBreakStartSound();     // adapter → PlayChannelAsync
    Task PlayBreakEndSound();       // adapter → PlayChannelAsync
    Task PlayBreakWarningSound();   // unchanged

    Task PlayCustomSoundTestAsync(string filePath);
    Task TestEyeRestAudioAsync();
}
```

### 3.3 Source resolution inside `PlayChannelAsync`

```
config.Source == Off      → no-op (Task.CompletedTask)
config.Source == Default  → play bundled WAV for that channel
config.Source == File     → play file at config.CustomFilePath (fallback to Default if missing)
config.Source == Url      → IUrlOpener.Open(config.Url) — no audio
```

If `AudioSettings.Enabled == false`, `PlayChannelAsync` is a no-op for all sources except `Url`. (URL opens regardless, because it's a user-action equivalent, not a sound effect.)

### 3.4 Lifecycle contract

Every playback call:
1. Acquires a per-service `SemaphoreSlim(1, 1)` — serializes concurrent calls, never overlap.
2. Constructs a fresh `SoundPlayer` (Win) or `AVAudioPlayer` (macOS) instance.
3. Plays the sound; `SoundPlayer` (synchronous API) is wrapped in `Task.Run(..., ct)` so the public `PlayChannelAsync` contract is honored and cancellation is checkable. `AVAudioPlayer` awaits its native completion callback.
4. Disposes the player in `finally`, including on `OperationCanceledException`.
5. No long-lived player field on the service.

Cancellation: caller passes a `CancellationToken`. Mid-playback cancel calls `player.Stop()` then disposes.

### 3.5 Bundled assets

- `EyeRest.UI/Assets/Sounds/eye-rest-start.wav` — ascending 2-note chime (~0.6 s).
- `EyeRest.UI/Assets/Sounds/eye-rest-end.wav` — descending 2-note chime (~0.5 s).
- `EyeRest.UI/Assets/Sounds/break-start.wav` — warm bell (~1.0 s).
- `EyeRest.UI/Assets/Sounds/break-end.wav` — energetic chime (~0.8 s).
- All 44.1 kHz / 16-bit / mono PCM.
- Generated once via a one-off C# tone-synthesis script; committed to repo.
- `EyeRest.UI/EyeRest.UI.csproj`: `<AvaloniaResource Include="Assets\Sounds\*.wav" />`
- Resolved at first play via `AssetLoader.Open(avares://…)`, extracted to a deterministic temp path (`<temp>/EyeRest/sounds/<filename>.wav`), cached for the process lifetime.

### 3.6 URL opener (`EyeRest.Core/Services/DefaultUrlOpener.cs`)

```csharp
public interface IUrlOpener { void Open(string url); }

public sealed class DefaultUrlOpener : IUrlOpener
{
    public void Open(string url)
    {
        using var _ = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
```

Registered as a singleton in DI. URL validation happens at config-save time in the UI (regex `^https?://`), not at play time.

---

## 4. Migration

`ConfigurationService.LoadAsync` runs migration on legacy configs (detected by `Meta.SchemaVersion < 2` or `Meta` absent):

1. For each timer + start/end combo:
   - `StartSoundEnabled == true` → `StartAudio.Source = Default`
   - `StartSoundEnabled == false` → `StartAudio.Source = Off`
2. If `AudioSettings.CustomSoundPath` is non-null:
   - The legacy `CustomSoundPath` was a single global field shared by all enabled channels. Migration copies it into `CustomFilePath` on **every channel** whose legacy bool was `true` (i.e. the same path may land on multiple channels) but **leaves `Source = Default`** on each.
   - User must explicitly switch to `File` source via UI to activate — preserves intent, prevents silent change.
3. Set `Meta.SchemaVersion = 2`, persist immediately.

Migration is idempotent — re-running on a v2 config is a no-op.

Stale-binary protection: when `ConfigurationService` loads a config with `Meta.SchemaVersion > CurrentSchemaVersion`, it logs a high-severity warning and refuses to save (prevents the LaunchAgent-old-binary corruption pattern from project lessons-learned).

---

## 5. Settings UI

In `MainWindow.axaml`, between the existing "Audio" card (global toggle + volume, lines ~1124–1179) and the per-timer settings cards:

```
┌────────────────────────────────────────────────┐
│  Popup Audio                                   │
├────────────────────────────────────────────────┤
│  Eye-rest Start                                │
│  Source: ( • Default  ○ File  ○ URL  ○ Off )  │
│  File:  [path…              ] [Browse] [▶︎]   │
│  URL:   [https://…          ]         [↗]    │
├────────────────────────────────────────────────┤
│  Eye-rest End            (same layout)         │
│  Break Start             (same layout)         │
│  Break End               (same layout)         │
└────────────────────────────────────────────────┘
```

- File and URL rows are always visible; inactive ones rendered at 50 % opacity but editable (preserves retention behavior).
- Test button (▶︎) on the File row plays the channel through `PlayChannelAsync` with the current config.
- ↗ button on the URL row opens the URL immediately (test).
- ViewModel: 12 new properties per channel (Source enum + FilePath + Url) × 4 channels + 8 commands.
- Initial implementation: 4 inline AXAML card blocks (DRY follow-up tracked as separate backlog item).

---

## 6. Integration points

`AvaloniaNotificationService` is where the four audio events become tangible:

| Event | Hook (existing line) | Audio call |
|-------|----------------------|-----------|
| Eye-rest popup shown | `ShowEyeRestReminderInternalAsync` ~line 138 | `_audio.PlayChannelAsync(EyeRestStart, _config.EyeRest.StartAudio)` |
| Eye-rest popup closed | `myPopup.Closed` handler ~line 147 | `_audio.PlayChannelAsync(EyeRestEnd, _config.EyeRest.EndAudio)` |
| Break popup shown | `ShowBreakReminderInternalAsync` ~line 329 | `_audio.PlayChannelAsync(BreakStart, _config.Break.StartAudio)` |
| Break popup closed | `myPopup.Closed` handler ~line 348 | `_audio.PlayChannelAsync(BreakEnd, _config.Break.EndAudio)` |

The legacy `PlayEyeRestStartSound`-style methods stay as backward-compatibility shims in case any other caller still uses them (none expected after `NotificationService` rewrite — caller audit happens in M5).

---

## 7. Risks & mitigations

| Risk | Severity | Mitigation |
|------|----------|-----------|
| Audio handle leak across rapid popup cycles | High | Per-call construct + `finally`-dispose + per-service `SemaphoreSlim` serialization |
| `AVAudioPlayer` thread affinity issues on macOS | Medium | Audio runs on `Task.Run`-scheduled thread, never blocks UI thread |
| Stale binary writes legacy schema over new schema | High | `Meta.SchemaVersion` sentinel with refuse-to-save guard |
| Migration corrupts user's existing custom sound | High | Migration sets `CustomFilePath` but leaves `Source = Default` — explicit user action required to activate |
| `Process.Start(url, UseShellExecute = true)` opens unexpected handler on macOS | Low | OS-level concern; if user has YouTube native app set as `https://youtube.com` handler, it opens there — expected |
| Temp-file cache for bundled WAVs grows unbounded | Low | Deterministic filename (same content → same temp file); 4 fixed files max |
| Audio plays after orchestrator shutdown | Medium | `CancellationToken` propagated from orchestrator's shutdown token; cancel on dispose |

---

## 8. Testing

### Unit tests
- `AppConfiguration` migration: 8 cases (cross-product of legacy `StartSoundEnabled` true/false × `CustomSoundPath` set/null × 2 channels).
- `AudioChannelConfig` round-trip serialization.
- `PlayChannelAsync` source resolution: 4 cases (Off, Default, File, Url) for each platform service.
- Disposal: simulate cancel mid-playback; assert player disposed.

### Integration tests
- Migrate a synthetic legacy config file → assert new shape + `SchemaVersion = 2`.
- 100 back-to-back `PlayChannelAsync` invocations → assert no handle leak (use `Diagnostics.Process.HandleCount` delta tolerance).

### Manual tests
- All 4 channels × all 4 sources (16 combinations).
- Switch source from File → URL → File and confirm path retained.
- Stop app mid-playback; confirm no orphaned audio.

---

## 9. Milestones with Technical Architect reviews

Each milestone ends with a Technical Architect sub-agent review specifically scoped to **Performance**, **Memory Leak**, **Resource Lifecycle**. A milestone is not closed until its review passes.

| # | Milestone | Architect review focus |
|---|-----------|------------------------|
| **M1** | Config schema + migration (`AudioChannelSource`, `AudioChannelConfig`, settings classes, `ConfigurationService` migration, `Meta.SchemaVersion`, unit tests) | Migration idempotence, no extra config writes, sentinel correctness |
| **M2** | Audio service surface + WAV playback (`PlayChannelAsync`, Windows `SoundPlayer` extension, macOS `AVAudioPlayer` extension, `SemaphoreSlim`, cancellation, disposal) | Player handle disposal across success/error/cancel paths, no `_currentPlayer` field, semaphore safety on shutdown |
| **M3** | Asset generation + bundling + URL opener (4 WAVs, `<AvaloniaResource>` include, temp-file cache, `IUrlOpener` + DI) | Temp-file cache cleanup, `Process.Start` lifecycle (no zombies), cache init thread-safety |
| **M4** | Settings UI (4 card sections, ViewModel properties, commands, radio binding, file picker, Test buttons) | UI thread safety, viewmodel notification correctness, dispatcher misuse |
| **M5** | Integration wiring + spec-vs-implementation audit (`NotificationService` calls, end-to-end manual test, audit) | End-to-end resource lifecycle across 100 popup cycles, audio doesn't outlive orchestrator, shutdown cancels pending playback |

---

## 10. Open items / follow-ups

- Extract `AudioChannelEditor` UserControl from the 4 inline AXAML blocks (post-merge polish item).
- Per-channel volume (if user requests after using the feature).
- MP3/AAC format support contract (currently best-effort — works iff platform decoder available).
- Background-tab vs foreground-tab control for URL mode (currently delegated to OS default).
