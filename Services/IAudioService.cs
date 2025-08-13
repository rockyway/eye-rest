using System.Threading.Tasks;

namespace EyeRest.Services
{
    public interface IAudioService
    {
        Task PlayEyeRestStartSound();
        Task PlayEyeRestEndSound();
        Task PlayBreakWarningSound();
        Task PlayCustomSoundTestAsync(); // For testing custom sound from UI
        bool IsAudioEnabled { get; }
    }
}