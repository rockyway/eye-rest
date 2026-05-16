using EyeRest.Models;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace EyeRest.Tests.Avalonia.Audio;

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

    [Fact]
    public void EyeRestSettings_NewInstance_HasDefaultStartAudio_AndDefaultEndAudio()
    {
        var s = new EyeRestSettings();
        s.StartAudio.Should().NotBeNull();
        s.StartAudio.Source.Should().Be(AudioChannelSource.Default);
        s.EndAudio.Should().NotBeNull();
        s.EndAudio.Source.Should().Be(AudioChannelSource.Default);
    }

    [Fact]
    public void BreakSettings_NewInstance_HasDefaultStartAudio_AndDefaultEndAudio()
    {
        var s = new BreakSettings();
        s.StartAudio.Should().NotBeNull();
        s.StartAudio.Source.Should().Be(AudioChannelSource.Default);
        s.EndAudio.Should().NotBeNull();
        s.EndAudio.Source.Should().Be(AudioChannelSource.Default);
    }
}
