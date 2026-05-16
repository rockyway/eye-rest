using EyeRest.Models;
using EyeRest.Services;
using FluentAssertions;
using Xunit;

namespace EyeRest.Tests.Avalonia.Audio;

public class ConfigurationMigrationTests
{
    [Fact]
    public void Migrate_LegacyTrueBools_MapTo_DefaultSource()
    {
        var legacyJson = """
        {
          "Meta": { "SchemaVersion": 1 },
          "EyeRest": { "StartSoundEnabled": true, "EndSoundEnabled": false },
          "Break":   { "StartSoundEnabled": true, "EndSoundEnabled": true  },
          "Audio":   { "Enabled": true, "Volume": 50, "CustomSoundPath": "/tmp/my.wav" }
        }
        """;
        var cfg = ConfigurationMigrator.MigrateFromJson(legacyJson);

        cfg.Meta!.SchemaVersion.Should().Be(2);
        cfg.EyeRest.StartAudio.Source.Should().Be(AudioChannelSource.Default);
        cfg.EyeRest.EndAudio.Source.Should().Be(AudioChannelSource.Off);
        cfg.Break.StartAudio.Source.Should().Be(AudioChannelSource.Default);
        cfg.Break.EndAudio.Source.Should().Be(AudioChannelSource.Default);

        // Legacy global CustomSoundPath copied to every channel whose legacy bool was true,
        // but Source remains Default — user must explicitly switch to activate.
        cfg.EyeRest.StartAudio.CustomFilePath.Should().Be("/tmp/my.wav");
        cfg.EyeRest.EndAudio.CustomFilePath.Should().BeNull();
        cfg.Break.StartAudio.CustomFilePath.Should().Be("/tmp/my.wav");
        cfg.Break.EndAudio.CustomFilePath.Should().Be("/tmp/my.wav");
    }

    [Fact]
    public void Migrate_AlreadyV2_IsIdempotent()
    {
        var v2Json = """
        {
          "Meta": { "SchemaVersion": 2 },
          "EyeRest": { "StartAudio": { "Source": "File", "CustomFilePath": "/x.wav" }, "EndAudio": { "Source": "Default" } },
          "Break":   { "StartAudio": { "Source": "Default" }, "EndAudio": { "Source": "Url", "Url": "https://example.com" } },
          "Audio":   { "Enabled": true, "Volume": 50 }
        }
        """;
        var cfg = ConfigurationMigrator.MigrateFromJson(v2Json);
        cfg.Meta!.SchemaVersion.Should().Be(2);
        cfg.EyeRest.StartAudio.Source.Should().Be(AudioChannelSource.File);
        cfg.EyeRest.StartAudio.CustomFilePath.Should().Be("/x.wav");
        cfg.Break.EndAudio.Source.Should().Be(AudioChannelSource.Url);
        cfg.Break.EndAudio.Url.Should().Be("https://example.com");
    }

    [Fact]
    public void Migrate_NewerSchemaVersion_Throws()
    {
        var futureJson = """{ "Meta": { "SchemaVersion": 99 } }""";
        var act = () => ConfigurationMigrator.MigrateFromJson(futureJson);
        act.Should().Throw<InvalidOperationException>().WithMessage("*SchemaVersion*99*");
    }

    [Fact]
    public void Migrate_MissingMeta_TreatedAsV1()
    {
        var ancientJson = """
        {
          "EyeRest": { "StartSoundEnabled": true, "EndSoundEnabled": true },
          "Break":   { "StartSoundEnabled": false, "EndSoundEnabled": false },
          "Audio":   { "Enabled": true, "Volume": 75 }
        }
        """;
        var cfg = ConfigurationMigrator.MigrateFromJson(ancientJson);
        cfg.Meta!.SchemaVersion.Should().Be(2);
        cfg.EyeRest.StartAudio.Source.Should().Be(AudioChannelSource.Default);
        cfg.EyeRest.EndAudio.Source.Should().Be(AudioChannelSource.Default);
        cfg.Break.StartAudio.Source.Should().Be(AudioChannelSource.Off);
        cfg.Break.EndAudio.Source.Should().Be(AudioChannelSource.Off);
        cfg.Audio.Enabled.Should().BeTrue();
        cfg.Audio.Volume.Should().Be(75);
    }

    [Fact]
    public void Migrate_LegacyAllSoundsDisabled_NoCustomPathLeaks()
    {
        var legacyJson = """
        {
          "Meta": { "SchemaVersion": 1 },
          "EyeRest": { "StartSoundEnabled": false, "EndSoundEnabled": false },
          "Break":   { "StartSoundEnabled": false, "EndSoundEnabled": false },
          "Audio":   { "Enabled": true, "Volume": 50, "CustomSoundPath": "/tmp/my.wav" }
        }
        """;
        var cfg = ConfigurationMigrator.MigrateFromJson(legacyJson);
        // Path is NOT copied to any channel since every channel was disabled.
        cfg.EyeRest.StartAudio.CustomFilePath.Should().BeNull();
        cfg.EyeRest.EndAudio.CustomFilePath.Should().BeNull();
        cfg.Break.StartAudio.CustomFilePath.Should().BeNull();
        cfg.Break.EndAudio.CustomFilePath.Should().BeNull();
    }
}
