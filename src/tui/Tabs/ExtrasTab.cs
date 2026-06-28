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
/// variant cores) and installs the selected one via CoresService.GetPocketExtra on a background
/// task — the same install path the classic menu used.
/// </summary>
public sealed class ExtrasTab : FrameView
{
    public ExtrasTab(TuiContext context)
    {
        Title = "Extras";

        var extras = ServiceHelper.CoresService.PocketExtrasList ?? new List<PocketExtra>();

        var hint = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Text = "Pocket Extras — select one and Install.  (↑/↓ move)"
        };

        var list = new ListView
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(2),
            CanFocus = true
        };

        list.SetSource(new ObservableCollection<string>(
            extras.Select(x => $"[{TypeLabel(x.type)}] {DisplayName(x)}")));
        list.VerticalScrollBar.VisibilityMode = ScrollBarVisibilityMode.Auto;

        var install = new Button
        {
            X = 1,
            Y = Pos.AnchorEnd(1),
            Text = "_Install Selected"
        };

        install.Accepting += (_, e) =>
        {
            e.Handled = true;

            int? index = list.SelectedItem;

            if (!index.HasValue || index.Value < 0 || index.Value >= extras.Count)
            {
                TuiApp.PostStatus("No Pocket Extra selected.");
                return;
            }

            PocketExtra extra = extras[index.Value];
            string label = DisplayName(extra);

            context.RunBackground(install, () =>
            {
                TuiApp.PostStatus($"Installing {label}...");
                ServiceHelper.CoresService.GetPocketExtra(extra, ServiceHelper.UpdateDirectory, true);
                ServiceHelper.CoresService.RefreshLocalCores();
                ServiceHelper.CoresService.RefreshInstalledCores();
                TuiApp.PostStatus($"{label} installed.");
            });
        };

        Add(hint);
        Add(list);
        Add(install);
    }

    // additional_assets entries often have no name; the classic menu falls back to the first
    // core identifier, so mirror that.
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
