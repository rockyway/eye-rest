using System.Diagnostics;
using EyeRest.Models;
using EyeRest.Services;
using FluentAssertions;
using Xunit;

namespace EyeRest.Tests.Avalonia.Audio;

/// <summary>
/// BL-002 M5: end-to-end handle-leak guard. Exercises AudioServiceBase's
/// per-call construct + dispose + SemaphoreSlim contract over many cycles
/// and asserts the OS handle count for the process doesn't grow without bound.
///
/// The test uses a fake service (no real platform audio) so it can run on
/// any CI host. The leak hazards being guarded are:
///   - SemaphoreSlim never-released on cancellation paths.
///   - Task continuations retained past completion.
///   - Hidden state in the cache/dispatch path.
/// For real-platform handle leaks (SoundPlayer on Windows, NSSound on macOS)
/// the architectural review chain (M2 + M3) is the load-bearing artifact;
/// real-handle stress would require a manual run on each platform.
/// </summary>
[Trait("Category", "Integration")]
public class AudioLifecycleIntegrationTests
{
    [Fact(Timeout = 30000)]
    public async Task HundredCycles_NoUnboundedHandleGrowth()
    {
        var s = new InstantFakeAudioService();
        var proc = Process.GetCurrentProcess();
        proc.Refresh();
        var startHandles = proc.HandleCount;

        for (var i = 0; i < 100; i++)
        {
            await s.PlayChannelAsync(AudioChannel.EyeRestStart,
                new AudioChannelConfig { Source = AudioChannelSource.Default });
        }

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        proc.Refresh();
        var endHandles = proc.HandleCount;
        var delta = endHandles - startHandles;

        // Tolerance accounts for shared JIT/GC handles created during the run.
        // A real per-call leak (e.g. an undisposed FileStream per play) would
        // show ~100 — anything below ~30 indicates the dispose contract holds.
        delta.Should().BeLessThan(30, $"100 cycles produced a handle delta of {delta}");
    }

    [Fact(Timeout = 10000)]
    public async Task RapidCancellationCycle_DoesNotLeakSemaphoreCapacity()
    {
        // Regression guard: each cancellation must release the semaphore. If even
        // one Release were missed across 50 cycles, the gate would saturate at
        // capacity=1 and subsequent calls would deadlock. The Timeout above
        // turns any deadlock into a test failure.
        var s = new SlowCancelableFakeAudioService(playDurationMs: 100);
        for (var i = 0; i < 50; i++)
        {
            using var cts = new CancellationTokenSource();
            var task = s.PlayChannelAsync(AudioChannel.BreakStart,
                new AudioChannelConfig { Source = AudioChannelSource.Default }, cts.Token);
            await Task.Delay(5);
            cts.Cancel();
            try { await task; } catch (OperationCanceledException) { /* expected */ }
        }
        // After 50 cancels, the semaphore should still be acquirable — one final
        // clean call must complete (else it deadlocks and Timeout fires).
        s.SetFastMode();
        await s.PlayChannelAsync(AudioChannel.BreakStart,
            new AudioChannelConfig { Source = AudioChannelSource.Default });
        s.SuccessfulPlays.Should().Be(1, "the post-cancel clean play must complete");
    }
}

internal class InstantFakeAudioService : AudioServiceBase
{
    public InstantFakeAudioService() : base(new NoOpUrlOpener()) { }
    public override bool IsAudioEnabled => true;
    protected override Task PlayDefaultAsync(AudioChannel channel, CancellationToken ct) => Task.CompletedTask;
    protected override Task PlayFileAsync(string filePath, CancellationToken ct) => Task.CompletedTask;
    public override Task PlayEyeRestStartSound() => Task.CompletedTask;
    public override Task PlayEyeRestEndSound() => Task.CompletedTask;
    public override Task PlayBreakStartSound() => Task.CompletedTask;
    public override Task PlayBreakEndSound() => Task.CompletedTask;
    public override Task PlayBreakWarningSound() => Task.CompletedTask;
    public override Task PlayCustomSoundTestAsync() => Task.CompletedTask;
    public override Task TestEyeRestAudioAsync() => Task.CompletedTask;
    private sealed class NoOpUrlOpener : IUrlOpener { public void Open(string url) { } }
}

internal class SlowCancelableFakeAudioService : AudioServiceBase
{
    private int _playDurationMs;
    public int SuccessfulPlays { get; private set; }
    public SlowCancelableFakeAudioService(int playDurationMs)
        : base(new NoOpUrlOpener()) { _playDurationMs = playDurationMs; }
    public void SetFastMode() => _playDurationMs = 1;
    public override bool IsAudioEnabled => true;
    protected override async Task PlayDefaultAsync(AudioChannel channel, CancellationToken ct)
    {
        await Task.Delay(_playDurationMs, ct);
        SuccessfulPlays++;
    }
    protected override Task PlayFileAsync(string filePath, CancellationToken ct) => Task.CompletedTask;
    public override Task PlayEyeRestStartSound() => Task.CompletedTask;
    public override Task PlayEyeRestEndSound() => Task.CompletedTask;
    public override Task PlayBreakStartSound() => Task.CompletedTask;
    public override Task PlayBreakEndSound() => Task.CompletedTask;
    public override Task PlayBreakWarningSound() => Task.CompletedTask;
    public override Task PlayCustomSoundTestAsync() => Task.CompletedTask;
    public override Task TestEyeRestAudioAsync() => Task.CompletedTask;
    private sealed class NoOpUrlOpener : IUrlOpener { public void Open(string url) { } }
}
