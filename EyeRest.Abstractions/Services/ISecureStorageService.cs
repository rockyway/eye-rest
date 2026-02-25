using System.Threading.Tasks;

namespace EyeRest.Services
{
    public interface ISecureStorageService
    {
        Task SetAsync(string key, string value);
        Task<string?> GetAsync(string key);
        Task RemoveAsync(string key);
    }
}
