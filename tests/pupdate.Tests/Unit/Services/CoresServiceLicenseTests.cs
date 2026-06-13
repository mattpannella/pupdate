using FluentAssertions;
using Pannella.Models.OpenFPGA_Cores_Inventory.V3;
using Pannella.Services;
using Pannella.Tests.Fixtures;

namespace Pannella.Tests.Unit.Services;

public class CoresServiceLicenseTests : IClassFixture<TempDirectoryFixture>
{
    private readonly TempDirectoryFixture _temp;

    public CoresServiceLicenseTests(TempDirectoryFixture temp)
    {
        _temp = temp;
    }

    private (string installPath, CoresService svc) NewSvc()
    {
        string installPath = Path.Combine(_temp.Path, "pocket-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(installPath);
        var svc = new CoresService(installPath, settingsService: null, archiveService: null, assetsService: null);
        return (installPath, svc);
    }

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static void WriteBytes(string path, byte[] content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }

    // ---------- RequiresLicense ----------

    [Fact]
    public void RequiresLicense_NoUpdatersJson_ReturnsFalse()
    {
        var (_, svc) = NewSvc();

        var (requires, slotId, platformIndex, filename) = svc.RequiresLicense("nope");

        requires.Should().BeFalse();
        slotId.Should().BeNull();
        platformIndex.Should().Be(0);
        filename.Should().BeNull();
    }

    [Fact]
    public void RequiresLicense_UpdatersHasNoLicense_ReturnsFalse()
    {
        var (install, svc) = NewSvc();
        WriteFile(Path.Combine(install, "Cores", "x.y", "updaters.json"),
            """{ "previous": [] }""");

        svc.RequiresLicense("x.y").Item1.Should().BeFalse();
    }

    [Fact]
    public void RequiresLicense_LicenseFilenameMatchesDataSlot_ReturnsTrue()
    {
        var (install, svc) = NewSvc();
        WriteFile(Path.Combine(install, "Cores", "x.y", "updaters.json"),
            """{ "license": { "filename": "key.bin" } }""");
        WriteFile(Path.Combine(install, "Cores", "x.y", "data.json"),
            """
            {
              "data": {
                "data_slots": [
                  { "id": "100", "name": "key", "filename": "key.bin", "parameters": "0x0" }
                ]
              }
            }
            """);

        var (requires, slotId, platformIndex, filename) = svc.RequiresLicense("x.y");

        requires.Should().BeTrue();
        slotId.Should().Be("100");
        platformIndex.Should().Be(0);
        filename.Should().Be("key.bin");
    }

    [Fact]
    public void RequiresLicense_NoMatchingDataSlot_ReturnsFalse()
    {
        var (install, svc) = NewSvc();
        WriteFile(Path.Combine(install, "Cores", "x.y", "updaters.json"),
            """{ "license": { "filename": "key.bin" } }""");
        WriteFile(Path.Combine(install, "Cores", "x.y", "data.json"),
            """
            {
              "data": {
                "data_slots": [
                  { "id": "200", "name": "rom", "filename": "rom.bin", "parameters": "0x0" }
                ]
              }
            }
            """);

        svc.RequiresLicense("x.y").Item1.Should().BeFalse(
            "license filename doesn't match any data slot filename");
    }

    [Fact]
    public void RequiresLicense_PlatformIndexExtractedFromParametersBits()
    {
        // GetPlatformIdIndex pulls bits 24-25 from parameters. 0x01000000 sets bit 24 → index 1.
        var (install, svc) = NewSvc();
        WriteFile(Path.Combine(install, "Cores", "x.y", "updaters.json"),
            """{ "license": { "filename": "key.bin" } }""");
        WriteFile(Path.Combine(install, "Cores", "x.y", "data.json"),
            """
            {
              "data": {
                "data_slots": [
                  { "id": "5", "name": "key", "filename": "key.bin", "parameters": "0x01000000" }
                ]
              }
            }
            """);

        svc.RequiresLicense("x.y").Item3.Should().Be(1);
    }

    // ---------- CopyLicense ----------

    [Fact]
    public void CopyLicense_LicenseFilePresent_CopiesToAssetsCommonDir()
    {
        var (install, svc) = NewSvc();
        // Core has platform_ids = ["nes"]; index 0 picks "nes".
        WriteFile(Path.Combine(install, "Cores", "agg23.NES", "core.json"),
            """{ "core": { "magic": "x", "metadata": { "platform_ids": ["nes"] }, "framework": { "version_required":"0","sleep_supported":false } } }""");
        WriteBytes(Path.Combine(install, "Licenses", "key.bin"), new byte[] { 0xDE, 0xAD });

        var core = new Core
        {
            id = "agg23.NES",
            license_slot_filename = "key.bin",
            license_slot_platform_id_index = 0
        };

        svc.CopyLicense(core);

        string expected = Path.Combine(install, "Assets", "nes", "common", "key.bin");
        File.Exists(expected).Should().BeTrue();
        File.ReadAllBytes(expected).Should().Equal(new byte[] { 0xDE, 0xAD });
    }

    [Fact]
    public void CopyLicense_LicenseFileMissing_DoesNotCreateTarget_NoThrow()
    {
        var (install, svc) = NewSvc();
        WriteFile(Path.Combine(install, "Cores", "agg23.NES", "core.json"),
            """{ "core": { "magic": "x", "metadata": { "platform_ids": ["nes"] }, "framework": { "version_required":"0","sleep_supported":false } } }""");
        // Note: no Licenses/key.bin on disk.

        var core = new Core
        {
            id = "agg23.NES",
            license_slot_filename = "key.bin",
            license_slot_platform_id_index = 0
        };

        var act = () => svc.CopyLicense(core);
        act.Should().NotThrow();

        File.Exists(Path.Combine(install, "Assets", "nes", "common", "key.bin"))
            .Should().BeFalse();
        // The Assets/nes/common dir IS created (CopyLicense unconditionally creates it
        // before checking for the key file). Pin this current behavior.
        Directory.Exists(Path.Combine(install, "Assets", "nes", "common"))
            .Should().BeTrue("the target dir is created unconditionally before the key check");
    }

    [Fact]
    public void CopyLicense_PlatformIndexSelectsRightPlatformId()
    {
        var (install, svc) = NewSvc();
        // Multi-platform core; index 1 picks "snes".
        WriteFile(Path.Combine(install, "Cores", "multi.core", "core.json"),
            """{ "core": { "magic": "x", "metadata": { "platform_ids": ["nes","snes","gb"] }, "framework": { "version_required":"0","sleep_supported":false } } }""");
        WriteBytes(Path.Combine(install, "Licenses", "key.bin"), new byte[] { 0x42 });

        var core = new Core
        {
            id = "multi.core",
            license_slot_filename = "key.bin",
            license_slot_platform_id_index = 1
        };

        svc.CopyLicense(core);

        File.Exists(Path.Combine(install, "Assets", "snes", "common", "key.bin")).Should().BeTrue();
        File.Exists(Path.Combine(install, "Assets", "nes", "common", "key.bin")).Should().BeFalse();
        File.Exists(Path.Combine(install, "Assets", "gb", "common", "key.bin")).Should().BeFalse();
    }

    [Fact]
    public void CopyLicense_OverwritesExistingTargetFile()
    {
        var (install, svc) = NewSvc();
        WriteFile(Path.Combine(install, "Cores", "agg23.NES", "core.json"),
            """{ "core": { "magic": "x", "metadata": { "platform_ids": ["nes"] }, "framework": { "version_required":"0","sleep_supported":false } } }""");
        WriteBytes(Path.Combine(install, "Licenses", "key.bin"), new byte[] { 0xAA });
        WriteBytes(Path.Combine(install, "Assets", "nes", "common", "key.bin"), new byte[] { 0xBB });

        var core = new Core
        {
            id = "agg23.NES",
            license_slot_filename = "key.bin",
            license_slot_platform_id_index = 0
        };

        svc.CopyLicense(core);

        File.ReadAllBytes(Path.Combine(install, "Assets", "nes", "common", "key.bin"))
            .Should().Equal(new byte[] { 0xAA });
    }
}
