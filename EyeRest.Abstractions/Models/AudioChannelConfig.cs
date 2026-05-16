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
