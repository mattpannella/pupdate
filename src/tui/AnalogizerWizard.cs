using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Pannella.Services;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// TUI front-ends for the two Analogizer configurators. Both reuse the service logic (bit-packing /
/// file writing for Standard; the letter-encoding for Jotego) and share the same wizard page shape:
/// a prompt above a radio-button option list, where a click only marks the choice and Back/Next
/// navigate. Standard is one multi-step <see cref="Wizard"/>; Jotego is service-driven one prompt
/// at a time, so each prompt is its own single-page wizard. Cancelling aborts without writing a file.
/// </summary>
public static class AnalogizerWizard
{
    // ── Standard ─────────────────────────────────────────────────────────────────────────────

    public static void RunStandard()
    {
        var wizard = NewWizard("Analogizer — Standard");

        var snac = AddPickStep(wizard, "SNAC Controller", "Select your SNAC game controller:",
            AnalogizerSettingsService.SNACSelectionOptions);
        var assign = AddPickStep(wizard, "SNAC Assignment", "Select the SNAC controller assignment:",
            AnalogizerSettingsService.SNACassigmentsOptions);
        var video = AddPickStep(wizard, "Video Output", "Select the video output:",
            AnalogizerSettingsService.VideoOutputOptions);
        var blank = AddPickStep(wizard, "Pocket Screen", "Pocket blank screen:",
            AnalogizerSettingsService.PocketBlankScreenOptions);
        var osd = AddPickStep(wizard, "OSD Output", "Where should the OSD be shown:",
            AnalogizerSettingsService.AnalogizerOSDOptions);
        var regional = AddPickStep(wizard, "Regional Settings", "Select the regional setting:",
            AnalogizerSettingsService.AnalogizerRegionalSettingsOptions);

        // The assignment question only applies when a SNAC controller is selected; the wizard
        // skips disabled steps automatically (the classic flow's "assignment is bypassed" rule).
        void SyncAssignStep() => assign.Step.Enabled = snac.SelectedKey != 0;
        SyncAssignStep();
        snac.Selector.ValueChanged += (_, _) => SyncAssignStep();

        TuiHost.Run(wizard);

        if (wizard.Canceled)
        {
            Cancel();
            return;
        }

        int snacAssign = assign.Step.Enabled ? assign.SelectedKey : 0;

        AnalogizerSettingsService.WriteConfig(regional.SelectedKey, osd.SelectedKey, blank.SelectedKey,
            video.SelectedKey, snacAssign, snac.SelectedKey, TuiApp.PostStatus);
    }

    // SelectedKey is the chosen dictionary KEY (the value stored in the config word).
    private sealed class PickStep
    {
        public WizardStep Step { get; init; }
        public OptionSelector Selector { get; init; }
        public List<int> Keys { get; init; }

        public int SelectedKey => Keys[Selector.Value is { } v && v >= 0 && v < Keys.Count ? v : 0];
    }

    private static PickStep AddPickStep(Wizard wizard, string title, string prompt, Dictionary<int, string> options)
    {
        var keys = options.Keys.ToList();
        var (step, selector) = BuildStep(wizard, title, prompt, keys.Select(k => $"{k}: {options[k]}"));

        return new PickStep { Step = step, Selector = selector, Keys = keys };
    }

    private static Wizard NewWizard(string title) => new()
    {
        Title = title,
        Width = Dim.Percent(75),
        Height = Dim.Percent(75)
    };

    // The shared page shape: prompt label above a radio-button list. The prompt is deliberately
    // NOT WizardStep.HelpText (which renders in a detached pane), and double-click accept is off
    // so only Back/Next navigate.
    private static (WizardStep Step, OptionSelector Selector) BuildStep(
        Wizard wizard, string title, string prompt, IEnumerable<string> labels)
    {
        var step = new WizardStep
        {
            Title = title
        };

        var promptLabel = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Text = prompt,
            CanFocus = false
        };

        var selector = new OptionSelector
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Labels = labels.ToList(),
            Value = 0,
            DoubleClickAccepts = false
        };

        step.Add(promptLabel);
        step.Add(selector);
        wizard.AddStep(step);

        return (step, selector);
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
    // the record's prompt text and showing it as a single-page wizard (the service asks one
    // question at a time, so multi-step Back/Next isn't possible). Returns null (→ abort) on cancel.
    private static string JotegoPick(AnalogizerOptionType record)
    {
        var options = ParseLetterOptions(record.Options);

        if (options.Count == 0)
        {
            return null;
        }

        var wizard = NewWizard("Analogizer — Jotego");
        var (step, selector) = BuildStep(wizard, "Option", FirstPromptLine(record.Options),
            options.Select(o => $"{o.Letter.ToUpper()}: {o.Description}"));
        step.NextButtonText = "_Next";

        TuiHost.Run(wizard);

        return wizard.Canceled ? null : options[selector.Value ?? 0].Letter;
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
