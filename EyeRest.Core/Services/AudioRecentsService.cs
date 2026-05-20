using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using EyeRest.Models;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services;

/// <summary>
/// Persists per-channel audio recent items to audio-recents.json, separate from
/// AppConfiguration so config.json stays lean (recents are transient UI data).
/// Load failures return empty recents; save failures are logged and swallowed —
/// losing recents on a filesystem hiccup is not worth surfacing to the user.
/// </summary>
public class AudioRecentsService : IAudioRecentsService
{
    private static readonly JsonSerializerOptions s_opts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly ILogger<AudioRecentsService> _logger;
    private readonly string _filePath;

    public AudioRecentsService(ILogger<AudioRecentsService> logger)
    {
        _logger = logger;
        _filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EyeRest", "audio-recents.json");
    }

    public async Task<AudioRecents> LoadAsync()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new AudioRecents();

            var json = await File.ReadAllTextAsync(_filePath).ConfigureAwait(false);
            return JsonSerializer.Deserialize<AudioRecents>(json, s_opts) ?? new AudioRecents();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load audio recents — starting fresh");
            return new AudioRecents();
        }
    }

    public async Task SaveAsync(AudioRecents recents)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            var json = JsonSerializer.Serialize(recents, s_opts);
            var tmp = _filePath + ".tmp";
            await File.WriteAllTextAsync(tmp, json).ConfigureAwait(false);
            File.Move(tmp, _filePath, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save audio recents");
        }
    }
}
