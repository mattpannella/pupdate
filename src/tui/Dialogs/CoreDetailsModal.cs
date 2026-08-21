using System.Collections.Generic;
using Pannella.Helpers;
using Pannella.Models.OpenFPGA_Cores_Inventory.V3;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// Read-only details for a single core: metadata plus clickable GitHub-repo and funding
/// <see cref="Link"/>s (which open the default browser). Opened from the Cores tab by pressing Enter
/// or clicking a row. Dismissed with Close / Esc.
/// </summary>
public static class CoreDetailsModal
{
    public static void Show(Core core)
    {
        if (core == null)
        {
            return;
        }

        var dialog = new Dialog
        {
            Title = core.ToString(),
            Width = Dim.Percent(70),
            Height = Dim.Percent(70)
        };

        View previous = null;

        void AddRow(View row)
        {
            row.X = 1;
            row.Y = previous == null ? 0 : Pos.Bottom(previous);
            dialog.Add(row);
            previous = row;
        }

        void AddText(string text) =>
            AddRow(new Label { Text = text ?? string.Empty, Width = Dim.Fill(), Height = 1, CanFocus = false });

        void AddLink(string text, string url) =>
            AddRow(new Link { Text = text, Url = url });

        var repo = core.repository;

        if (repo != null && !string.IsNullOrEmpty(repo.owner))
        {
            AddText($"Author: {repo.owner}");
        }

        string platform = FormatPlatform(core.platform);

        if (!string.IsNullOrEmpty(platform))
        {
            AddText($"Platform: {platform}");
        }

        if (!string.IsNullOrEmpty(core.version) || !string.IsNullOrEmpty(core.release_date))
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(core.version))
            {
                parts.Add($"Version: {core.version}");
            }

            if (!string.IsNullOrEmpty(core.release_date))
            {
                parts.Add($"Released: {core.release_date}");
            }

            AddText(string.Join("   ", parts));
        }

        string license = $"License required: {(core.requires_license ? "yes" : "no")}";

        if (ServiceHelper.CoresService.IsAiOverThreshold(core.id))
        {
            license += "   ·   AI core";
        }

        AddText(license);

        AddText(string.Empty);

        if (repo != null && !string.IsNullOrEmpty(repo.owner) && !string.IsNullOrEmpty(repo.name))
        {
            AddText("GitHub repository:");
            AddLink($"github.com/{repo.owner}/{repo.name}", $"https://github.com/{repo.owner}/{repo.name}");

            AddText(string.Empty);

            var funding = repo.funding?.GetLinks();

            if (funding is { Count: > 0 })
            {
                AddText("Funding:");

                foreach (string url in funding)
                {
                    AddLink(url, url);
                }
            }
            else
            {
                AddText("Funding: none listed");
            }
        }
        else
        {
            AddText("Local core — not in the online inventory (no repository or funding info).");
        }

        var close = new Button { Text = "_Close" };
        close.Accepting += (_, e) =>
        {
            e.Handled = true;
            TuiHost.RequestStop();
        };

        dialog.AddButton(close);

        TuiHost.Run(dialog);
    }

    private static string FormatPlatform(Platform platform)
    {
        if (platform == null)
        {
            return null;
        }

        var parts = new List<string>();

        if (!string.IsNullOrEmpty(platform.name))
        {
            parts.Add(platform.name);
        }

        if (!string.IsNullOrEmpty(platform.manufacturer))
        {
            parts.Add(platform.manufacturer);
        }

        if (platform.year > 0)
        {
            parts.Add(platform.year.ToString());
        }

        return parts.Count > 0 ? string.Join(" · ", parts) : null;
    }
}
