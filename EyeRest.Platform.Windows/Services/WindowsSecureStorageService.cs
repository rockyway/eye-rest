using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using EyeRest.Services;
using Microsoft.Extensions.Logging;

namespace EyeRest.Platform.Windows
{
    public class WindowsSecureStorageService : ISecureStorageService
    {
        private readonly ILogger<WindowsSecureStorageService> _logger;
        private readonly string _storePath;
        private Dictionary<string, string>? _cache;

        public WindowsSecureStorageService(ILogger<WindowsSecureStorageService> logger)
        {
            _logger = logger;
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _storePath = Path.Combine(appData, "EyeRest", "donor-state.dat");
        }

        public async Task SetAsync(string key, string value)
        {
            var store = await LoadStoreAsync();
            store[key] = value;
            await SaveStoreAsync(store);
        }

        public async Task<string?> GetAsync(string key)
        {
            var store = await LoadStoreAsync();
            return store.TryGetValue(key, out var value) ? value : null;
        }

        public async Task RemoveAsync(string key)
        {
            var store = await LoadStoreAsync();
            if (store.Remove(key))
                await SaveStoreAsync(store);
        }

        private async Task<Dictionary<string, string>> LoadStoreAsync()
        {
            if (_cache != null)
                return _cache;

            try
            {
                if (!File.Exists(_storePath))
                    return _cache = new Dictionary<string, string>();

                var encrypted = await File.ReadAllBytesAsync(_storePath);
                var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                var json = Encoding.UTF8.GetString(decrypted);
                _cache = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
                return _cache;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load secure storage, starting fresh");
                return _cache = new Dictionary<string, string>();
            }
        }

        private async Task SaveStoreAsync(Dictionary<string, string> store)
        {
            try
            {
                var dir = Path.GetDirectoryName(_storePath)!;
                Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(store);
                var bytes = Encoding.UTF8.GetBytes(json);
                var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
                await File.WriteAllBytesAsync(_storePath, encrypted);
                _cache = store;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save secure storage");
            }
        }
    }
}
