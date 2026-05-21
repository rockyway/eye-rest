using System.Threading.Tasks;
using EyeRest.Models;

namespace EyeRest.Services;

public interface IAudioRecentsService
{
    Task<AudioRecents> LoadAsync();
    Task SaveAsync(AudioRecents recents);
}
