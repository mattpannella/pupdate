using FluentAssertions;
using Pannella.Helpers;
using Pannella.Models.OpenFPGA_Cores_Inventory.V3;
using Pannella.Services;
using Pannella.Tests.Fixtures;

namespace Pannella.Tests.Unit;

public class FirstRunCoreSelectionTests : IClassFixture<TempDirectoryFixture>
{
    private readonly TempDirectoryFixture _temp;

    public FirstRunCoreSelectionTests(TempDirectoryFixture temp)
    {
        _temp = temp;
    }

    private static Core MakeCore(string id, string category) =>
        new() { id = id, platform = category == null ? null : new Platform { id = id, category = category } };

    [Theory]
    [InlineData("Console", "Console")]
    [InlineData("handheld", "Handheld")]
    [InlineData("Arcade Multi", "Arcade Multi")]
    [InlineData("Music", "Other")]
    [InlineData(null, "Other")]
    public void TypeOf_FoldsMinorCategoriesIntoOther(string category, string expected)
    {
        FirstRunCoreSelection.TypeOf(MakeCore("a.b", category)).Should().Be(expected);
    }

    [Fact]
    public void Summarize_CountsByTypeInDisplayOrder()
    {
        var cores = new[]
        {
            MakeCore("1", "Arcade"), MakeCore("2", "Arcade"), MakeCore("3", "Console"),
            MakeCore("4", "Tools"), MakeCore("5", "Handheld")
        };

        FirstRunCoreSelection.Summarize(cores).Should().Equal(
            ("Console", 1), ("Handheld", 1), ("Arcade", 2), ("Other", 1));
    }

    [Fact]
    public void ShouldEnable_FollowsTheChoice()
    {
        var console = MakeCore("x.nes", "Console");
        var arcade = MakeCore("x.pacman", "Arcade");
        bool IsInstalled(string id) => id == "x.pacman";
        var types = new HashSet<string> { "Console" };

        FirstRunCoreSelection.ShouldEnable(console, FirstRunChoice.Everything, IsInstalled).Should().BeTrue();
        FirstRunCoreSelection.ShouldEnable(console, FirstRunChoice.None, IsInstalled).Should().BeFalse();
        FirstRunCoreSelection.ShouldEnable(arcade, FirstRunChoice.KeepInstalled, IsInstalled).Should().BeTrue();
        FirstRunCoreSelection.ShouldEnable(console, FirstRunChoice.KeepInstalled, IsInstalled).Should().BeFalse();
        FirstRunCoreSelection.ShouldEnable(console, FirstRunChoice.ByType, IsInstalled, types).Should().BeTrue();
        FirstRunCoreSelection.ShouldEnable(arcade, FirstRunChoice.ByType, IsInstalled, types).Should().BeFalse();
        FirstRunCoreSelection.ShouldEnable(console, FirstRunChoice.ByType, IsInstalled).Should().BeFalse();
    }

    [Fact]
    public void HasCoreSelections_FalseOnFreshInstall_TrueOnceAnyCoreIsChosen()
    {
        string dir = Directory.CreateDirectory(Path.Combine(_temp.Path, "firstrun-" + Guid.NewGuid().ToString("N"))).FullName;
        var svc = new SettingsService(dir, new List<Core> { MakeCore("a.one", "Console"), MakeCore("a.two", "Arcade") });

        svc.HasCoreSelections.Should().BeFalse();
        svc.GetMissingCores().Should().HaveCount(2);

        svc.DisableCore("a.two");

        svc.HasCoreSelections.Should().BeTrue();
    }
}
