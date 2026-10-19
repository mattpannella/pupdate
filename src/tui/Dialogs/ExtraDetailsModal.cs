using System.Collections.Generic;
using Pannella.Models.Extras;

namespace Pannella.TUI;

/// <summary>
/// Description + links for a Pocket Extra, shown before installing when the
/// <c>show_menu_descriptions</c> setting is on. Returns true if the user chose to install.
/// </summary>
public static class ExtraDetailsModal
{
    public static bool Confirm(PocketExtra extra, string title)
    {
        var links = new List<string>();

        if (!string.IsNullOrEmpty(extra.github_user) && !string.IsNullOrEmpty(extra.github_repository))
        {
            links.Add($"https://github.com/{extra.github_user}/{extra.github_repository}");
        }

        if (extra.additional_links is { Count: > 0 })
        {
            links.AddRange(extra.additional_links);
        }

        return DetailsConfirmModal.Confirm(title, extra.description, links);
    }
}
