using System.Threading;
using System.Threading.Tasks;
using EyeRest.Models;

namespace EyeRest.Services
{
    /// <summary>
    /// BL-002: channels addressable by the new <c>PlayChannelAsync</c> entry point.
    /// </summary>
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

        /// <summary>
        /// BL-002 channel-aware entry point. Dispatches to a bundled default sound, a
        /// custom file path, or a URL-in-browser action based on <paramref name="config"/>'s
        /// <c>Source</c>. Honors <see cref="IsAudioEnabled"/> for audio sources only —
        /// URL mode opens the browser regardless of the global audio mute.
        /// </summary>
        Task PlayChannelAsync(
            AudioChannel channel,
            AudioChannelConfig config,
            CancellationToken cancellationToken = default);

        // Existing methods preserved as adapter overloads. Implementations may now
        // delegate to PlayChannelAsync using the appropriate channel config.
        Task PlayEyeRestStartSound();
        Task PlayEyeRestEndSound();
        Task PlayBreakWarningSound();
        Task PlayBreakStartSound();
        Task PlayBreakEndSound();
        Task PlayCustomSoundTestAsync();
        Task TestEyeRestAudioAsync();
    }
}
