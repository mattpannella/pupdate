using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Pannella.Helpers;
using Pannella.Models.Extras;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// Extras tab. Lists the available Pocket Extras (additional assets / combination platforms /
/// variant cores); Enter installs the highlighted one via CoresService.GetPocketExtra on a
/// background task. Consistent with the other list tabs — no loose button.
/// </summary>
public sealed class ExtrasTab : FrameView
{
    private readonly TuiContext context;
    private readonly List<PocketExtra> extras;
    private readonly ListView list;

    public ExtrasTab(TuiContext context)
    {
        this.context = context;
        Title = "Extras";

        extras = ServiceHelper.CoresService.PocketExtrasList ?? new List<PocketExtra>();

        var hint = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Text = "↑/↓ move · Enter installs the highlighted extra"
        };

        list = new MenuListView
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        list.SetSource(new ObservableCollection<string>(
            extras.Select(x => $"[{TypeLabel(x.type)}] {DisplayName(x)}")));

        list.Accepting += (_, e) =>
        {
            e.Handled = true;
            InstallSelected();
        };

        Add(hint);
        Add(list);
    }

    private void InstallSelected()
    {
        int? index = list.SelectedItem;

        if (!index.HasValue || index.Value < 0 || index.Value >= extras.Count)
        {
            TuiApp.PostStatus("No Pocket Extra selected.");
            return;
        }

        PocketExtra extra = extras[index.Value];
        string label = DisplayName(extra);

        context.RunBackground(null, () =>
        {
            TuiApp.PostStatus($"Installing {label}...");
            ServiceHelper.CoresService.GetPocketExtra(extra, ServiceHelper.UpdateDirectory, true);
            ServiceHelper.CoresService.RefreshLocalCores();
            ServiceHelper.CoresService.RefreshInstalledCores();
            TuiApp.PostStatus($"{label} installed.");
        });
    }

    // additional_assets entries often have no name; mirror the classic menu's fallback.
    private static string DisplayName(PocketExtra extra) =>
        !string.IsNullOrWhiteSpace(extra.name)
            ? extra.name
            : extra.core_identifiers is { Count: > 0 }
                ? $"extras for {extra.core_identifiers[0]}"
                : extra.id;

    private static string TypeLabel(PocketExtraType type) => type switch
    {
        PocketExtraType.additional_assets => "Assets",
        PocketExtraType.combination_platform => "Combo",
        PocketExtraType.variant_core => "Variant",
        _ => "Extra"
    };
}
