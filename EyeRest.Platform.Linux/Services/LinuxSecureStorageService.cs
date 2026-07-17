using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    /// <summary>
    /// Linux implementation of <see cref="ISecureStorageService"/>.
    /// Stores key-value pairs as JSON at ~/.config/EyeRest/secure-storage.json with
    /// 0600 permissions (owner read/write only). This is the storage for the
    /// supporter license key — user-scoped file permissions match the protection
    /// level of typical Linux app credentials; a libsecret/Keyring backend can be
    /// swapped in later without changing the contract.
    /// </summary>
    public class LinuxSecureStorageService : ISecureStorageService
    {
        private readonly ILogger<LinuxSecureStorageService> _logger;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly string _storePath;

        public LinuxSecureStorageService(ILogger<LinuxSecureStorageService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _storePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EyeRest", "secure-storage.json");
        }

        public async Task SetAsync(string key, string value)
        {
            ArgumentException.ThrowIfNullOrEmpty(key);

            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var store = await LoadStoreAsync().ConfigureAwait(false);
                store[key] = value;
                await SaveStoreAsync(store).ConfigureAwait(false);
                _logger.LogDebug("Secure storage: stored value for key {Key}", key);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<string?> GetAsync(string key)
        {
            ArgumentException.ThrowIfNullOrEmpty(key);

            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var store = await LoadStoreAsync().ConfigureAwait(false);
                return store.TryGetValue(key, out var value) ? value : null;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task RemoveAsync(string key)
        {
            ArgumentException.ThrowIfNullOrEmpty(key);

            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var store = await LoadStoreAsync().ConfigureAwait(false);
                if (store.Remove(key))
                {
                    await SaveStoreAsync(store).ConfigureAwait(false);
                    _logger.LogDebug("Secure storage: removed key {Key}", key);
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task<Dictionary<string, string>> LoadStoreAsync()
        {
            try
            {
                if (!File.Exists(_storePath)) return new Dictionary<string, string>();
                var json = await File.ReadAllTextAsync(_storePath).ConfigureAwait(false);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                       ?? new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Secure storage file unreadable — starting from empty store");
                return new Dictionary<string, string>();
            }
        }

        private async Task SaveStoreAsync(Dictionary<string, string> store)
        {
            var dir = Path.GetDirectoryName(_storePath)!;
            Directory.CreateDirectory(dir);

            // Write to a temp file with restrictive permissions before it holds data,
            // then atomically move it over the store.
            var tmpPath = _storePath + ".tmp";
            await File.WriteAllTextAsync(tmpPath, JsonSerializer.Serialize(store)).ConfigureAwait(false);
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                File.SetUnixFileMode(tmpPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            File.Move(tmpPath, _storePath, overwrite: true);
        }
    }
}
