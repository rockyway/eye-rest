using System;
using System.Text.Json;
using EyeRest.Models;

namespace EyeRest.Services;

/// <summary>
/// BL-002 config schema migration. Reads a JSON config string of any supported
/// schema version and returns a fully-populated <see cref="AppConfiguration"/>
/// in the current schema (v2). Older schemas are upgraded in-memory.
///
/// Schema versions:
///   1 = pre-BL002. EyeRestSettings/BreakSettings had StartSoundEnabled and
///       EndSoundEnabled bool toggles. AudioSettings had a single global
///       CustomSoundPath string.
///   2 = BL002. Per-channel AudioChannelConfig (Source + CustomFilePath + Url).
///       Legacy bools and global path are gone.
///
/// Migration is idempotent: a v2 input is returned unchanged (except that
/// SchemaVersion is normalized to <see cref="CurrentSchemaVersion"/>). A
/// schema version newer than <see cref="CurrentSchemaVersion"/> throws
/// <see cref="InvalidOperationException"/> — the refuse-to-save guard against
/// the stale-binary corruption pattern (see project lessons-learned, Mar 2026).
/// </summary>
public static class ConfigurationMigrator
{
    public const int CurrentSchemaVersion = 2;

    public static AppConfiguration MigrateFromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // ConfigurationService serializes with JsonNamingPolicy.CamelCase, so on-disk
        // property names are lowercase ('meta', 'schemaVersion'). JsonDocument's
        // TryGetProperty is case-sensitive, so we look up both casings.
        int version = 1;
        var meta = LookupCaseInsensitive(root, "Meta");
        if (meta.ValueKind == JsonValueKind.Object)
        {
            var v = LookupCaseInsensitive(meta, "SchemaVersion");
            if (v.ValueKind == JsonValueKind.Number)
                version = v.GetInt32();
        }

        if (version > CurrentSchemaVersion)
        {
            throw new SchemaVersionTooNewException(version, CurrentSchemaVersion);
        }

        var cfg = JsonSerializer.Deserialize<AppConfiguration>(json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
            }) ?? new AppConfiguration();

        if (version < 2)
        {
            ApplyV1ToV2(cfg, root);
        }

        cfg.Meta ??= new ConfigMetadata();
        cfg.Meta.SchemaVersion = CurrentSchemaVersion;
        return cfg;
    }

    private static void ApplyV1ToV2(AppConfiguration cfg, JsonElement root)
    {
        var eyeRest = LookupCaseInsensitive(root, "EyeRest");
        var brk     = LookupCaseInsensitive(root, "Break");
        var audio   = LookupCaseInsensitive(root, "Audio");

        bool erStart = ReadBool(eyeRest, "StartSoundEnabled", true);
        bool erEnd   = ReadBool(eyeRest, "EndSoundEnabled",   true);
        bool bkStart = ReadBool(brk,     "StartSoundEnabled", true);
        bool bkEnd   = ReadBool(brk,     "EndSoundEnabled",   true);
        string? legacyCustomPath = ReadString(audio, "CustomSoundPath");

        cfg.EyeRest.StartAudio = ToChannel(erStart, legacyCustomPath);
        cfg.EyeRest.EndAudio   = ToChannel(erEnd,   legacyCustomPath);
        cfg.Break.StartAudio   = ToChannel(bkStart, legacyCustomPath);
        cfg.Break.EndAudio     = ToChannel(bkEnd,   legacyCustomPath);
    }

    private static AudioChannelConfig ToChannel(bool legacyEnabled, string? legacyCustomPath)
    {
        var c = new AudioChannelConfig
        {
            Source = legacyEnabled ? AudioChannelSource.Default : AudioChannelSource.Off,
        };
        if (legacyEnabled && !string.IsNullOrWhiteSpace(legacyCustomPath))
        {
            // Copy the path forward (so the M4 file picker still shows the user's previous
            // choice) but leave Source as Default — explicit user action required to play
            // the custom file. Conservative per spec §4.
            c.CustomFilePath = legacyCustomPath;
        }
        return c;
    }

    // Case-insensitive property lookup — needed because the on-disk JSON uses
    // camelCase per JsonNamingPolicy.CamelCase, but the migrator's tests use
    // PascalCase literals for readability. We match either casing transparently.
    private static JsonElement LookupCaseInsensitive(JsonElement parent, string pascalName)
    {
        if (parent.ValueKind != JsonValueKind.Object) return default;
        if (parent.TryGetProperty(pascalName, out var v)) return v;
        var camelName = char.ToLowerInvariant(pascalName[0]) + pascalName.Substring(1);
        return parent.TryGetProperty(camelName, out v) ? v : default;
    }

    private static bool ReadBool(JsonElement node, string name, bool fallback)
    {
        var v = LookupCaseInsensitive(node, name);
        if (v.ValueKind == JsonValueKind.True) return true;
        if (v.ValueKind == JsonValueKind.False) return false;
        return fallback;
    }

    private static string? ReadString(JsonElement node, string name)
    {
        var v = LookupCaseInsensitive(node, name);
        return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }
}
