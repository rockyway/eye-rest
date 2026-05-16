using System.Diagnostics;
using EyeRest.Models;
using EyeRest.Services;
using FluentAssertions;
using Xunit;

namespace EyeRest.Tests.Avalonia.Audio;

public class AudioServiceConcurrencyTests
{
    [Fact(Timeout = 5000)]
    public async Task BackToBackCalls_AreSerializedByGate()
    {
        // Three 50ms plays issued concurrently must take >= 150ms total because
        // AudioServiceBase's SemaphoreSlim(1, 1) serializes them. MaxConcurrent
        // proves no two plays were ever in PlayDefaultAsync simultaneously, which
        // is what protects native audio handles from racing each other.
        var s = new SlowFakeAudioService(playDurationMs: 50);
        var sw = Stopwatch.StartNew();
        await Task.WhenAll(
            s.PlayChannelAsync(AudioChannel.BreakStart,   new AudioChannelConfig { Source = AudioChannelSource.Default }),
            s.PlayChannelAsync(AudioChannel.BreakEnd,     new AudioChannelConfig { Source = AudioChannelSource.Default }),
            s.PlayChannelAsync(AudioChannel.EyeRestStart, new AudioChannelConfig { Source = AudioChannelSource.Default })
        );
        sw.Stop();
        sw.ElapsedMilliseconds.Should().BeGreaterOrEqualTo(150,
            "three 50ms plays serialized through the SemaphoreSlim should take >= 150ms");
        s.MaxConcurrent.Should().Be(1);
        s.TotalPlays.Should().Be(3);
    }

    [Fact(Timeout = 3000)]
    public async Task Cancellation_MidPlayback_ThrowsAndDoesNotRunLaterStages()
    {
        // A long-running play that observes the cancellation token must throw
        // OperationCanceledException promptly. Subsequent code in the play
        // must not execute (the test fixture sets a "completed" flag at the
        // end, which we assert was never set).
        var s = new SlowFakeAudioService(playDurationMs: 5000);
        using var cts = new CancellationTokenSource();
        var task = s.PlayChannelAsync(AudioChannel.BreakStart,
            new AudioChannelConfig { Source = AudioChannelSource.Default }, cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        var act = async () => await task;
        await act.Should().ThrowAsync<OperationCanceledException>();
        s.CompletedPlays.Should().Be(0,
            "cancellation must abort before PlayDefaultAsync sets the completed flag");
    }

    [Fact(Timeout = 3000)]
    public async Task Cancellation_AfterSemaphoreAcquired_ReleasesSemaphore()
    {
        // Regression guard: if the SemaphoreSlim weren't released on cancel,
        // the next call would block forever and this test would time out.
        var s = new SlowFakeAudioService(playDurationMs: 2000);
        using var cts = new CancellationTokenSource();
        var firstTask = s.PlayChannelAsync(AudioChannel.BreakStart,
            new AudioChannelConfig { Source = AudioChannelSource.Default }, cts.Token);
        await Task.Delay(50);
        cts.Cancel();
        try { await firstTask; } catch (OperationCanceledException) { /* expected */ }

        // The semaphore must be released — second call (with a fresh quick play) completes normally.
        var s2 = new SlowFakeAudioService(playDurationMs: 20);
        // Use the same fixture's gate for a true regression test: rerun on s after the cancel.
        await s.PlayChannelAsync(AudioChannel.EyeRestStart,
            new AudioChannelConfig { Source = AudioChannelSource.Default });
        s.CompletedPlays.Should().Be(1, "the post-cancel play should complete normally");
    }
}

internal class SlowFakeAudioService : AudioServiceBase
{
    private readonly int _playDurationMs;
    private int _concurrent;
    public int MaxConcurrent { get; private set; }
    public int TotalPlays { get; private set; }
    public int CompletedPlays { get; private set; }

    public SlowFakeAudioService(int playDurationMs)
        : base(new NoOpUrlOpener())
    {
        _playDurationMs = playDurationMs;
    }

    public override bool IsAudioEnabled => true;

    protected override async Task PlayDefaultAsync(AudioChannel channel, CancellationToken ct)
    {
        Interlocked.Increment(ref _concurrent);
        try
        {
            TotalPlays++;
            MaxConcurrent = Math.Max(MaxConcurrent, _concurrent);
            await Task.Delay(_playDurationMs, ct);
            CompletedPlays++;
        }
        finally
        {
            Interlocked.Decrement(ref _concurrent);
        }
    }

    protected override Task PlayFileAsync(string filePath, CancellationToken ct) => Task.CompletedTask;

    public override Task PlayEyeRestStartSound() => Task.CompletedTask;
    public override Task PlayEyeRestEndSound()   => Task.CompletedTask;
    public override Task PlayBreakStartSound()   => Task.CompletedTask;
    public override Task PlayBreakEndSound()     => Task.CompletedTask;
    public override Task PlayBreakWarningSound() => Task.CompletedTask;
    public override Task PlayCustomSoundTestAsync() => Task.CompletedTask;
    public override Task TestEyeRestAudioAsync() => Task.CompletedTask;

    private sealed class NoOpUrlOpener : IUrlOpener { public void Open(string url) { } }
}
