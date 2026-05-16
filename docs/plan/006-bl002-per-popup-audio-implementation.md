# BL-002 Per-popup Audio + Custom Audio Sources — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refactor audio playback so each of four popup events (eye-rest start/end, break start/end) is independently configurable to play a bundled default WAV, a custom local file, open a URL in the browser, or be silent — with a single global on/off master toggle and retention of inactive alternates.

**Architecture:** Approach A from the spec — minimal-disruption extension. Extend `IAudioService` with channel-aware `PlayChannelAsync(channel, config, ct)`. Add a new `AudioChannelConfig` POCO (Source enum + retained file path + retained URL) onto `EyeRestSettings` and `BreakSettings`. Bundle four WAVs as `<AvaloniaResource>` and extract to a temp-file cache for native playback. New `IUrlOpener` for `Process.Start`-based browser launch. Migration is gated by a new `ConfigMetadata.SchemaVersion` sentinel.

**Tech Stack:** .NET 8, Avalonia UI, xUnit, FluentAssertions, Moq, `System.Media.SoundPlayer` (Windows), `AVAudioPlayer` via P/Invoke (macOS).

**Reference spec:** `docs/plan/005-bl002-per-popup-audio-design.md` — sections numbered S1–S10 are cross-referenced below as **[Spec §N]**.

**Milestone gating rule:** Each milestone ends with a *Technical Architect Review* task. The reviewer is a sub-agent specifically tasked with auditing the milestone diff for **Performance**, **Memory Leak**, and **Resource Lifecycle**. A milestone is not closed until its review passes; surfaced critical issues reopen the milestone.

---

## File Map (created / modified)

**Created:**
- `EyeRest.Abstractions/Models/AudioChannelConfig.cs` — new POCO + enum.
- `EyeRest.Abstractions/Services/IUrlOpener.cs` — new service interface.
- `EyeRest.Core/Services/DefaultUrlOpener.cs` — `Process.Start`-based implementation.
- `EyeRest.Core/Services/BundledSoundCache.cs` — temp-file cache for embedded WAVs.
- `EyeRest.UI/Assets/Sounds/eye-rest-start.wav` (and 3 siblings) — bundled defaults.
- `scripts/generate-default-sounds.csx` — one-off tone-synthesis script.
- `EyeRest.Tests/Audio/AudioChannelConfigTests.cs` — POCO + serialization tests.
- `EyeRest.Tests/Audio/ConfigurationMigrationTests.cs` — migration tests.
- `EyeRest.Tests/Audio/PlayChannelAsyncTests.cs` — service surface tests.
- `EyeRest.Tests/Audio/DefaultUrlOpenerTests.cs` — opener tests.
- `EyeRest.Tests/Audio/BundledSoundCacheTests.cs` — cache tests.

**Modified:**
- `EyeRest.Abstractions/Models/AppConfiguration.cs` — modify `EyeRestSettings`, `BreakSettings`, `AudioSettings`, `ConfigMetadata`.
- `EyeRest.Abstractions/Services/IAudioService.cs` — add `PlayChannelAsync` + `AudioChannel` enum.
- `EyeRest.Platform.Windows/Services/AudioService.cs` — implement `PlayChannelAsync`.
- `EyeRest.Platform.macOS/Services/MacOSAudioService.cs` — implement `PlayChannelAsync`.
- `EyeRest.Core/Services/ConfigurationService.cs` — migration logic in `LoadAsync`.
- `EyeRest.UI/Services/AvaloniaNotificationService.cs` — wire channel events.
- `EyeRest.UI/Views/MainWindow.axaml` — add "Popup Audio" card.
- `EyeRest.UI/ViewModels/MainWindowViewModel.cs` — 12 properties × 4 channels + 8 commands.
- `EyeRest.UI/EyeRest.UI.csproj` — `<AvaloniaResource>` include.
- `EyeRest.Core/Extensions/ServiceCollectionExtensions.cs` (or equivalent) — DI for `IUrlOpener` and `BundledSoundCache`.

---

## Milestone 1 — Config schema + migration

**Spec §3.1, §3.5 (Meta), §4.** Locks in the data model and the safe-migration path. All later work depends on these types.

### Task 1.1: Add `AudioChannelSource` enum and `AudioChannelConfig` POCO

**Files:**
- Create: `EyeRest.Abstractions/Models/AudioChannelConfig.cs`
- Create: `EyeRest.Tests/Audio/AudioChannelConfigTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// EyeRest.Tests/Audio/AudioChannelConfigTests.cs
using EyeRest.Abstractions.Models;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace EyeRest.Tests.Audio;

public class AudioChannelConfigTests
{
    [Fact]
    public void DefaultConstructor_HasDefaultSource_NullPathAndUrl()
    {
        var cfg = new AudioChannelConfig();
        cfg.Source.Should().Be(AudioChannelSource.Default);
        cfg.CustomFilePath.Should().BeNull();
        cfg.Url.Should().BeNull();
    }

    [Fact]
    public void Serializes_RoundTrip_PreservesAllFields()
    {
        var original = new AudioChannelConfig
        {
            Source = AudioChannelSource.File,
            CustomFilePath = "/tmp/x.wav",
            Url = "https://example.com",
        };
        var json = JsonSerializer.Serialize(original);
        var back = JsonSerializer.Deserialize<AudioChannelConfig>(json)!;
        back.Source.Should().Be(AudioChannelSource.File);
        back.CustomFilePath.Should().Be("/tmp/x.wav");
        back.Url.Should().Be("https://example.com");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test EyeRest.Tests/EyeRest.Tests.csproj --filter "FullyQualifiedName~AudioChannelConfigTests"
```
Expected: FAIL — `AudioChannelConfig` / `AudioChannelSource` do not exist.

- [ ] **Step 3: Create the types**

```csharp
// EyeRest.Abstractions/Models/AudioChannelConfig.cs
namespace EyeRest.Abstractions.Models;

public enum AudioChannelSource
{
    Off = 0,
    Default = 1,
    File = 2,
    Url = 3,
}

public class AudioChannelConfig
{
    public AudioChannelSource Source { get; set; } = AudioChannelSource.Default;
    public string? CustomFilePath { get; set; }
    public string? Url { get; set; }
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
dotnet test EyeRest.Tests/EyeRest.Tests.csproj --filter "FullyQualifiedName~AudioChannelConfigTests"
```
Expected: PASS — both tests green.

- [ ] **Step 5: Commit**

```bash
git add EyeRest.Abstractions/Models/AudioChannelConfig.cs EyeRest.Tests/Audio/AudioChannelConfigTests.cs
git commit -m "feat: add AudioChannelConfig and AudioChannelSource"
```

---

### Task 1.2: Add `SchemaVersion` to `ConfigMetadata`

**Files:**
- Modify: `EyeRest.Abstractions/Models/AppConfiguration.cs` (locate `ConfigMetadata` class)

- [ ] **Step 1: Add field**

Find the `ConfigMetadata` class definition and add the new property:

```csharp
public class ConfigMetadata
{
    // ... existing fields (SaveCount, LastSavedAt, AppVersion, ProcessId, ExecutablePath) ...

    /// <summary>
    /// Config schema version. 1 = pre-BL002 (legacy bool toggles + global CustomSoundPath).
    /// 2 = BL002 per-channel AudioChannelConfig. Increment on schema-breaking changes.
    /// </summary>
    public int SchemaVersion { get; set; } = 1; // default to legacy so older files migrate
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build EyeRest.Abstractions/EyeRest.Abstractions.csproj
```
Expected: BUILD SUCCESS.

- [ ] **Step 3: Commit**

```bash
git add EyeRest.Abstractions/Models/AppConfiguration.cs
git commit -m "feat: add SchemaVersion sentinel to ConfigMetadata"
```

---

### Task 1.3: Add `StartAudio`/`EndAudio` to `EyeRestSettings` and `BreakSettings`; remove legacy bools

**Files:**
- Modify: `EyeRest.Abstractions/Models/AppConfiguration.cs` (EyeRestSettings, BreakSettings, AudioSettings)

- [ ] **Step 1: Write the failing test**

Add to `EyeRest.Tests/Audio/AudioChannelConfigTests.cs`:

```csharp
[Fact]
public void EyeRestSettings_NewInstance_HasDefaultStartAudio_AndDefaultEndAudio()
{
    var s = new EyeRest.Abstractions.Models.EyeRestSettings();
    s.StartAudio.Should().NotBeNull();
    s.StartAudio.Source.Should().Be(AudioChannelSource.Default);
    s.EndAudio.Should().NotBeNull();
    s.EndAudio.Source.Should().Be(AudioChannelSource.Default);
}

[Fact]
public void BreakSettings_NewInstance_HasDefaultStartAudio_AndDefaultEndAudio()
{
    var s = new EyeRest.Abstractions.Models.BreakSettings();
    s.StartAudio.Should().NotBeNull();
    s.EndAudio.Should().NotBeNull();
}
```

- [ ] **Step 2: Run to verify failure**

```bash
dotnet test EyeRest.Tests/EyeRest.Tests.csproj --filter "FullyQualifiedName~AudioChannelConfigTests"
```
Expected: FAIL — `StartAudio`/`EndAudio` do not exist on settings classes.

- [ ] **Step 3: Modify settings classes**

In `EyeRest.Abstractions/Models/AppConfiguration.cs`:

- Remove from `EyeRestSettings`: `public bool StartSoundEnabled { get; set; }`, `public bool EndSoundEnabled { get; set; }`.
- Remove from `BreakSettings`: same two bools.
- Remove from `AudioSettings`: `public string? CustomSoundPath { get; set; }`.
- Add to `EyeRestSettings`:

```csharp
public AudioChannelConfig StartAudio { get; set; } = new();
public AudioChannelConfig EndAudio   { get; set; } = new();
```

- Add the same two properties to `BreakSettings`.

- [ ] **Step 4: Run to verify pass + verify build**

