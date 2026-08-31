using System;
using System.Collections.Generic;
using System.Linq;
using Pannella.Helpers;
using Pannella.Models.OpenFPGA_Cores_Inventory.V3;
using Pannella.Services;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// Read-only details for a single core: metadata, its openFPGA AI check findings, plus clickable
/// GitHub-repo and funding <see cref="Link"/>s (which open the default browser). Opened from the
/// Cores tab by pressing Enter or clicking a row. The body scrolls (up/down/PgUp/PgDn), so a core
/// with a long AI breakdown or many funding links is never silently cut off. Dismissed with Close.
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
            Width = Dim.Percent(75),
            Height = Dim.Percent(80)
        };

        var body = new ScrollableBody { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };

        void AddText(string text)
        {
            body.Measure(text);
            body.AddRow(new Label { Text = text ?? string.Empty, CanFocus = false });
        }

        void AddLink(string text, string url)
        {
            body.Measure(text);
            body.AddRow(new Link { Text = text, Url = url });
        }

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

        AddText($"License required: {(core.requires_license ? "yes" : "no")}");

        AddText(string.Empty);
        AddAiCheck(core.id, AddText);
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
            AddText("Local core - not in the online inventory (no repository or funding info).");
        }

        body.Finish();
        dialog.Add(body);

        var close = new Button { Text = "_Close" };
        close.Accepting += (_, e) =>
        {
            e.Handled = true;
            TuiHost.RequestStop();
        };

        dialog.AddButton(close);

        // Focus the body so up/down scroll it straight away instead of sitting on the Close button.
        dialog.Initialized += (_, _) => TuiHost.AddTimeout(TimeSpan.Zero, () =>
        {
            body.SetFocus();
            return false;
        });

        TuiHost.Run(dialog);
    }

    // The openFPGA AI check report's findings for this core: the overall score against your
    // threshold, then each individual check with the evidence it recorded.
    private static void AddAiCheck(string identifier, Action<string> addText)
    {
        var report = ServiceHelper.CoresService.AiReport;

        if (report == null || !report.TryGetValue(identifier, out var entry) || entry == null)
        {
            addText("AI check: not scored");
            return;
        }

        int threshold = ServiceHelper.SettingsService.Config.ai_core_threshold;
        bool over = CoresService.ExceedsAiThreshold(entry.overall_score, threshold);

        addText($"AI check: {Math.Round(entry.overall_score * 100)}%   "
                + (over ? $"(over your {threshold}% threshold)" : $"(under your {threshold}% threshold)"));

        // The report can repeat one check per piece of evidence (e.g. four README hits); show a
        // single heading per distinct check with its evidence stacked underneath.
        var checks = entry.AllResults
            .GroupBy(item => ($"{CategoryLabel(item.Category)} · {item.Result.name}", item.Result.score?.ToString()));

        foreach (var check in checks)
        {
            addText($"  {check.Key.Item1} - {check.Key.Item2 ?? "no score"}");

            foreach (string line in check.SelectMany(item => item.Result.output ?? new List<string>()))
            {
                addText($"      {line}");
            }
        }

        if (entry.last_run > 0)
        {
            addText($"  last checked {DateTimeOffset.FromUnixTimeMilliseconds(entry.last_run).LocalDateTime:yyyy-MM-dd}");
        }
    }

    private static string CategoryLabel(string category) => category switch
    {
        "CommitsCheck" => "Commits",
        "ReadmeCheck" => "Readme",
        "ContributorsCheck" => "Contributors",
        _ => category
    };

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
