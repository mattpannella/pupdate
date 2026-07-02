using System;

namespace Pannella.TUI;

/// <summary>
/// Single source of truth for the TUI's single-keypress accelerators, shared by the tab strip
/// (<see cref="TuiShell"/>), action lists (<see cref="ActionMenuTab"/>) and single-select popups
/// (<see cref="SelectDialog"/>) so their key↔index mappings can never drift apart.
///
/// Tabs claim the first <see cref="TabCount"/> letters (A, B, …) as global switch keys. Menu items
/// therefore use digits first (0-9) and then letters STARTING AFTER the reserved tab letters, so an
/// item key can never collide with a tab key — that separation is what lets a tab letter typed inside
/// a focused list fall through to the shell and switch tabs while item keys stay local.
/// </summary>
internal static class TuiAccelerators
{
    // The number of tabs in TuiShell. Tabs use letters 'A' .. 'A'+TabCount-1. If a tab is added or
    // removed, bump this: the item-key letters below shift automatically to avoid the new range.
    public const int TabCount = 6;

    private const string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    // 0-9, then the letters that AREN'T reserved for tabs (G-Z when TabCount is 6). ~30 keys — plenty
    // for any realistic menu; items beyond this simply get no accelerator.
    private static readonly string ItemKeys = "0123456789" + Letters.Substring(TabCount);

    // --- Tabs -------------------------------------------------------------------------------------

    /// <summary>The switch key for the tab at <paramref name="index"/> ('A'..), or null if out of range.</summary>
    public static char? TabKey(int index) =>
        index >= 0 && index < TabCount ? (char)('A' + index) : null;

    /// <summary>The tab index for a typed char (case-insensitive), or -1 if it isn't a tab key.</summary>
    public static int TabIndex(char c)
    {
        int i = char.ToUpperInvariant(c) - 'A';
        return i >= 0 && i < TabCount ? i : -1;
    }

    // --- Menu items -------------------------------------------------------------------------------

    /// <summary>The accelerator key for the item at <paramref name="index"/>, or null past the key set.</summary>
    public static char? ItemKey(int index) =>
        index >= 0 && index < ItemKeys.Length ? ItemKeys[index] : null;

    /// <summary>The item index for a typed char (case-insensitive for letters), or -1 if it isn't an item key.</summary>
    public static int ItemIndex(char c) => ItemKeys.IndexOf(char.ToUpperInvariant(c));

    // --- Label formatting -------------------------------------------------------------------------

    /// <summary>Prefixes a tab title with its switch key, e.g. "[A] Main".</summary>
    public static string FormatTab(int index, string title) =>
        TabKey(index) is { } key ? $"[{key}] {title}" : title;

    /// <summary>Prefixes a menu label with its accelerator, e.g. "[0] Update All" (aligned when none).</summary>
    public static string FormatItem(int index, string label) =>
        ItemKey(index) is { } key ? $"[{key}] {label}" : $"    {label}";
}
