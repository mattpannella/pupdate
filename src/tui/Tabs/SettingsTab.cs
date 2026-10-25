using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Pannella.Helpers;
using Pannella.Models.Settings;
using Pannella.Services;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// Settings tab: pupdate's own configuration (Pocket-side operations live on the Pocket Setup tab).
/// An ordered catalog of groups, each holding toggles and non-toggle rows (paths, tokens, the AI
/// threshold, the display-modes default) that show their current value inline. Changes stay PENDING
/// until Save - or until the tab is left, which commits automatically - so the expensive
/// <see cref="ServiceHelper.ReloadSettings"/> runs once per commit rather than once per keystroke.
/// </summary>
public sealed class SettingsTab : FrameView
{
    private const string Hint = "↑/↓ move · Space/Enter changes a row · Save commits";

    private readonly TuiContext context;
    private readonly List<SettingsRow> rows = new();
    private readonly HashSet<string> catalogued = new();
    private readonly ObservableCollection<string> labels = new();
    private readonly SettingsListView list;
    private readonly Label hint;
    private bool dirty;

    public SettingsTab(TuiContext context)
    {
        this.context = context;
        Title = "Settings";

        hint = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Text = Hint
        };

        list = new SettingsListView
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = Dim.Fill(1) // leave the bottom row for the Save button
        };

        var saveButton = new Button
        {
            X = Pos.Center(),
            Y = Pos.AnchorEnd(1),
            Text = "_Save"
        };

        saveButton.Accepting += (_, e) =>
        {
            e.Handled = true;
            Commit();
        };

        BuildCatalog();

        list.SetRows(rows);
        list.SetSource(labels);
        list.SetActivate(ActivateRow);

        Refresh();

        Add(hint);
        Add(list);
        Add(saveButton);
    }

    // ── Catalog ──────────────────────────────────────────────────────────────────────────────

    private void BuildCatalog()
    {
        AddGroup("Update All");
        AddToggle(nameof(Config.download_assets));
        AddToggle(nameof(Config.only_check_updated_core_assets));
        AddToggle(nameof(Config.download_firmware));
        AddToggle(nameof(Config.build_instance_jsons));
        AddToggle(nameof(Config.preserve_platforms_folder));
        AddToggle(nameof(Config.delete_skipped_cores));
        AddToggle(nameof(Config.backup_saves));
        AddToggle(nameof(Config.update_plugins));
        AddValue(nameof(Config.backup_saves_location), "Backup saves location",
            TextEditor("Backup Saves Location", "Folder for save/memory backups (blank = \"Backups\"):",
                input => string.IsNullOrWhiteSpace(input) ? "Backups" : input.Trim()),
            PathDisplay("Backups"));
        AddValue(nameof(Config.save_states_to_keep), "Save states to keep per game",
            NumberEditor("Save States to Keep", "Prune Save States keeps this many of the newest save states per game:", 1, 999),
            value => $"{value}");

        AddGroup("Assets & Downloads");
        AddToggle(nameof(Config.crc_check));
        AddToggle(nameof(Config.skip_alternative_assets));
        AddToggle(nameof(Config.concurrent_downloads));
        AddToggle(nameof(Config.use_custom_archive));
        AddToggle(nameof(Config.cache_archive_files));
        AddValue(nameof(Config.archive_cache_location), "Archive cache location",
            TextEditor("Archive Cache Location", "Folder for cached archive files (blank = default):", Blank),
            PathDisplay("(default)"));
        AddValue(nameof(Config.temp_directory), "Temp directory",
            TextEditor("Temp Directory", "Temp directory (blank = system default):", Blank),
            PathDisplay("(system default)"));

        AddGroup("Core Filtering");
        AddToggle(nameof(Config.no_analogizer_variants));
        AddToggle(nameof(Config.filter_ai_cores));
        AddValue(nameof(Config.ai_core_threshold), "AI score threshold",
            NumberEditor("AI Filter Threshold", "Cores with an AI score over this percentage are hidden (0-100):", 0, 100),
            value => $"{value}%");

        AddGroup("Display Modes");
        AddValue(nameof(Config.display_modes_option), "Merge or overwrite default",
            ChoiceEditor("Display Modes Default", "When applying display modes, by default:",
                ("merge", "Merge with existing"), ("overwrite", "Overwrite existing"), ("ask", "Ask each time")),
            value => value as string is { Length: > 0 } option ? option : "ask");
        AddToggle(nameof(Config.add_display_mode_description_to_video_json));

        AddGroup("Accounts");
        AddValue(nameof(Config.github_token), "GitHub token",
            TextEditor("GitHub Token", "Enter your GitHub personal access token (leave blank to clear):",
                input => string.IsNullOrWhiteSpace(input) ? string.Empty : input.Trim(), secret: true),
            SecretDisplay);

        AddGroup("Application & Interface");
        AddToggle(nameof(Config.use_tui));
        AddToggle(nameof(Config.show_menu_descriptions));
        AddToggle(nameof(Config.auto_install_updates));

        AddGroup("Advanced");
        AddToggle(nameof(Config.use_local_pocket_extras));
        AddToggle(nameof(Config.use_local_pocket_library_images));
        AddToggle(nameof(Config.use_local_display_modes));
        AddToggle(nameof(Config.use_local_plugins_catalog));

        AddUncataloguedToggles();
    }

    // Safety net: any [Description] bool added to Config that this catalog doesn't place still shows
    // up, so a new setting is never silently missing from the TUI.
    private void AddUncataloguedToggles()
    {
        var missing = typeof(Config).GetProperties()
            .Where(property => property.PropertyType == typeof(bool)
                               && property.GetCustomAttribute<DescriptionAttribute>() != null
                               && !catalogued.Contains(property.Name))
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        AddGroup("Other");

        foreach (var property in missing)
        {
            AddToggle(property.Name);
        }
    }

    private void AddGroup(string title) => rows.Add(new SettingsHeaderRow(title));

    private void AddToggle(string name)
    {
        var property = typeof(Config).GetProperty(name);

        catalogued.Add(name);
        rows.Add(new SettingsToggleRow(property,
            property!.GetCustomAttribute<DescriptionAttribute>()?.Description ?? name));
    }

    private void AddValue(string name, string label, Func<SettingsValueRow, bool> edit, Func<object, string> display)
    {
        catalogued.Add(name);
        rows.Add(new SettingsValueRow(typeof(Config).GetProperty(name), label, edit, display));
    }

    // ── Editors & displays ───────────────────────────────────────────────────────────────────

    private static string Blank(string input) => string.IsNullOrWhiteSpace(input) ? null : input.Trim();

    private static Func<object, string> PathDisplay(string fallback) =>
        value => value as string is { Length: > 0 } path ? path : fallback;

    private static string SecretDisplay(object value) =>
        string.IsNullOrWhiteSpace(value as string) ? "(not set)" : "(set)";

    private static Func<SettingsValueRow, bool> TextEditor(string title, string prompt,
        Func<string, string> normalize, bool secret = false) =>
        row =>
        {
            string input = TuiPrompts.PromptText(title, prompt, row.Value as string ?? string.Empty, secret);

            if (input == null)
            {
                return false;
            }

            string normalized = normalize(input);

            if (string.Equals(row.Value as string ?? string.Empty, normalized ?? string.Empty, StringComparison.Ordinal))
            {
                return false;
            }

            row.Value = normalized;

            return true;
        };

    private static Func<SettingsValueRow, bool> NumberEditor(string title, string prompt, int min, int max) =>
        row =>
        {
            string input = TuiPrompts.PromptText(title, prompt, row.Value?.ToString() ?? string.Empty);

            if (input == null)
            {
                return false;
            }

            if (!int.TryParse(input.Trim(), out int parsed))
            {
                TuiApp.PostStatus($"Invalid value. Enter a whole number from {min} to {max}.");
                return false;
            }

            int clamped = Math.Clamp(parsed, min, max);

            if (clamped.Equals(row.Value))
            {
                return false;
            }

            row.Value = clamped;

            return true;
        };

    private static Func<SettingsValueRow, bool> ChoiceEditor(string title, string prompt,
        params (string Value, string Label)[] options) =>
        row =>
        {
            int? choice = SelectDialog.Show(title, prompt, options.Select(option => option.Label).ToList());

            if (choice == null)
            {
                return false;
            }

            string picked = options[choice.Value].Value;

            if (string.Equals(row.Value as string, picked, StringComparison.Ordinal))
            {
                return false;
            }

            row.Value = picked;

            return true;
        };

    // ── State ────────────────────────────────────────────────────────────────────────────────

    private void ActivateRow(int index)
    {
        if (index < 0 || index >= rows.Count)
        {
            return;
        }

        if (rows[index].Activate())
        {
            dirty = true;
            RenderAll();
        }
    }

    /// <summary>Re-reads every row from the live config. Called when the tab is opened, so a value
    /// changed elsewhere isn't clobbered by a stale snapshot on the next Save.</summary>
    public void Refresh()
    {
        var config = ServiceHelper.SettingsService.Config;

        foreach (var row in rows)
        {
            row.Reload(config);
        }

        dirty = false;
        RenderAll();
        list.SelectFirst();
    }

    public void CommitIfDirty()
    {
        if (dirty)
        {
            Commit();
        }
    }

    private void Commit()
    {
        if (!dirty)
        {
            TuiApp.PostStatus("No settings changes to save.");
            return;
        }

        var config = ServiceHelper.SettingsService.Config;

        foreach (var row in rows)
        {
            row.Commit(config);
        }

        ServiceHelper.SettingsService.Save();
        ServiceHelper.ReloadSettings();
        context.CoreUpdater.ReloadSettings();

        dirty = false;
        RenderAll();

        TuiApp.PostStatus("Settings saved.");
    }

    private void RenderAll()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            string text = rows[i].Render();

            if (i < labels.Count)
            {
                if (labels[i] != text)
                {
                    labels[i] = text;
                }
            }
            else
            {
                labels.Add(text);
            }
        }

        hint.Text = dirty ? $"{Hint} - unsaved changes" : Hint;
    }
}
