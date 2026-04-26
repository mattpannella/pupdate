using FluentAssertions;
using Pannella.Models.OpenFPGA_Cores_Inventory.V3;
using Pannella.Services;
using Pannella.Tests.Fixtures;

namespace Pannella.Tests.Integration;

public class CoresServiceInstallTests : IClassFixture<TempDirectoryFixture>
{
    private readonly TempDirectoryFixture _temp;

    public CoresServiceInstallTests(TempDirectoryFixture temp)
    {
        _temp = temp;
    }

    private CoresService BuildBare()
    {
        string installPath = Path.Combine(_temp.Path, "pocket-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(installPath);
        return new CoresService(installPath, settingsService: null, archiveService: null, assetsService: null);
    }

    [Fact]
    public void Install_CoreWithNullRepository_ReturnsFalse()
    {
        // Pin behavior: a manually-installed core (repository == null) is skipped.
        var svc = BuildBare();
        var core = new Core { id = "manual.core", repository = null, platform_id = "manual" };

        svc.Install(core).Should().BeFalse();
    }
}
