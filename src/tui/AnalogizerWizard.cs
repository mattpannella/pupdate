using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Pannella.Services;

namespace Pannella.TUI;

/// <summary>
/// TUI front-ends for the two Analogizer configurators. Both reuse the service logic (bit-packing /
/// file writing for Standard; the letter-encoding for Jotego) and only replace the console prompts
/// with <see cref="SelectDialog"/> picks. Cancelling any step aborts without writing a file.
/// </summary>
public static class AnalogizerWizard
{
    // ── Standard ─────────────────────────────────────────────────────────────────────────────

    public static void RunStandard()
    {
        int? snac = Pick("Analogizer — SNAC Controller", "Select your SNAC game controller:",
            AnalogizerSettingsService.SNACSelectionOptions);

        if (snac == null)
        {
            Cancel();
            return;
        }

        int snacAssign;

        if (snac.Value == 0)
        {
            snacAssign = 0; // no SNAC controller selected → assignment is bypassed
        }
        else
        {
            int? assign = Pick("Analogizer — SNAC Assignment", "Select the SNAC controller assignment:",
                AnalogizerSettingsService.SNACassigmentsOptions);

            if (assign == null)
            {
                Cancel();
                return;
            }

            snacAssign = assign.Value;
        }

        int? video = Pick("Analogizer — Video Output", "Select the video output:",
            AnalogizerSettingsService.VideoOutputOptions);
        if (video == null) { Cancel(); return; }

        int? blank = Pick("Analogizer — Pocket Screen", "Pocket blank screen:",
            AnalogizerSettingsService.PocketBlankScreenOptions);
        if (blank == null) { Cancel(); return; }

        int? osd = Pick("Analogizer — OSD Output", "Where should the OSD be shown:",
            AnalogizerSettingsService.AnalogizerOSDOptions);
        if (osd == null) { Cancel(); return; }

        int? regional = Pick("Analogizer — Regional Settings", "Select the regional setting:",
            AnalogizerSettingsService.AnalogizerRegionalSettingsOptions);
        if (regional == null) { Cancel(); return; }

        AnalogizerSettingsService.WriteConfig(regional.Value, osd.Value, blank.Value, video.Value,
            snacAssign, snac.Value, TuiApp.PostStatus);
    }

    // Shows a dictionary's entries as "key: label" and returns the chosen KEY (the value stored in
    // the config word), or null if cancelled.
    private static int? Pick(string title, string prompt, Dictionary<int, string> options)
    {
        var keys = options.Keys.ToList();
        var labels = keys.Select(k => $"{k}: {options[k]}").ToList();

        int? index = SelectDialog.Show(title, prompt, labels);

        return index == null ? null : keys[index.Value];
    }

    // ── Jotego ───────────────────────────────────────────────────────────────────────────────

    public static void RunJotego()
    {
        bool ok = new JotegoAnalogizerSettingsService().RunAnalogizerSettings(JotegoPick);

        TuiApp.PostStatus(ok
            ? "Jotego Analogizer configuration saved."
            : "Jotego Analogizer config cancelled.");
    }

    // Supplies one option's chosen letter to the Jotego encoder by parsing the option table out of
    // the record's prompt text and showing it as a SelectDialog. Returns null (→ abort) on cancel.
    private static string JotegoPick(AnalogizerOptionType record)
    {
        var options = ParseLetterOptions(record.Options);

        if (options.Count == 0)
        {
            return null;
        }

        var labels = options.Select(o => $"{o.Letter.ToUpper()}: {o.Description}").ToList();

        int? index = SelectDialog.Show("Jotego Analogizer", FirstPromptLine(record.Options), labels);

        return index == null ? null : options[index.Value].Letter;
    }

    // Parses table rows like "A   | RBGS       (SCART)   |" into (letter, description) pairs. The
    // "Letter | Option" header and the "---" separators don't match (they aren't a single letter).
    private static List<(string Letter, string Description)> ParseLetterOptions(string optionsText)
    {
        var result = new List<(string, string)>();

        foreach (var raw in optionsText.Split('\n'))
        {
            var match = Regex.Match(raw.Trim(), @"^([A-Za-z])\s*\|\s*(.+?)\s*\|?\s*$");

            if (match.Success)
            {
                string description = Regex.Replace(match.Groups[2].Value, @"\s+", " ").Trim();
                result.Add((match.Groups[1].Value.ToLower(), description));
            }
        }

        return result;
    }

    private static string FirstPromptLine(string optionsText)
    {
        foreach (var raw in optionsText.Split('\n'))
        {
            var line = raw.Trim();

            if (line.Length > 0)
            {
                return line;
            }
        }

        return "Select an option:";
    }

    private static void Cancel() => TuiApp.PostStatus("Analogizer config cancelled.");
}
