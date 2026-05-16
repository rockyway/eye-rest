using EyeRest.Models;
using EyeRest.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace EyeRest.Tests.Avalonia.Audio;

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

    [Fact]
    public async Task NullConfig_DoesNothing_DoesNotThrow()
    {
        var s = NewService();
        await s.PlayChannelAsync(AudioChannel.EyeRestStart, null!);
        s.DefaultPlays.Should().Be(0);
        s.FilePlays.Should().BeEmpty();
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

    public override Task PlayEyeRestStartSound() => Task.CompletedTask;
    public override Task PlayEyeRestEndSound()   => Task.CompletedTask;
    public override Task PlayBreakStartSound()   => Task.CompletedTask;
    public override Task PlayBreakEndSound()     => Task.CompletedTask;
    public override Task PlayBreakWarningSound() => Task.CompletedTask;
    public override Task PlayCustomSoundTestAsync() => Task.CompletedTask;
    public override Task TestEyeRestAudioAsync() => Task.CompletedTask;
}
