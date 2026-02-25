using System.Runtime.InteropServices;
using System.Text;
using EyeRest.Platform.macOS.Interop;
using EyeRest.Services;
using Microsoft.Extensions.Logging;

namespace EyeRest.Platform.macOS;

public class MacOSSecureStorageService : ISecureStorageService
{
    private const string ServiceName = "EyeRest";
    private readonly ILogger<MacOSSecureStorageService> _logger;

    public MacOSSecureStorageService(ILogger<MacOSSecureStorageService> logger)
    {
        _logger = logger;
    }

    public Task SetAsync(string key, string value)
    {
        try
        {
            var valueBytes = Encoding.UTF8.GetBytes(value);

            // Try to update first
            var query = BuildQuery(key);
            var update = Security.CreateMutableDictionary();
            var cfData = Security.CreateCFData(valueBytes);
            Security.CFDictionarySetValue(update, Security.kSecValueData, cfData);

            var status = Security.SecItemUpdate(query, update);
            Security.CFRelease(update);
            Security.CFRelease(cfData);

            if (status == Security.errSecItemNotFound)
            {
                // Item doesn't exist, add it
                Security.CFRelease(query);
                query = BuildQuery(key);
                var addData = Security.CreateCFData(valueBytes);
                Security.CFDictionarySetValue(query, Security.kSecValueData, addData);
                status = Security.SecItemAdd(query, out _);
                Security.CFRelease(addData);
            }

            Security.CFRelease(query);

            if (status != Security.errSecSuccess)
                _logger.LogWarning("Keychain set failed for key {Key} with status {Status}", key, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set keychain value for key {Key}", key);
        }

        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string key)
    {
        try
        {
            var query = BuildQuery(key);
            Security.CFDictionarySetValue(query, Security.kSecReturnData, Security.kCFBooleanTrue);
            Security.CFDictionarySetValue(query, Security.kSecMatchLimit, Security.kSecMatchLimitOne);

            var status = Security.SecItemCopyMatching(query, out var result);
            Security.CFRelease(query);

            if (status == Security.errSecSuccess && result != IntPtr.Zero)
            {
                var bytes = Security.ReadCFData(result);
                Security.CFRelease(result);
                if (bytes != null)
                    return Task.FromResult<string?>(Encoding.UTF8.GetString(bytes));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get keychain value for key {Key}", key);
        }

        return Task.FromResult<string?>(null);
    }

    public Task RemoveAsync(string key)
    {
        try
        {
            var query = BuildQuery(key);
            Security.SecItemDelete(query);
            Security.CFRelease(query);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove keychain value for key {Key}", key);
        }

        return Task.CompletedTask;
    }

    private static IntPtr BuildQuery(string account)
    {
        var dict = Security.CreateMutableDictionary();
        Security.CFDictionarySetValue(dict, Security.kSecClass, Security.kSecClassGenericPassword);

        var serviceStr = Foundation.CreateRetainedNSString(ServiceName);
        var accountStr = Foundation.CreateRetainedNSString(account);
        Security.CFDictionarySetValue(dict, Security.kSecAttrService, serviceStr);
        Security.CFDictionarySetValue(dict, Security.kSecAttrAccount, accountStr);

        return dict;
    }
}
