using System;
using System.Collections.Generic;
using System.Linq;

namespace Pannella.TUI;

/// <summary>
/// Builds the (key, display) choices behind a filter dropdown. Dropdown selections are mapped back
/// to keys by their label text, so two keys with the same friendly name (the "playstation" and "psx"
/// platforms are both "PlayStation") must get distinct labels or the second can never be selected.
/// </summary>
internal static class FilterChoices
{
    public static List<(string Key, string Display)> Build(IEnumerable<string> keys, Func<string, string> display)
    {
        var choices = (keys ?? Array.Empty<string>())
            .Where(k => !string.IsNullOrEmpty(k))
            .Distinct()
            .Select(k => (Key: k, Display: display(k)))
            .ToList();

        var collisions = choices
            .GroupBy(c => c.Display, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g)
            .Select(c => c.Key)
            .ToHashSet(StringComparer.Ordinal);

        return choices
            .Select(c => collisions.Contains(c.Key) ? (Key: c.Key, Display: $"{c.Display} ({c.Key})") : c)
            .OrderBy(x => x.Display, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
