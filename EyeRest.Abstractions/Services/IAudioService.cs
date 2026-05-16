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
        ///
        /// <para><b>Platform contract (M2 state, closed in M3):</b></para>
        /// <para>The returned <see cref="Task"/> completes when audio playback finishes on
        /// Windows (synchronous <c>SoundPlayer.PlaySync</c>). On <b>macOS</b> with
        /// <see cref="AudioChannelSource.File"/>, the Task currently resolves immediately
        /// after <c>NSSound.play</c> returns — playback then continues asynchronously on
        /// the system audio queue. M3 will wire <c>NSSoundDelegate sound:didFinishPlaying:</c>
        /// to a <c>TaskCompletionSource</c> so the macOS File path completes deterministically.
        /// Until M3, back-to-back File-mode plays on macOS can overlap natively despite the
        /// service-level <see cref="System.Threading.SemaphoreSlim"/> serialization.</para>
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
