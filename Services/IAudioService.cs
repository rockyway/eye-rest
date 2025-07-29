using System.Threading.Tasks;

namespace EyeRest.Services
{
    public interface IAudioService
    {
        Task PlayEyeRestStartSound();
        Task PlayEyeRestEndSound();
        Task PlayBreakWarningSound();
        bool IsAudioEnabled { get; }
    }
}