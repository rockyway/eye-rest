namespace EyeRest.Services
{
    /// <summary>
    /// BL-002 M3: resolves an <see cref="AudioChannel"/> to a filesystem path for
    /// its bundled default WAV. The implementation in EyeRest.UI extracts from
    /// avares:// resources to a deterministic temp path on first access.
    /// Abstraction lives in EyeRest.Abstractions so the platform audio services
    /// can depend on it without pulling in Avalonia.
    /// </summary>
    public interface IBundledSoundCache
    {
        /// <summary>
        /// Returns a filesystem path to the bundled WAV for the given channel,
        /// or <c>null</c> if no bundled asset is registered for that channel —
        /// in which case <see cref="AudioServiceBase"/> falls back to the legacy
        /// platform-native named-sound path (e.g. NSSound "Glass" on macOS,
        /// SystemSounds.Beep on Windows). Idempotent — repeated calls return
        /// the same path without re-extraction.
        /// </summary>
        string? GetPath(AudioChannel channel);
    }
}
