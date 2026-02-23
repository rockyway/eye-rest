using System.Threading.Tasks;

namespace EyeRest.Services
{
    public interface IAudioService
    {
        Task PlayEyeRestStartSound();
        Task PlayEyeRestEndSound();
        Task PlayBreakWarningSound();
        Task PlayBreakStartSound();  // Missing method for break popup start
        Task PlayBreakEndSound();    // Missing method for break popup end
        Task PlayCustomSoundTestAsync(); // For testing custom sound from UI
        Task TestEyeRestAudioAsync(); // For testing eye rest audio from UI
        bool IsAudioEnabled { get; }
    }
}