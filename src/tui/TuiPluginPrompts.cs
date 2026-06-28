using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Pannella.Models.Plugins;

namespace Pannella.TUI;

/// <summary>
/// TUI implementations of the plugin host prompts. Plugins run on a background task, so each prompt
/// is marshaled onto the UI thread (where the modal must run) and blocks the plugin thread until the
/// user answers. Wired into PluginService.ChoiceHandler / TextHandler by TuiApp; the classic menu
/// leaves those null and keeps its Console prompts.
/// </summary>
internal static class TuiPluginPrompts
{
    public static HostMessage Choice(ChoicePluginMessage choice)
    {
        return RunOnUi(() =>
        {
            List<string> choices = choice.Choices?.ToList() ?? new List<string>();
            int? index = SelectDialog.Show(choice.Query ?? "Choose", "Select an option:", choices);

            return index == null
                ? (HostMessage)new KillHostMessage()
                : new AnswerHostMessage { Name = choice.Name, Value = choices[index.Value] };
        });
    }

    public static HostMessage Text(TextPluginMessage text)
    {
        return RunOnUi(() =>
        {
            string input = TuiPrompts.PromptText(text.Query ?? "Input", text.Query ?? "Enter value:");

            return string.IsNullOrEmpty(input)
                ? (HostMessage)new KillHostMessage()
                : new AnswerHostMessage { Name = text.Name, Value = input };
        });
    }

    private static HostMessage RunOnUi(Func<HostMessage> show)
    {
        HostMessage result = null;
        using var done = new ManualResetEventSlim(false);

        TuiHost.Invoke(() =>
        {
            try
            {
                result = show();
            }
            finally
            {
                done.Set();
            }
        });

        done.Wait();
        return result;
    }
}
