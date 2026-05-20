using System.Collections.Generic;

namespace EyeRest.Models;

/// <summary>
/// Per-channel audio recent items. Stored separately from AppConfiguration in
/// audio-recents.json — recents are transient convenience data, not app settings.
/// </summary>
public class AudioRecents
{
    public List<string> EyeRestStartFilePaths { get; set; } = new();
    public List<string> EyeRestStartUrls      { get; set; } = new();
    public List<string> EyeRestEndFilePaths   { get; set; } = new();
    public List<string> EyeRestEndUrls        { get; set; } = new();
    public List<string> BreakStartFilePaths   { get; set; } = new();
    public List<string> BreakStartUrls        { get; set; } = new();
    public List<string> BreakEndFilePaths     { get; set; } = new();
    public List<string> BreakEndUrls          { get; set; } = new();
}