```bash
dotnet build EyeRest.sln
dotnet test EyeRest.Tests/EyeRest.Tests.csproj --filter "FullyQualifiedName~AudioChannelConfigTests"
```
Expected: BUILD SUCCESS (callers of removed fields will FAIL — that's intentional, addressed in Task 1.6 callers fixup). PASS for the new tests.

Note: do NOT fix caller compile errors here yet — Task 1.6 will rewire them.

- [ ] **Step 5: Commit**

```bash
git add EyeRest.Abstractions/Models/AppConfiguration.cs EyeRest.Tests/Audio/AudioChannelConfigTests.cs
git commit -m "feat: add StartAudio/EndAudio to settings, remove legacy bool toggles"
```

---

### Task 1.4: Identify and fix callers of removed legacy fields

**Files:**
- Modify: any file that references `StartSoundEnabled`, `EndSoundEnabled`, `CustomSoundPath`.

- [ ] **Step 1: Find callers**

```bash
grep -rn "StartSoundEnabled\|EndSoundEnabled\|CustomSoundPath" \
  --include="*.cs" --include="*.axaml" \
  EyeRest.Abstractions EyeRest.Core EyeRest.Platform.Windows EyeRest.Platform.macOS EyeRest.UI EyeRest.Tests
```

Expected output: a list of files. Confirm none are in `AppConfiguration.cs` (already removed).

- [ ] **Step 2: For each caller, replace usage**

Rules (apply per file):
- `cfg.EyeRest.StartSoundEnabled` (read)   → `cfg.EyeRest.StartAudio.Source != AudioChannelSource.Off`
- `cfg.EyeRest.StartSoundEnabled = true`    → `cfg.EyeRest.StartAudio.Source = AudioChannelSource.Default`
- `cfg.EyeRest.StartSoundEnabled = false`   → `cfg.EyeRest.StartAudio.Source = AudioChannelSource.Off`
- `cfg.Audio.CustomSoundPath` (read)        → consult Task 2.2 routing (the property no longer exists; callers that played a custom sound must be migrated to `PlayChannelAsync`). If the call site is in `AudioService.PlayCustomSoundTestAsync(string filePath)`, no change — it takes the path as parameter.
- AXAML bindings to `EyeRestStartSoundEnabled` etc. — note them but do NOT fix yet; Task 4.x rewrites the UI section that contains them.

For now (M1), in any non-UI caller that compiled against the legacy bools and only checks/sets them as on/off flags, do the mechanical translation above. If a caller actively uses `AudioSettings.CustomSoundPath` as a runtime value, leave a TEMPORARY local variable `string? legacyCustomSoundPath = null;` so the file compiles. (Will be removed in M5 caller audit.)

- [ ] **Step 3: Verify build**

```bash
dotnet build EyeRest.sln
```
Expected: BUILD SUCCESS. AXAML compile errors against ViewModel properties are acceptable iff they relate only to UI bindings that M4 will replace; otherwise fix.

If AXAML emits unresolved binding warnings against `EyeRestStartSoundEnabled` etc., add temporary `EyeRestStartSoundEnabled => StartAudio.Source != AudioChannelSource.Off` shims on the ViewModel — these will be deleted in M4.

- [ ] **Step 4: Commit**

```bash
git add -u
git commit -m "fix: migrate legacy bool/path callers to AudioChannelConfig"
```

---

### Task 1.5: Write the migration in `ConfigurationService.LoadAsync`

**Files:**
- Modify: `EyeRest.Core/Services/ConfigurationService.cs`
- Create: `EyeRest.Tests/Audio/ConfigurationMigrationTests.cs`

- [ ] **Step 1: Write the failing migration tests**

```csharp
// EyeRest.Tests/Audio/ConfigurationMigrationTests.cs
using EyeRest.Abstractions.Models;
using FluentAssertions;
using Xunit;

namespace EyeRest.Tests.Audio;

public class ConfigurationMigrationTests
{
    [Fact]
    public void Migrate_LegacyTrueBools_MapTo_DefaultSource()
    {
        var legacy = new AppConfiguration
        {
            Meta = new ConfigMetadata { SchemaVersion = 1 },
            // The legacy bool fields no longer exist on the in-memory model.
            // We simulate the legacy-JSON shape by writing a JSON string with
            // legacy keys and round-tripping through the migration entry point.
        };
        // See helper in ConfigurationService: ApplyLegacyToV2Migration(jsonDoc) -> AppConfiguration
        var legacyJson = """
        {
          "Meta": { "SchemaVersion": 1 },
          "EyeRest": { "StartSoundEnabled": true, "EndSoundEnabled": false },
          "Break":   { "StartSoundEnabled": true, "EndSoundEnabled": true  },
          "Audio":   { "Enabled": true, "Volume": 50, "CustomSoundPath": "/tmp/my.wav" }
        }
        """;
        var cfg = ConfigurationMigrator.MigrateFromJson(legacyJson);

        cfg.Meta!.SchemaVersion.Should().Be(2);
        cfg.EyeRest.StartAudio.Source.Should().Be(AudioChannelSource.Default);
        cfg.EyeRest.EndAudio.Source.Should().Be(AudioChannelSource.Off);
        cfg.Break.StartAudio.Source.Should().Be(AudioChannelSource.Default);
        cfg.Break.EndAudio.Source.Should().Be(AudioChannelSource.Default);

        // CustomSoundPath copied to all channels where the legacy bool was true,
        // but Source remains Default — user must explicitly switch.
        cfg.EyeRest.StartAudio.CustomFilePath.Should().Be("/tmp/my.wav");
        cfg.EyeRest.EndAudio.CustomFilePath.Should().BeNull();
        cfg.Break.StartAudio.CustomFilePath.Should().Be("/tmp/my.wav");
        cfg.Break.EndAudio.CustomFilePath.Should().Be("/tmp/my.wav");
    }

    [Fact]
    public void Migrate_AlreadyV2_IsIdempotent()
    {
        var v2Json = """
        {
          "Meta": { "SchemaVersion": 2 },
          "EyeRest": { "StartAudio": { "Source": "File", "CustomFilePath": "/x.wav" }, "EndAudio": { "Source": "Default" } },
          "Break":   { "StartAudio": { "Source": "Default" }, "EndAudio": { "Source": "Url", "Url": "https://example.com" } },
          "Audio":   { "Enabled": true, "Volume": 50 }
        }
        """;
        var cfg = ConfigurationMigrator.MigrateFromJson(v2Json);
        cfg.Meta!.SchemaVersion.Should().Be(2);
        cfg.EyeRest.StartAudio.Source.Should().Be(AudioChannelSource.File);
        cfg.EyeRest.StartAudio.CustomFilePath.Should().Be("/x.wav");
        cfg.Break.EndAudio.Source.Should().Be(AudioChannelSource.Url);
        cfg.Break.EndAudio.Url.Should().Be("https://example.com");
    }

    [Fact]
    public void Migrate_NewerSchemaVersion_Throws()
    {
        var futureJson = """{ "Meta": { "SchemaVersion": 99 } }""";
        var act = () => ConfigurationMigrator.MigrateFromJson(futureJson);
        act.Should().Throw<InvalidOperationException>().WithMessage("*SchemaVersion*99*");
    }
}
```

- [ ] **Step 2: Run to verify failure**

```bash
dotnet test EyeRest.Tests/EyeRest.Tests.csproj --filter "FullyQualifiedName~ConfigurationMigrationTests"
```
Expected: FAIL — `ConfigurationMigrator` does not exist.

- [ ] **Step 3: Implement `ConfigurationMigrator`**

Create `EyeRest.Core/Services/ConfigurationMigrator.cs`:

```csharp
using System.Text.Json;
using EyeRest.Abstractions.Models;

namespace EyeRest.Core.Services;

public static class ConfigurationMigrator
{
    public const int CurrentSchemaVersion = 2;

    public static AppConfiguration MigrateFromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        int version = 1;
        if (root.TryGetProperty("Meta", out var meta)
            && meta.TryGetProperty("SchemaVersion", out var v))
        {
            version = v.GetInt32();
        }

        if (version > CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Config SchemaVersion={version} is newer than supported version "
                + $"{CurrentSchemaVersion}. A newer EyeRest binary may have written this file.");
        }

        // Deserialize into a permissive shape that tolerates both legacy and v2 keys.
        var cfg = JsonSerializer.Deserialize<AppConfiguration>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new AppConfiguration();

        if (version < 2)
        {
            ApplyV1ToV2(cfg, root);
        }

        cfg.Meta ??= new ConfigMetadata();
        cfg.Meta.SchemaVersion = CurrentSchemaVersion;
        return cfg;
    }

    private static void ApplyV1ToV2(AppConfiguration cfg, JsonElement root)
    {
        var eyeRest = root.TryGetProperty("EyeRest", out var er) ? er : default;
        var brk     = root.TryGetProperty("Break",   out var bk) ? bk : default;
        var audio   = root.TryGetProperty("Audio",   out var au) ? au : default;

        bool erStart = ReadBool(eyeRest, "StartSoundEnabled", true);
        bool erEnd   = ReadBool(eyeRest, "EndSoundEnabled",   true);
        bool bkStart = ReadBool(brk,     "StartSoundEnabled", true);
        bool bkEnd   = ReadBool(brk,     "EndSoundEnabled",   true);
        string? legacyCustomPath = ReadString(audio, "CustomSoundPath");

        cfg.EyeRest.StartAudio = ToChannel(erStart, legacyCustomPath);
        cfg.EyeRest.EndAudio   = ToChannel(erEnd,   legacyCustomPath);
        cfg.Break.StartAudio   = ToChannel(bkStart, legacyCustomPath);
        cfg.Break.EndAudio     = ToChannel(bkEnd,   legacyCustomPath);
    }

    private static AudioChannelConfig ToChannel(bool legacyEnabled, string? legacyCustomPath)
    {
        var c = new AudioChannelConfig
        {
            Source = legacyEnabled ? AudioChannelSource.Default : AudioChannelSource.Off,
        };
        if (legacyEnabled && !string.IsNullOrWhiteSpace(legacyCustomPath))
        {
            c.CustomFilePath = legacyCustomPath;
            // Source remains Default — user must promote to File via UI.
        }
        return c;
    }

    private static bool ReadBool(JsonElement node, string name, bool fallback)
    {
        if (node.ValueKind == JsonValueKind.Undefined) return fallback;
        return node.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True
            || (v.ValueKind == JsonValueKind.False ? false : fallback);
    }

    private static string? ReadString(JsonElement node, string name)
    {
        if (node.ValueKind == JsonValueKind.Undefined) return null;
        return node.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
    }
}
```

- [ ] **Step 4: Wire the migrator into `ConfigurationService.LoadAsync`**

In `EyeRest.Core/Services/ConfigurationService.cs`, locate the file-read + `JsonSerializer.Deserialize` block in `LoadAsync`. Replace the deserialize call with:

```csharp
var json = await File.ReadAllTextAsync(_configPath).ConfigureAwait(false);
var config = ConfigurationMigrator.MigrateFromJson(json);
// If migration changed the shape (legacy detected), persist immediately to lock the new schema.
if (config.Meta?.SchemaVersion == ConfigurationMigrator.CurrentSchemaVersion
    && !json.Contains("\"SchemaVersion\""))
{
    await SaveAsync(config).ConfigureAwait(false);
}
return config;
```

Catch `InvalidOperationException` from the migrator at the call site and log + rethrow (do not silently fall back — stale-binary corruption protection is the whole point).

- [ ] **Step 5: Run tests + integration smoke**

```bash
dotnet test EyeRest.Tests/EyeRest.Tests.csproj --filter "FullyQualifiedName~ConfigurationMigrationTests"
dotnet build EyeRest.sln
```
Expected: PASS on all three tests; clean build.

- [ ] **Step 6: Commit**

```bash
git add EyeRest.Core/Services/ConfigurationMigrator.cs EyeRest.Core/Services/ConfigurationService.cs EyeRest.Tests/Audio/ConfigurationMigrationTests.cs
git commit -m "feat: migrate legacy audio config to schema v2 with SchemaVersion sentinel"
```

---

### Task 1.6: M1 Technical Architect Review

- [ ] **Step 1: Dispatch Technical Architect sub-agent**

Use the `Agent` tool with `subagent_type: claude` and the following prompt (do not invoke until M1 tasks 1.1–1.5 are committed):

> You are the **Technical Architect** reviewing **Milestone 1 (Config schema + migration)** of BL-002 in the Eye-rest project. Diff to review: commits since the M0 baseline (use `git log --oneline -10` and `git diff` to identify M1 commits — they touch `EyeRest.Abstractions/Models/AppConfiguration.cs`, `EyeRest.Abstractions/Models/AudioChannelConfig.cs`, `EyeRest.Core/Services/ConfigurationMigrator.cs`, `EyeRest.Core/Services/ConfigurationService.cs`, and `EyeRest.Tests/Audio/*`).
>
> Spec reference: `docs/plan/005-bl002-per-popup-audio-design.md`, sections §3.1, §3.5, §4, §7.
>
> Review **only** these three risk axes (do NOT comment on style, naming, etc. unless they directly cause one of these risks):
>
> 1. **Performance** — Is the migration O(n) on config size? Does it cause an extra disk write on every load, or only on legacy detection? Does `JsonDocument.Parse` get disposed?
> 2. **Memory Leak** — Are any `IDisposable` instances created and not disposed (`JsonDocument`, `FileStream`)? Does the migrator capture `JsonElement` references that outlive their `JsonDocument`?
> 3. **Resource Lifecycle** — Is the SchemaVersion refuse-to-save logic correct (does a v=99 future-config refuse to overwrite)? Is migration idempotent (re-running on already-v2 input produces no extra write)? Could a race between two concurrent processes (per the Mar 2026 lessons-learned) interleave a migration with a save?
>
> Output: a numbered list of findings with severity (Critical/Major/Minor) and file:line citations. If no critical issues found, state that explicitly. Keep under 500 words.

- [ ] **Step 2: Address Critical findings**

If the architect reports any **Critical** findings, fix them in additional commits before closing M1. Re-run tests after each fix.

- [ ] **Step 3: Mark M1 complete in plan**

Update this plan document: replace `- [ ]` with `- [x]` on all M1 tasks.

---

## Milestone 2 — Audio service surface + WAV playback

**Spec §3.2, §3.3, §3.4.** Adds the channel-aware playback entry point and the platform-specific WAV playback implementations.

### Task 2.1: Add `AudioChannel` enum + `PlayChannelAsync` to `IAudioService`

**Files:**
- Modify: `EyeRest.Abstractions/Services/IAudioService.cs`

- [ ] **Step 1: Modify interface**

```csharp
using System.Threading;
using System.Threading.Tasks;
using EyeRest.Abstractions.Models;

namespace EyeRest.Abstractions.Services;

public enum AudioChannel
{
    EyeRestStart,
    EyeRestEnd,
    BreakStart,
    BreakEnd,
    BreakWarning,
}

public interface IAudioService
{
    bool IsAudioEnabled { get; }

    // NEW: channel-aware entry point.
    Task PlayChannelAsync(
        AudioChannel channel,
        AudioChannelConfig config,
        CancellationToken cancellationToken = default);

    // Existing methods preserved as adapter overloads:
    Task PlayEyeRestStartSound();
    Task PlayEyeRestEndSound();
    Task PlayBreakStartSound();
    Task PlayBreakEndSound();
    Task PlayBreakWarningSound();
    Task PlayCustomSoundTestAsync(string filePath);
    Task TestEyeRestAudioAsync();
}
```

- [ ] **Step 2: Verify build (will fail in platform projects)**

```bash
dotnet build EyeRest.sln
```
Expected: FAIL in `EyeRest.Platform.Windows` and `EyeRest.Platform.macOS` — both must implement the new method. Acceptable; fixed in 2.2 and 2.4.

- [ ] **Step 3: Commit**

```bash
git add EyeRest.Abstractions/Services/IAudioService.cs
git commit -m "feat: add IAudioService.PlayChannelAsync and AudioChannel enum"
```

---

### Task 2.2: Implement `PlayChannelAsync` source-resolution skeleton (shared via abstract base)

**Files:**
- Create: `EyeRest.Core/Services/AudioServiceBase.cs`
- Create: `EyeRest.Tests/Audio/PlayChannelAsyncTests.cs`

We introduce a small shared base class to keep source-resolution logic DRY across platforms. Platform-specific subclasses implement only the actual playback primitive.

- [ ] **Step 1: Write the failing tests**

```csharp
// EyeRest.Tests/Audio/PlayChannelAsyncTests.cs
using EyeRest.Abstractions.Models;
using EyeRest.Abstractions.Services;
using EyeRest.Core.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace EyeRest.Tests.Audio;

public class PlayChannelAsyncTests
{
    private static FakeAudioService NewService(Mock<IUrlOpener>? urlOpener = null)
        => new(urlOpener?.Object ?? Mock.Of<IUrlOpener>());

    [Fact]
    public async Task Source_Off_DoesNothing()
    {
        var s = NewService();
        await s.PlayChannelAsync(AudioChannel.EyeRestStart,
            new AudioChannelConfig { Source = AudioChannelSource.Off });
        s.DefaultPlays.Should().Be(0);
        s.FilePlays.Should().BeEmpty();
    }

    [Fact]
    public async Task Source_Default_CallsPlatformDefaultPlay()
    {
        var s = NewService();
        await s.PlayChannelAsync(AudioChannel.BreakStart,
            new AudioChannelConfig { Source = AudioChannelSource.Default });
        s.DefaultPlays.Should().Be(1);
        s.LastChannel.Should().Be(AudioChannel.BreakStart);
    }

    [Fact]
    public async Task Source_File_WithExistingFile_CallsPlatformFilePlay()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            var s = NewService();
            await s.PlayChannelAsync(AudioChannel.EyeRestEnd,
                new AudioChannelConfig { Source = AudioChannelSource.File, CustomFilePath = tmp });
            s.FilePlays.Should().ContainSingle().Which.Should().Be(tmp);
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public async Task Source_File_WithMissingFile_FallsBackToDefault()
    {
        var s = NewService();
        await s.PlayChannelAsync(AudioChannel.EyeRestStart,
            new AudioChannelConfig { Source = AudioChannelSource.File, CustomFilePath = "/nope/missing.wav" });
        s.DefaultPlays.Should().Be(1);
        s.FilePlays.Should().BeEmpty();
    }

    [Fact]
    public async Task Source_Url_CallsUrlOpener_DoesNotPlayAudio()
    {
        var opener = new Mock<IUrlOpener>();
        var s = NewService(opener);
        await s.PlayChannelAsync(AudioChannel.BreakStart,
            new AudioChannelConfig { Source = AudioChannelSource.Url, Url = "https://example.com" });
        opener.Verify(o => o.Open("https://example.com"), Times.Once);
        s.DefaultPlays.Should().Be(0);
        s.FilePlays.Should().BeEmpty();
    }

    [Fact]
    public async Task GlobalAudioDisabled_SkipsAudio_StillOpensUrl()
    {
        var opener = new Mock<IUrlOpener>();
        var s = NewService(opener);
        s.SetGlobalAudioEnabled(false);

        await s.PlayChannelAsync(AudioChannel.BreakStart,
            new AudioChannelConfig { Source = AudioChannelSource.Default });
        s.DefaultPlays.Should().Be(0);

        await s.PlayChannelAsync(AudioChannel.BreakStart,
            new AudioChannelConfig { Source = AudioChannelSource.Url, Url = "https://x" });
        opener.Verify(o => o.Open("https://x"), Times.Once);
    }
}

internal class FakeAudioService : AudioServiceBase
{
    public int DefaultPlays { get; private set; }
    public List<string> FilePlays { get; } = new();
    public AudioChannel LastChannel { get; private set; }
    private bool _globalEnabled = true;

    public FakeAudioService(IUrlOpener urlOpener) : base(urlOpener) { }

    public void SetGlobalAudioEnabled(bool enabled) => _globalEnabled = enabled;
    public override bool IsAudioEnabled => _globalEnabled;

    protected override Task PlayDefaultAsync(AudioChannel channel, CancellationToken ct)
    {
        DefaultPlays++;
        LastChannel = channel;
        return Task.CompletedTask;
    }

    protected override Task PlayFileAsync(string filePath, CancellationToken ct)
    {
        FilePlays.Add(filePath);
        return Task.CompletedTask;
    }

    // Adapter methods (no-ops in this fake — covered in adapter tests).
    public override Task PlayEyeRestStartSound() => Task.CompletedTask;
    public override Task PlayEyeRestEndSound()   => Task.CompletedTask;
    public override Task PlayBreakStartSound()   => Task.CompletedTask;
    public override Task PlayBreakEndSound()     => Task.CompletedTask;
    public override Task PlayBreakWarningSound() => Task.CompletedTask;
    public override Task PlayCustomSoundTestAsync(string filePath) => Task.CompletedTask;
    public override Task TestEyeRestAudioAsync() => Task.CompletedTask;
}
```

- [ ] **Step 2: Run to verify failure**

```bash
dotnet test EyeRest.Tests/EyeRest.Tests.csproj --filter "FullyQualifiedName~PlayChannelAsyncTests"
```
Expected: FAIL — `AudioServiceBase`, `IUrlOpener` do not exist.

- [ ] **Step 3: Create `IUrlOpener` interface (stub for tests; real implementation in M3)**

```csharp
// EyeRest.Abstractions/Services/IUrlOpener.cs
namespace EyeRest.Abstractions.Services;

public interface IUrlOpener
{
    void Open(string url);
}
```

- [ ] **Step 4: Create `AudioServiceBase`**

```csharp
// EyeRest.Core/Services/AudioServiceBase.cs
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EyeRest.Abstractions.Models;
using EyeRest.Abstractions.Services;

namespace EyeRest.Core.Services;

public abstract class AudioServiceBase : IAudioService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IUrlOpener _urlOpener;

    protected AudioServiceBase(IUrlOpener urlOpener)
    {
        _urlOpener = urlOpener ?? throw new ArgumentNullException(nameof(urlOpener));
    }

    public abstract bool IsAudioEnabled { get; }

    public async Task PlayChannelAsync(
        AudioChannel channel,
        AudioChannelConfig config,
        CancellationToken cancellationToken = default)
    {
        if (config is null) return;

        switch (config.Source)
        {
            case AudioChannelSource.Off:
                return;

            case AudioChannelSource.Url:
                if (!string.IsNullOrWhiteSpace(config.Url))
                    _urlOpener.Open(config.Url);
                return;

            case AudioChannelSource.File:
                if (!IsAudioEnabled) return;
                if (!string.IsNullOrWhiteSpace(config.CustomFilePath)
                    && File.Exists(config.CustomFilePath))
                {
                    await GatedAsync(() => PlayFileAsync(config.CustomFilePath, cancellationToken),
                        cancellationToken).ConfigureAwait(false);
                    return;
                }
                // Fall through to Default on missing file.
                goto case AudioChannelSource.Default;

            case AudioChannelSource.Default:
                if (!IsAudioEnabled) return;
                await GatedAsync(() => PlayDefaultAsync(channel, cancellationToken),
                    cancellationToken).ConfigureAwait(false);
                return;
        }
    }

    private async Task GatedAsync(Func<Task> work, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try { await work().ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    protected abstract Task PlayDefaultAsync(AudioChannel channel, CancellationToken ct);
    protected abstract Task PlayFileAsync(string filePath, CancellationToken ct);

    public abstract Task PlayEyeRestStartSound();
    public abstract Task PlayEyeRestEndSound();
    public abstract Task PlayBreakStartSound();
    public abstract Task PlayBreakEndSound();
    public abstract Task PlayBreakWarningSound();
    public abstract Task PlayCustomSoundTestAsync(string filePath);
    public abstract Task TestEyeRestAudioAsync();
}
```

- [ ] **Step 5: Run to verify pass**

```bash
dotnet test EyeRest.Tests/EyeRest.Tests.csproj --filter "FullyQualifiedName~PlayChannelAsyncTests"
```
Expected: PASS on all six tests.

- [ ] **Step 6: Commit**

```bash
git add EyeRest.Abstractions/Services/IUrlOpener.cs EyeRest.Core/Services/AudioServiceBase.cs EyeRest.Tests/Audio/PlayChannelAsyncTests.cs
git commit -m "feat: add AudioServiceBase with channel-aware source resolution"
```

---

### Task 2.3: Refactor Windows `AudioService` to extend `AudioServiceBase`

**Files:**
- Modify: `EyeRest.Platform.Windows/Services/AudioService.cs`
- Modify: `EyeRest.Platform.Windows/Extensions/WindowsServiceCollectionExtensions.cs` (or DI registration file)

- [ ] **Step 1: Refactor class to inherit `AudioServiceBase`**

Change the class declaration from `: IAudioService` to `: AudioServiceBase`. Remove the per-method explicit `IAudioService` implementation; convert each existing public method into `public override` of the abstract members on `AudioServiceBase`. Add a constructor that takes `IUrlOpener` and the existing `IConfigurationService` / logger dependencies, calling `: base(urlOpener)`.

Inside `PlayDefaultAsync(channel, ct)`, branch on `channel`:
- `EyeRestStart` → existing eye-rest start playback (`SystemSounds.Beep.Play()` and fallback cascade).
- `EyeRestEnd`   → existing eye-rest end playback.
- `BreakStart`   → existing break start.
- `BreakEnd`     → existing break end.
- `BreakWarning` → existing break warning.

Wrap each in `await Task.Run(...)` so cancellation can interrupt. Use a `try`/`finally` around any `SoundPlayer` instance to call `Dispose()`. Example for one channel:

```csharp
protected override async Task PlayDefaultAsync(AudioChannel channel, CancellationToken ct)
{
    await Task.Run(() =>
    {
        ct.ThrowIfCancellationRequested();
        // Map channel → existing platform sound (preserve current fallback cascade).
        switch (channel)
        {
            case AudioChannel.EyeRestStart: PlayEyeRestStartSync(); break;
            case AudioChannel.EyeRestEnd:   PlayEyeRestEndSync();   break;
            case AudioChannel.BreakStart:   PlayBreakStartSync();   break;
            case AudioChannel.BreakEnd:     PlayBreakEndSync();     break;
            case AudioChannel.BreakWarning: PlayBreakWarningSync(); break;
        }
    }, ct).ConfigureAwait(false);
}
```

The `PlayXxxSync()` helpers retain the existing 5-step fallback cascade.

`PlayFileAsync` uses `SoundPlayer`:

```csharp
protected override async Task PlayFileAsync(string filePath, CancellationToken ct)
{
    await Task.Run(() =>
    {
        ct.ThrowIfCancellationRequested();
        SoundPlayer? player = null;
        try
        {
            player = new SoundPlayer(filePath);
            player.Load();
            player.PlaySync(); // blocks until done
        }
        finally
        {
            player?.Dispose();
        }
    }, ct).ConfigureAwait(false);
}
```

- [ ] **Step 2: Update DI registration**

Locate `WindowsServiceCollectionExtensions.AddWindowsPlatformServices` (or equivalent). Ensure `IUrlOpener` is registered before `IAudioService` (M3 will add the concrete implementation; for now, register a stub `services.AddSingleton<IUrlOpener, DefaultUrlOpener>()`. If `DefaultUrlOpener` doesn't exist yet, add a temporary stub `class StubUrlOpener : IUrlOpener { public void Open(string url) {} }` inside the platform project — to be removed in M3.

- [ ] **Step 3: Verify build + run all tests**

```bash
dotnet build EyeRest.sln
dotnet test EyeRest.Tests/EyeRest.Tests.csproj --filter "Category=Unit"
```
Expected: BUILD SUCCESS; all unit tests PASS.

- [ ] **Step 4: Commit**

```bash
git add EyeRest.Platform.Windows
git commit -m "refactor: Windows AudioService extends AudioServiceBase with PlayChannelAsync"
```

---

### Task 2.4: Refactor macOS `MacOSAudioService` to extend `AudioServiceBase`

**Files:**
- Modify: `EyeRest.Platform.macOS/Services/MacOSAudioService.cs`
- Modify: `EyeRest.Platform.macOS/Extensions/MacOSServiceCollectionExtensions.cs` (or DI registration file)

- [ ] **Step 1: Refactor class to inherit `AudioServiceBase`**

Same shape as Task 2.3 but using NSSound for defaults and `AVAudioPlayer` (via existing P/Invoke surface; if not present, use `NSSound.SoundNamed` with a temp-file URL).

```csharp
protected override async Task PlayFileAsync(string filePath, CancellationToken ct)
{
    await Task.Run(() =>
    {
        ct.ThrowIfCancellationRequested();
        // NSSound with file URL — simplest cross-API path on macOS.
        // If AVAudioPlayer P/Invoke is available, prefer that for better format support.
        using var sound = NSSoundLoader.Load(filePath);
        sound?.Play();
        // NSSound.Play is async; wait for completion via CompletionHandler or polling.
        WaitForNSSoundCompletion(sound, ct);
    }, ct).ConfigureAwait(false);
}
```

If the project already has an `AVAudioPlayer` P/Invoke wrapper, use it instead and capture the completion callback to signal a `TaskCompletionSource` (more accurate than NSSound). Either way, ensure the player is disposed in `finally`.

- [ ] **Step 2: Update DI registration**

Locate `MacOSServiceCollectionExtensions.AddMacOSPlatformServices` (or equivalent). Ensure `IUrlOpener` is registered before `IAudioService` registration. If the concrete `DefaultUrlOpener` doesn't exist yet (added in M3), register a temporary stub:

```csharp
// Temporary — replaced in M3 Task 3.4
class StubUrlOpener : IUrlOpener { public void Open(string url) { } }
services.AddSingleton<IUrlOpener, StubUrlOpener>();
```

This stub will be removed in M3 Task 3.4 Step 4.

- [ ] **Step 3: Verify build + run all tests**

```bash
dotnet build EyeRest.sln
dotnet test EyeRest.Tests/EyeRest.Tests.csproj --filter "Category=Unit"
```
Expected: BUILD SUCCESS; all unit tests PASS.

- [ ] **Step 4: Commit**

```bash
git add EyeRest.Platform.macOS
git commit -m "refactor: macOS AudioService extends AudioServiceBase with PlayChannelAsync"
```

---

### Task 2.5: Concurrency + cancellation tests (integration)

**Files:**
- Create: `EyeRest.Tests/Audio/AudioServiceConcurrencyTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// EyeRest.Tests/Audio/AudioServiceConcurrencyTests.cs
using System.Diagnostics;
using EyeRest.Abstractions.Models;
using EyeRest.Abstractions.Services;
using FluentAssertions;
using Xunit;

namespace EyeRest.Tests.Audio;

public class AudioServiceConcurrencyTests
{
    [Fact(Timeout = 5000)]
    public async Task BackToBackCalls_AreSerializedByGate()
    {
        var s = new SlowFakeAudioService(playDurationMs: 50);
        var sw = Stopwatch.StartNew();
        await Task.WhenAll(
            s.PlayChannelAsync(AudioChannel.BreakStart, new AudioChannelConfig { Source = AudioChannelSource.Default }),
            s.PlayChannelAsync(AudioChannel.BreakEnd,   new AudioChannelConfig { Source = AudioChannelSource.Default }),
            s.PlayChannelAsync(AudioChannel.EyeRestStart, new AudioChannelConfig { Source = AudioChannelSource.Default })
        );
        sw.Stop();
        sw.ElapsedMilliseconds.Should().BeGreaterOrEqualTo(150, "three 50ms plays serialized = >= 150ms");
        s.MaxConcurrent.Should().Be(1);
    }

    [Fact(Timeout = 3000)]
    public async Task Cancellation_StopsPlayback_AndDisposesPlayer()
    {
        var s = new SlowFakeAudioService(playDurationMs: 5000);
        using var cts = new CancellationTokenSource();
        var task = s.PlayChannelAsync(AudioChannel.BreakStart,
            new AudioChannelConfig { Source = AudioChannelSource.Default }, cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        var act = async () => await task;
        await act.Should().ThrowAsync<OperationCanceledException>();
        s.DisposeCount.Should().Be(1);
    }
}
```

(`SlowFakeAudioService` is a test fixture extending `AudioServiceBase`, tracking max concurrent calls and dispose count — extend the `FakeAudioService` from Task 2.2 with timing, or write a sibling. Include full code; do not reference "similar to.")

- [ ] **Step 2: Run to verify failure / pass**

```bash
dotnet test EyeRest.Tests/EyeRest.Tests.csproj --filter "FullyQualifiedName~AudioServiceConcurrencyTests"
```
Expected: PASS (the `SemaphoreSlim` in `AudioServiceBase` already provides this; this task is verification, not new code).

If the cancellation test fails because `Task.Run(..., ct)` is the only cancellation hook and the inner work doesn't check `ct` again, fix `AudioServiceBase.GatedAsync` to thread `ct` into `Task.Run`.

- [ ] **Step 3: Commit**

```bash
git add EyeRest.Tests/Audio/AudioServiceConcurrencyTests.cs
git commit -m "test: verify AudioServiceBase serializes and honors cancellation"
```

---

### Task 2.6: M2 Technical Architect Review

- [ ] **Step 1: Dispatch Technical Architect sub-agent**

Use the `Agent` tool with the following prompt:

> You are the **Technical Architect** reviewing **Milestone 2 (Audio service surface + WAV playback)** of BL-002 in the Eye-rest project. Diff: M2 commits (`EyeRest.Core/Services/AudioServiceBase.cs`, both platform `AudioService.cs` files, `EyeRest.Abstractions/Services/IAudioService.cs`, `EyeRest.Abstractions/Services/IUrlOpener.cs`, `EyeRest.Tests/Audio/PlayChannelAsyncTests.cs`, `EyeRest.Tests/Audio/AudioServiceConcurrencyTests.cs`).
>
> Spec reference: `docs/plan/005-bl002-per-popup-audio-design.md` §3.2, §3.3, §3.4, §7.
>
> Review **only**:
>
> 1. **Performance** — Does the per-call construct/dispose of `SoundPlayer`/`AVAudioPlayer` add measurable latency? Is `SemaphoreSlim.WaitAsync(ct)` correctly propagating cancellation without leaking the semaphore on exception?
> 2. **Memory Leak** — Verify `SoundPlayer` and `AVAudioPlayer` are disposed in EVERY code path (success, exception, cancellation). Look for any field-stored player references. Verify NSSound completion handlers don't capture and retain `self` (macOS).
> 3. **Resource Lifecycle** — Is the semaphore released in `finally`? Does the platform service get disposed cleanly when the app shuts down? Are there any orphan `Task.Run` continuations after cancellation?
>
> Output: numbered findings with severity + file:line citations. <500 words.

- [ ] **Step 2: Address Critical findings**

If Critical issues are reported, fix and re-test before closing M2.

- [ ] **Step 3: Mark M2 complete**

---

## Milestone 3 — Asset generation + bundling + URL opener

**Spec §3.5, §3.6.**

### Task 3.1: Write the tone-synthesis script and generate WAVs

**Files:**
- Create: `scripts/generate-default-sounds.csx`
- Create (as outputs): `EyeRest.UI/Assets/Sounds/eye-rest-start.wav`, `eye-rest-end.wav`, `break-start.wav`, `break-end.wav`

- [ ] **Step 1: Write the script**

```csharp
// scripts/generate-default-sounds.csx
// Run with: dotnet script scripts/generate-default-sounds.csx
// Generates four PCM WAV files at 44.1kHz / 16-bit / mono.
using System;
using System.IO;

static byte[] BuildWav(double[] samples)
{
    const int sampleRate = 44100;
    const short bits = 16;
    const short channels = 1;
    int dataLen = samples.Length * sizeof(short);
    using var ms = new MemoryStream();
    using var bw = new BinaryWriter(ms);
    bw.Write("RIFF".ToCharArray());
    bw.Write(36 + dataLen);
    bw.Write("WAVEfmt ".ToCharArray());
    bw.Write(16);                             // PCM chunk size
    bw.Write((short)1);                       // PCM
    bw.Write(channels);
    bw.Write(sampleRate);
    bw.Write(sampleRate * channels * bits / 8);
    bw.Write((short)(channels * bits / 8));
    bw.Write(bits);
    bw.Write("data".ToCharArray());
    bw.Write(dataLen);
    foreach (var v in samples)
        bw.Write((short)(Math.Clamp(v, -1.0, 1.0) * short.MaxValue));
    return ms.ToArray();
}

static double[] Tone(double freq, double durationS, double attackS = 0.01, double releaseS = 0.05)
{
    const int sr = 44100;
    int n = (int)(durationS * sr);
    var s = new double[n];
    for (int i = 0; i < n; i++)
    {
        double t = i / (double)sr;
        double env = 1.0;
        if (t < attackS) env = t / attackS;
        else if (t > durationS - releaseS) env = (durationS - t) / releaseS;
        s[i] = Math.Sin(2 * Math.PI * freq * t) * env * 0.6;
    }
    return s;
}

static double[] Concat(params double[][] parts)
{
    int n = 0; foreach (var p in parts) n += p.Length;
    var o = new double[n]; int i = 0;
    foreach (var p in parts) { Array.Copy(p, 0, o, i, p.Length); i += p.Length; }
    return o;
}

void Write(string path, double[] samples) => File.WriteAllBytes(path, BuildWav(samples));

string outDir = "EyeRest.UI/Assets/Sounds";
Directory.CreateDirectory(outDir);

// Eye-rest start: ascending two-note chime (A5 → C#6), gentle
Write($"{outDir}/eye-rest-start.wav",
    Concat(Tone(880, 0.20), Tone(1108.73, 0.30)));

// Eye-rest end: descending two-note chime (C#6 → A5), gentle
Write($"{outDir}/eye-rest-end.wav",
    Concat(Tone(1108.73, 0.18), Tone(880, 0.28)));

// Break start: warm bell, single longer note (E5)
Write($"{outDir}/break-start.wav",
    Tone(659.25, 1.00, attackS: 0.005, releaseS: 0.4));

// Break end: bright three-note rise (E5 → G5 → B5)
Write($"{outDir}/break-end.wav",
    Concat(Tone(659.25, 0.20), Tone(783.99, 0.20), Tone(987.77, 0.40)));

Console.WriteLine("Generated 4 WAV files in " + outDir);
```

- [ ] **Step 2: Generate the files**

```bash
cd /Users/tamtran/sources/demo/eye-rest
dotnet script scripts/generate-default-sounds.csx
ls -la EyeRest.UI/Assets/Sounds/
```
Expected: 4 `.wav` files, each ~30–90 KB.

If `dotnet script` is not installed, install with `dotnet tool install -g dotnet-script` first.

- [ ] **Step 3: Smoke-play each WAV manually**

```bash
afplay EyeRest.UI/Assets/Sounds/eye-rest-start.wav   # macOS
afplay EyeRest.UI/Assets/Sounds/eye-rest-end.wav
afplay EyeRest.UI/Assets/Sounds/break-start.wav
afplay EyeRest.UI/Assets/Sounds/break-end.wav
```
Expected: each sound plays cleanly, no clicks/pops, distinct.

- [ ] **Step 4: Commit**

```bash
git add scripts/generate-default-sounds.csx EyeRest.UI/Assets/Sounds/
git commit -m "feat: add four bundled default popup audio WAVs (eye-rest/break start+end)"
```

---

### Task 3.2: Add `<AvaloniaResource>` include

**Files:**
- Modify: `EyeRest.UI/EyeRest.UI.csproj`

- [ ] **Step 1: Add include**

Inside the existing `<ItemGroup>` that has other `<AvaloniaResource>` entries (or add a new ItemGroup):

```xml
<ItemGroup>
  <AvaloniaResource Include="Assets\Sounds\*.wav" />
</ItemGroup>
```

- [ ] **Step 2: Verify build packages the resources**

```bash
dotnet build EyeRest.UI/EyeRest.UI.csproj
```
Expected: BUILD SUCCESS.

- [ ] **Step 3: Commit**

```bash
git add EyeRest.UI/EyeRest.UI.csproj
git commit -m "build: include Assets/Sounds/*.wav as AvaloniaResource"
```

---

### Task 3.3: Implement `BundledSoundCache` (temp-file cache)

**Files:**
- Create: `EyeRest.Core/Services/BundledSoundCache.cs`
- Create: `EyeRest.Tests/Audio/BundledSoundCacheTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// EyeRest.Tests/Audio/BundledSoundCacheTests.cs
using EyeRest.Abstractions.Services;
using EyeRest.Core.Services;
using FluentAssertions;
using Xunit;

namespace EyeRest.Tests.Audio;

public class BundledSoundCacheTests
{
    [Fact]
    public void GetPath_FirstCall_ExtractsAndReturnsValidFile()
    {
        var cache = new BundledSoundCache();
        var path = cache.GetPath(AudioChannel.EyeRestStart);
        path.Should().NotBeNullOrWhiteSpace();
        File.Exists(path).Should().BeTrue();
        new FileInfo(path).Length.Should().BeGreaterThan(100);
    }

    [Fact]
    public void GetPath_SecondCall_ReturnsSamePath_NoReExtraction()
    {
        var cache = new BundledSoundCache();
        var p1 = cache.GetPath(AudioChannel.BreakStart);
        var t1 = File.GetLastWriteTimeUtc(p1);
        Thread.Sleep(50);
        var p2 = cache.GetPath(AudioChannel.BreakStart);
        p2.Should().Be(p1);
        File.GetLastWriteTimeUtc(p2).Should().Be(t1);
    }

    [Fact]
    public void GetPath_AllFourChannels_AreDistinctFiles()
    {
        var cache = new BundledSoundCache();
        var paths = new[]
        {
            cache.GetPath(AudioChannel.EyeRestStart),
            cache.GetPath(AudioChannel.EyeRestEnd),
            cache.GetPath(AudioChannel.BreakStart),
            cache.GetPath(AudioChannel.BreakEnd),
        };
        paths.Distinct().Should().HaveCount(4);
    }
}
```

- [ ] **Step 2: Run to verify failure**

```bash
dotnet test EyeRest.Tests/EyeRest.Tests.csproj --filter "FullyQualifiedName~BundledSoundCacheTests"
```
Expected: FAIL — `BundledSoundCache` does not exist.

- [ ] **Step 3: Implement**

```csharp
// EyeRest.Core/Services/BundledSoundCache.cs
using System;
using System.Collections.Concurrent;
using System.IO;
using Avalonia.Platform;
using EyeRest.Abstractions.Services;

namespace EyeRest.Core.Services;

public sealed class BundledSoundCache
{
    private static readonly string CacheDir =
        Path.Combine(Path.GetTempPath(), "EyeRest", "sounds");

    private readonly ConcurrentDictionary<AudioChannel, string> _extracted = new();

    static BundledSoundCache()
    {
        Directory.CreateDirectory(CacheDir);
    }

    public string GetPath(AudioChannel channel)
        => _extracted.GetOrAdd(channel, Extract);

    private static string Extract(AudioChannel channel)
    {
        var fileName = channel switch
        {
            AudioChannel.EyeRestStart  => "eye-rest-start.wav",
            AudioChannel.EyeRestEnd    => "eye-rest-end.wav",
            AudioChannel.BreakStart    => "break-start.wav",
            AudioChannel.BreakEnd      => "break-end.wav",
            AudioChannel.BreakWarning  => "break-start.wav", // reuse for warning
            _ => throw new ArgumentOutOfRangeException(nameof(channel)),
        };
        var destPath = Path.Combine(CacheDir, fileName);
        if (File.Exists(destPath) && new FileInfo(destPath).Length > 0)
            return destPath;

        var uri = new Uri($"avares://EyeRest.UI/Assets/Sounds/{fileName}");
        using var src = AssetLoader.Open(uri);
        using var dst = File.Create(destPath);
        src.CopyTo(dst);
        return destPath;
    }
}
```

- [ ] **Step 4: Run to verify pass**

```bash
dotnet test EyeRest.Tests/EyeRest.Tests.csproj --filter "FullyQualifiedName~BundledSoundCacheTests"
```
Expected: PASS.

- [ ] **Step 5: Wire `BundledSoundCache` into both platform audio services**

In both `AudioService.cs` (Windows) and `MacOSAudioService.cs` (macOS): inject `BundledSoundCache` in the constructor. In `PlayDefaultAsync`, replace the current named-sound calls with:

```csharp
var path = _bundledCache.GetPath(channel);
await PlayFileAsync(path, ct).ConfigureAwait(false);
```

This unifies Default and File playback paths under the same `PlayFileAsync` implementation — the only difference is where the path comes from.

- [ ] **Step 6: Register `BundledSoundCache` in DI**

In platform DI extension files (Windows and macOS):
```csharp
services.AddSingleton<BundledSoundCache>();
```

- [ ] **Step 7: Run all tests + smoke check**

```bash
dotnet test EyeRest.Tests/EyeRest.Tests.csproj
dotnet build EyeRest.sln
```
Expected: all PASS, BUILD SUCCESS.

- [ ] **Step 8: Commit**

```bash
git add EyeRest.Core/Services/BundledSoundCache.cs EyeRest.Tests/Audio/BundledSoundCacheTests.cs EyeRest.Platform.Windows EyeRest.Platform.macOS
git commit -m "feat: BundledSoundCache extracts AvaloniaResource WAVs to temp for native playback"
```

---

### Task 3.4: Implement `DefaultUrlOpener`

**Files:**
- Create: `EyeRest.Core/Services/DefaultUrlOpener.cs`
- Create: `EyeRest.Tests/Audio/DefaultUrlOpenerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// EyeRest.Tests/Audio/DefaultUrlOpenerTests.cs
using EyeRest.Core.Services;
using FluentAssertions;
using Xunit;

namespace EyeRest.Tests.Audio;

public class DefaultUrlOpenerTests
{
    [Fact]
    public void Open_NullOrEmpty_DoesNotThrow()
    {
        var opener = new DefaultUrlOpener();
        var act1 = () => opener.Open(null!);
        var act2 = () => opener.Open("");
        var act3 = () => opener.Open("   ");
        act1.Should().NotThrow();
        act2.Should().NotThrow();
        act3.Should().NotThrow();
    }

    // NOTE: We can't unit-test that the browser actually opened on a headless
    // CI. The real verification is in M5 manual testing.
}
```

- [ ] **Step 2: Run to verify failure**

```bash
dotnet test EyeRest.Tests/EyeRest.Tests.csproj --filter "FullyQualifiedName~DefaultUrlOpenerTests"
```
Expected: FAIL — `DefaultUrlOpener` does not exist.

- [ ] **Step 3: Implement**

```csharp
// EyeRest.Core/Services/DefaultUrlOpener.cs
using System.Diagnostics;
using EyeRest.Abstractions.Services;

namespace EyeRest.Core.Services;

public sealed class DefaultUrlOpener : IUrlOpener
{
    public void Open(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            using var _ = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Best-effort: if the OS can't dispatch the URL, log and move on.
            // No need to crash the popup flow over a missing browser.
        }
    }
}
```

- [ ] **Step 4: Run to verify pass + replace any DI stubs**

```bash
dotnet test EyeRest.Tests/EyeRest.Tests.csproj --filter "FullyQualifiedName~DefaultUrlOpenerTests"
```
Expected: PASS.

In platform DI files, replace any temporary `StubUrlOpener` registration with:
```csharp
services.AddSingleton<IUrlOpener, DefaultUrlOpener>();
```
Delete the stub class.

- [ ] **Step 5: Commit**

```bash
git add EyeRest.Core/Services/DefaultUrlOpener.cs EyeRest.Tests/Audio/DefaultUrlOpenerTests.cs EyeRest.Platform.Windows EyeRest.Platform.macOS
git commit -m "feat: DefaultUrlOpener via Process.Start with UseShellExecute"
```

---

### Task 3.5: M3 Technical Architect Review

- [ ] **Step 1: Dispatch Technical Architect sub-agent**

> You are the **Technical Architect** reviewing **Milestone 3 (Assets + URL opener)** of BL-002. Diff: M3 commits touching `EyeRest.UI/Assets/Sounds/`, `scripts/generate-default-sounds.csx`, `EyeRest.UI/EyeRest.UI.csproj`, `EyeRest.Core/Services/BundledSoundCache.cs`, `EyeRest.Core/Services/DefaultUrlOpener.cs`, both platform services' Default-playback hookup, and matching tests.
>
> Spec reference: §3.5, §3.6, §7.
>
> Review **only**:
>
> 1. **Performance** — First-call extraction latency for bundled WAVs. Is the `ConcurrentDictionary.GetOrAdd` factory thread-safe and called once per channel?
> 2. **Memory Leak** — Verify `AssetLoader.Open` returned stream is disposed. Verify `Process.Start` Process wrapper disposed (`using var _`). Confirm the static `CacheDir` initialization does not retain handles.
> 3. **Resource Lifecycle** — Cache files persist across app restarts (intentional). Could two app instances race on writing the same file? Verify `File.Create` + `CopyTo` atomicity is acceptable (atomic-rename if not).
>
> Output: numbered findings + severity + cites. <500 words.

- [ ] **Step 2: Address Criticals**
- [ ] **Step 3: Mark M3 complete**

---

## Milestone 4 — Settings UI

**Spec §5.** Adds the "Popup Audio" card to `MainWindow.axaml` with four per-channel rows.

### Task 4.1: Add ViewModel properties + commands

**Files:**
- Modify: `EyeRest.UI/ViewModels/MainWindowViewModel.cs`

For each of the four channels, the ViewModel exposes:

- `AudioChannelSource <Channel>Source` (e.g., `EyeRestStartSource`) — two-way bound to radio group.
- `string? <Channel>FilePath`
- `string? <Channel>Url`
- `IAsyncCommand Browse<Channel>FileCommand`
- `IAsyncCommand Test<Channel>FileCommand` (plays via `PlayChannelAsync`)
- `IAsyncCommand Open<Channel>UrlCommand` (test URL)

Where `<Channel>` ∈ {EyeRestStart, EyeRestEnd, BreakStart, BreakEnd}.

- [ ] **Step 1: Add backing fields + properties**

```csharp
// Inside MainWindowViewModel, in an "Audio Channels" region:
private AudioChannelSource _eyeRestStartSource;
public AudioChannelSource EyeRestStartSource
{
    get => _eyeRestStartSource;
    set
    {
        if (_eyeRestStartSource == value) return;
        _eyeRestStartSource = value;
        OnPropertyChanged();
        ScheduleSave(); // existing dirty-tracking save mechanism
    }
}

private string? _eyeRestStartFilePath;
public string? EyeRestStartFilePath
{
    get => _eyeRestStartFilePath;
    set { if (_eyeRestStartFilePath != value) { _eyeRestStartFilePath = value; OnPropertyChanged(); ScheduleSave(); } }
}

private string? _eyeRestStartUrl;
public string? EyeRestStartUrl
{
    get => _eyeRestStartUrl;
    set { if (_eyeRestStartUrl != value) { _eyeRestStartUrl = value; OnPropertyChanged(); ScheduleSave(); } }
}

// Repeat the three property triplets for EyeRestEnd, BreakStart, BreakEnd.
```

- [ ] **Step 2: Add 8 commands per channel (4 file browse + 4 file test + 4 url test = 12)**

Actually: 3 commands per channel × 4 channels = 12 commands. Use a shared private helper:

```csharp
private async Task BrowseAndSetAsync(Action<string?> setPath)
{
    var dlg = new OpenFileDialog
    {
        AllowMultiple = false,
        Filters = new List<FileDialogFilter> { new() { Name = "Audio (*.wav;*.mp3)", Extensions = { "wav", "mp3" } } },
    };
    var result = await dlg.ShowAsync(GetMainWindow());
    if (result is { Length: > 0 }) setPath(result[0]);
}

private async Task TestChannelAsync(AudioChannel channel, AudioChannelSource source, string? filePath, string? url)
{
    var cfg = new AudioChannelConfig { Source = source, CustomFilePath = filePath, Url = url };
    await _audioService.PlayChannelAsync(channel, cfg);
}

// Commands:
public IAsyncCommand BrowseEyeRestStartFileCommand =>
    new AsyncRelayCommand(() => BrowseAndSetAsync(p => EyeRestStartFilePath = p));

public IAsyncCommand TestEyeRestStartFileCommand =>
    new AsyncRelayCommand(() => TestChannelAsync(AudioChannel.EyeRestStart,
        AudioChannelSource.File, EyeRestStartFilePath, EyeRestStartUrl));

public IAsyncCommand OpenEyeRestStartUrlCommand =>
    new AsyncRelayCommand(() => TestChannelAsync(AudioChannel.EyeRestStart,
        AudioChannelSource.Url, EyeRestStartFilePath, EyeRestStartUrl));

// Repeat for EyeRestEnd, BreakStart, BreakEnd.
```

- [ ] **Step 3: Wire load/save**

In the existing `ApplyConfigurationToViewModel` (or equivalent) method:
```csharp
EyeRestStartSource    = config.EyeRest.StartAudio.Source;
EyeRestStartFilePath  = config.EyeRest.StartAudio.CustomFilePath;
EyeRestStartUrl       = config.EyeRest.StartAudio.Url;
// … same for EyeRestEnd, BreakStart, BreakEnd …
```

In the existing per-field save path, ensure these properties are included in the dirty-tracking HashSet (per the Mar 2026 lessons-learned about Slider corruption).

- [ ] **Step 4: Build + run existing tests**

```bash
dotnet build EyeRest.UI/EyeRest.UI.csproj
dotnet test EyeRest.Tests/EyeRest.Tests.csproj
```
Expected: BUILD SUCCESS, tests PASS.

- [ ] **Step 5: Commit**

```bash
git add EyeRest.UI/ViewModels/MainWindowViewModel.cs
git commit -m "feat: ViewModel properties and commands for four audio channels"
```

---

### Task 4.2: Add "Popup Audio" card to `MainWindow.axaml`

**Files:**
- Modify: `EyeRest.UI/Views/MainWindow.axaml`

Insert a new `Border`/`Card`-styled section between the existing "Audio" card (~line 1124) and the per-timer sections. Four nearly-identical row groups, one per channel.

- [ ] **Step 1: Add the AXAML block**

```xml
<!-- Popup Audio (BL-002) -->
<Border Classes="settingsCard">
  <StackPanel Spacing="16">
    <TextBlock Classes="cardHeader" Text="Popup Audio" />

    <!-- Eye-rest Start -->
    <StackPanel Spacing="8">
      <TextBlock Classes="rowHeader" Text="Eye-rest Start" />
      <StackPanel Orientation="Horizontal" Spacing="8">
        <RadioButton GroupName="EyeRestStartSource" Content="Default"
                     IsChecked="{Binding EyeRestStartSource, Converter={StaticResource EnumToBoolConverter}, ConverterParameter=Default}" />
        <RadioButton GroupName="EyeRestStartSource" Content="File"
                     IsChecked="{Binding EyeRestStartSource, Converter={StaticResource EnumToBoolConverter}, ConverterParameter=File}" />
        <RadioButton GroupName="EyeRestStartSource" Content="URL"
                     IsChecked="{Binding EyeRestStartSource, Converter={StaticResource EnumToBoolConverter}, ConverterParameter=Url}" />
        <RadioButton GroupName="EyeRestStartSource" Content="Off"
                     IsChecked="{Binding EyeRestStartSource, Converter={StaticResource EnumToBoolConverter}, ConverterParameter=Off}" />
      </StackPanel>
      <Grid ColumnDefinitions="Auto,*,Auto,Auto" RowDefinitions="Auto,Auto" Margin="0,4,0,0">
        <TextBlock Grid.Row="0" Grid.Column="0" Text="File:"/>
        <TextBox    Grid.Row="0" Grid.Column="1" Text="{Binding EyeRestStartFilePath}" />
        <Button     Grid.Row="0" Grid.Column="2" Content="Browse" Command="{Binding BrowseEyeRestStartFileCommand}" />
        <Button     Grid.Row="0" Grid.Column="3" Content="▶︎"      Command="{Binding TestEyeRestStartFileCommand}" />

        <TextBlock  Grid.Row="1" Grid.Column="0" Text="URL:"/>
        <TextBox    Grid.Row="1" Grid.Column="1" Text="{Binding EyeRestStartUrl}" />
        <Button     Grid.Row="1" Grid.Column="3" Content="↗"      Command="{Binding OpenEyeRestStartUrlCommand}" />
      </Grid>
    </StackPanel>

    <Separator />

    <!-- Eye-rest End — same shape, replacing EyeRestStart → EyeRestEnd -->
    <StackPanel Spacing="8">
      <!-- (full block, same structure as Eye-rest Start, all property names rebased to EyeRestEnd) -->
    </StackPanel>

    <Separator />

    <!-- Break Start — same shape, BreakStart -->
    <StackPanel Spacing="8">
      <!-- (full block, BreakStart) -->
    </StackPanel>

    <Separator />

    <!-- Break End — same shape, BreakEnd -->
    <StackPanel Spacing="8">
      <!-- (full block, BreakEnd) -->
    </StackPanel>
  </StackPanel>
</Border>
```

Replace each `<!-- (full block …) -->` placeholder by copying the Eye-rest Start block verbatim and substituting the binding names. Do NOT leave placeholders; the implementer must write all four blocks in full.

- [ ] **Step 2: Verify `EnumToBoolConverter` exists**

```bash
grep -rn "EnumToBoolConverter" EyeRest.UI/
```
If the converter exists, ensure it's listed in `App.axaml` Resources. If not, create it:

```csharp
// EyeRest.UI/Converters/EnumToBoolConverter.cs
using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace EyeRest.UI.Converters;

public sealed class EnumToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null && parameter is not null && string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true && parameter is not null && Enum.TryParse(targetType, parameter.ToString(), out var e) ? e : Avalonia.Data.BindingOperations.DoNothing;
}
```

Register in `App.axaml`:
```xml
<converters:EnumToBoolConverter x:Key="EnumToBoolConverter" />
```

- [ ] **Step 3: Run the app and visually verify**

```bash
dotnet run --project EyeRest.UI
```
Navigate to settings, confirm the new "Popup Audio" card renders with four sections and that radio selection persists across app restarts.

- [ ] **Step 4: Commit**

```bash
git add EyeRest.UI/Views/MainWindow.axaml EyeRest.UI/Converters/EnumToBoolConverter.cs EyeRest.UI/App.axaml
git commit -m "feat: add Popup Audio card with four per-channel source rows"
```

---

### Task 4.3: Remove temporary `EyeRestStartSoundEnabled`-style shims

**Files:**
- Modify: `EyeRest.UI/ViewModels/MainWindowViewModel.cs`

In Task 1.4 we may have added temporary property shims so AXAML compiled. Now that the new bindings are in place, delete them.

- [ ] **Step 1: Search for shims**

```bash
grep -n "EyeRestStartSoundEnabled\|EyeRestEndSoundEnabled\|BreakStartSoundEnabled\|BreakEndSoundEnabled" EyeRest.UI/ViewModels/MainWindowViewModel.cs
```

- [ ] **Step 2: Delete each shim property**

Remove the temporary getter/setter pairs added during M1 caller fixup.

- [ ] **Step 3: Build + run**

```bash
dotnet build EyeRest.sln
dotnet run --project EyeRest.UI
```
Expected: BUILD SUCCESS; settings UI works.

- [ ] **Step 4: Commit**

```bash
git add EyeRest.UI/ViewModels/MainWindowViewModel.cs
git commit -m "chore: remove temporary StartSoundEnabled/EndSoundEnabled ViewModel shims"
```

---

### Task 4.4: M4 Technical Architect Review

- [ ] **Step 1: Dispatch Technical Architect sub-agent**

> You are the **Technical Architect** reviewing **Milestone 4 (Settings UI)** of BL-002. Diff: M4 commits touching `EyeRest.UI/Views/MainWindow.axaml`, `EyeRest.UI/ViewModels/MainWindowViewModel.cs`, `EyeRest.UI/Converters/EnumToBoolConverter.cs`.
>
> Spec reference: §5, §7.
>
> Review **only**:
>
> 1. **Performance** — Is the dirty-tracking save (per Mar 2026 lessons-learned) correctly extended to the 12 new properties? Any debouncing missing?
> 2. **Memory Leak** — Verify the new AsyncRelayCommand instances aren't recreated on every getter call (memory churn) — they should be lazy-initialized fields, not expression-bodied properties. Verify no event handlers leak the ViewModel.
> 3. **Resource Lifecycle** — File picker dialog disposal; Test buttons invoke `PlayChannelAsync` which constructs/disposes its own player; confirm the call chain is awaited (no fire-and-forget that survives ViewModel disposal).
>
> Output: numbered findings + severity + cites. <500 words.

- [ ] **Step 2: Address Criticals**

In particular, if the architect flags commands-as-expression-bodied-properties as a memory churn issue, refactor each command to a backing `_lazyCommand` field with one-time initialization.

- [ ] **Step 3: Mark M4 complete**

---

## Milestone 5 — Integration + spec-vs-implementation audit

**Spec §6, §7, §8.**

### Task 5.1: Wire `NotificationService` to call `PlayChannelAsync` for all 4 events

**Files:**
- Modify: `EyeRest.UI/Services/AvaloniaNotificationService.cs`
- Modify: `EyeRest.Core/Services/ApplicationOrchestrator.cs` (or wherever audio is currently called)

The current code likely calls `_audioService.PlayEyeRestStartSound()` etc. from the orchestrator. We want the channel-config-aware path, fed from `AppConfiguration.EyeRest.StartAudio` and friends.

- [ ] **Step 1: Identify current audio call sites**

```bash
grep -rn "PlayEyeRestStartSound\|PlayEyeRestEndSound\|PlayBreakStartSound\|PlayBreakEndSound" \
  --include="*.cs" EyeRest.Core EyeRest.UI
```

- [ ] **Step 2: Replace with `PlayChannelAsync` calls**

In `AvaloniaNotificationService.ShowEyeRestReminderInternalAsync` (line ~138):
```csharp
// REPLACE the existing audio call (if any) with:
_ = _audioService.PlayChannelAsync(
    AudioChannel.EyeRestStart,
    _configurationService.Current.EyeRest.StartAudio,
    _appLifecycle.ShutdownToken);
```

In the corresponding `myPopup.Closed` handler (~line 147):
```csharp
_ = _audioService.PlayChannelAsync(
    AudioChannel.EyeRestEnd,
    _configurationService.Current.EyeRest.EndAudio,
    _appLifecycle.ShutdownToken);
```

Repeat for break-start (~line 329) and break-closed (~line 348).

Make sure `_configurationService` and `_appLifecycle` are constructor-injected dependencies; if not, add them.

- [ ] **Step 3: Build + run all tests**

```bash
dotnet build EyeRest.sln
dotnet test EyeRest.Tests/EyeRest.Tests.csproj
```
Expected: BUILD SUCCESS, all tests PASS.

- [ ] **Step 4: Commit**

```bash
git add EyeRest.UI/Services/AvaloniaNotificationService.cs
git commit -m "feat: NotificationService dispatches all 4 popup audio events via PlayChannelAsync"
```

---

### Task 5.2: 100-cycle handle-leak integration test

**Files:**
- Create: `EyeRest.Tests/Audio/AudioLifecycleIntegrationTests.cs`

- [ ] **Step 1: Write the test**

```csharp
// EyeRest.Tests/Audio/AudioLifecycleIntegrationTests.cs
using System.Diagnostics;
using EyeRest.Abstractions.Models;
using EyeRest.Abstractions.Services;
using FluentAssertions;
using Xunit;

namespace EyeRest.Tests.Audio;

[Trait("Category", "Integration")]
public class AudioLifecycleIntegrationTests
{
    [Fact(Timeout = 30000)]
    public async Task HundredCycles_NoHandleLeak()
    {
        var sut = AudioServiceTestFactory.CreateForCurrentPlatform();
        var proc = Process.GetCurrentProcess();
        long startHandles = proc.HandleCount;

        for (int i = 0; i < 100; i++)
        {
            await sut.PlayChannelAsync(AudioChannel.EyeRestStart,
                new AudioChannelConfig { Source = AudioChannelSource.Default });
        }

        proc.Refresh();
        long endHandles = proc.HandleCount;
        long delta = endHandles - startHandles;
        delta.Should().BeLessThan(20, $"100 cycles leaked ~{delta} handles");
    }
}
```

`AudioServiceTestFactory.CreateForCurrentPlatform()` is a helper that wires the real platform `AudioService` with a minimal in-memory `IConfigurationService` stub. Write the factory if it doesn't exist.

- [ ] **Step 2: Run the test**

```bash
dotnet test EyeRest.Tests/EyeRest.Tests.csproj --filter "Category=Integration&FullyQualifiedName~AudioLifecycle"
```
Expected: PASS — handle delta below tolerance. If it fails, M2's disposal review missed something; reopen.

- [ ] **Step 3: Commit**

```bash
git add EyeRest.Tests/Audio/AudioLifecycleIntegrationTests.cs
git commit -m "test: 100-cycle handle-leak guard for PlayChannelAsync"
```

---

### Task 5.3: Manual test matrix

This is human work — captured in the plan so the implementer (or reviewer) executes it before sign-off.

- [ ] **Run the app**

```bash
dotnet run --project EyeRest.UI
```

- [ ] **For each of 4 channels × 4 sources (16 combinations), verify:**

| Channel | Source | Expected behavior |
|---------|--------|-------------------|
| EyeRestStart | Default | Bundled WAV plays when 20-min timer fires |
| EyeRestStart | File    | Selected file plays |
| EyeRestStart | Url     | Browser tab opens to URL when popup shows |
| EyeRestStart | Off     | No audio, no browser action |
| EyeRestEnd   | (same four) | Same behavior at popup auto-dismiss |
| BreakStart   | (same four) | Same behavior at break popup show |
| BreakEnd     | (same four) | Same behavior at break popup close (any path) |

- [ ] **Test retention:** Configure File for a channel, switch to URL, switch back to File. File path remains.
- [ ] **Test global toggle:** With AudioSettings.Enabled = false, verify Default and File sources silent, but URL still opens (per §3.3).
- [ ] **Test mid-playback shutdown:** Trigger a long File playback, then close the app. Verify no orphan process or log error.

Record results in `docs/troubleshooting/` if any fail.

---

### Task 5.4: Spec-vs-implementation audit

**Per the project CLAUDE.md mandatory post-plan audit.**

- [ ] **Step 1: Dispatch Explore agent for audit**

Use `Agent` tool, `subagent_type: Explore`, with the prompt:

> Audit the implementation of BL-002 against its spec. Spec at `docs/plan/005-bl002-per-popup-audio-design.md`. For each section §1–§8, verify the corresponding code exists. Report gaps (missing, partial, extra) by section with severity (Critical/Major/Minor).
>
> Specific checks:
> - `AudioChannelSource` has exactly 4 values: Off, Default, File, Url.
> - `AudioChannelConfig` has 3 fields with the documented defaults.
> - Each of EyeRestSettings and BreakSettings has StartAudio and EndAudio (no other audio fields).
> - `AudioSettings` no longer has `CustomSoundPath`.
> - `ConfigMetadata.SchemaVersion = 2` after migration.
> - `IAudioService.PlayChannelAsync(channel, config, ct)` exists with that exact signature.
> - `AudioServiceBase` uses `SemaphoreSlim(1, 1)`.
> - `BundledSoundCache` extracts to `<temp>/EyeRest/sounds/`.
> - `DefaultUrlOpener` uses `Process.Start` with `UseShellExecute = true` and `using` disposal.
> - `EyeRest.UI/Assets/Sounds/` contains exactly the 4 documented WAVs and they are included via `<AvaloniaResource>`.
> - `MainWindow.axaml` has a Popup Audio card with exactly 4 channel rows.
>
> Output: a numbered gap report.

- [ ] **Step 2: Address Critical/Major gaps**

- [ ] **Step 3: Update backlog: mark BL-002 status as Done**

In `docs/backlog/001-product-backlog.md`, change BL-002 status from `New` to `Done`.

- [ ] **Step 4: Commit audit-driven fixes (if any)**

```bash
git add -u
git commit -m "fix: address spec-vs-implementation audit gaps"
```

---

### Task 5.5: M5 Technical Architect Review (final)

- [ ] **Step 1: Dispatch Technical Architect sub-agent**

> You are the **Technical Architect** doing the **final review** of BL-002 (M5 + cumulative). Diff: full M1–M5 work. Spec: `docs/plan/005-bl002-per-popup-audio-design.md` §6, §7, §8.
>
> Review **only**:
>
> 1. **Performance** — End-to-end popup-show → audio playback latency (should be <100ms). No regressions in popup show time vs main branch.
> 2. **Memory Leak** — Run the 100-cycle integration test mentally: any code path that fails to dispose a player? Any subscription on `_configurationService.Current` that survives orchestrator disposal?
> 3. **Resource Lifecycle** — Audio must NOT continue playing after orchestrator shutdown. URL launches must NOT leave orphan Process objects. Bundled WAV cache files persist across runs (intentional) but app must not write to them after a shutdown signal.
>
> Output: numbered findings + final go/no-go verdict. <500 words.

- [ ] **Step 2: Address Criticals**

- [ ] **Step 3: Final commit and push**

```bash
git push origin develop
```

Per CLAUDE.md: do NOT push to master. The feature lives on develop until a human approves merging.

- [ ] **Step 4: Mark plan complete**

Update this plan: all `- [ ]` → `- [x]`. Update `docs/backlog/001-product-backlog.md` BL-002 status to `Done`.

---

## Cross-cutting Notes

- **Never commit secrets or credentials.** None expected in this work.
- **Verification before completion** (`superpowers:verification-before-completion`): never mark a task complete without showing the actual command output.
- **TDD discipline:** all tests are written failing first. If a test passes without writing implementation, the test isn't testing what you think.
- **Frequent commits:** every passing test → commit. The plan has ~30 commits when followed; that's correct, not too many.
- **Branch:** all work on `develop` (per the project's git-flow). No direct commits to master.

## Open follow-ups (post-merge, not in scope for this plan)

- Refactor 4 inline AXAML card blocks into a single `AudioChannelEditor` `UserControl`.
- Per-channel volume control.
- MP3/AAC format contract beyond best-effort.
- URL background-tab vs foreground-tab control.
