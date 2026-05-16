using System;

namespace EyeRest.Models;

/// <summary>
/// Thrown by <c>ConfigurationMigrator</c> when a config file's
/// <c>Meta.SchemaVersion</c> is newer than the running binary supports.
/// This is the refuse-to-load guard against the stale-binary corruption
/// pattern documented in CLAUDE.md (Mar 2026 LaunchAgent net10.0 incident):
/// a newer binary may have written this file, and a stale binary that
/// silently fell back to defaults would clobber the user's newer config
/// on the next save.
///
/// <c>ConfigurationService.LoadConfigurationAsync</c> must catch this
/// specifically and rethrow — never call <c>SaveConfigurationAsync</c>
/// or fall back to <c>GetDefaultConfiguration()</c>.
/// </summary>
public sealed class SchemaVersionTooNewException : Exception
{
    public int FileSchemaVersion { get; }
    public int SupportedSchemaVersion { get; }

    public SchemaVersionTooNewException(int fileSchemaVersion, int supportedSchemaVersion)
        : base($"Config SchemaVersion={fileSchemaVersion} is newer than supported version "
               + $"{supportedSchemaVersion}. A newer EyeRest binary may have written this file. "
               + "Refusing to load to prevent stale-binary corruption.")
    {
        FileSchemaVersion = fileSchemaVersion;
        SupportedSchemaVersion = supportedSchemaVersion;
    }
}
