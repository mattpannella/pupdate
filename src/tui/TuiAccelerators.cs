using System;

namespace Pannella.TUI;

/// <summary>
/// Single source of truth for the TUI's single-keypress accelerators. Tabs claim the first
/// <see cref="TabCount"/> letters (A, B, …); menu items use digits then letters STARTING AFTER the
/// tab letters, so an item key can never collide with a tab key.
/// </summary>
internal static class TuiAccelerators
{
    // Tabs use letters 'A'..'A'+TabCount-1; if the tab set changes, bump this and the item-key
    // letters below shift past it automatically.
    public const int TabCount = 6;

    private const string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    // 0-9 then the non-tab letters (G-Z when TabCount is 6). Items past this get no accelerator.
    private static readonly string ItemKeys = "0123456789" + Letters.Substring(TabCount);

    public static char? TabKey(int index) =>
        index >= 0 && index < TabCount ? (char)('A' + index) : null;

    public static int TabIndex(char c)
    {
        int i = char.ToUpperInvariant(c) - 'A';
        return i >= 0 && i < TabCount ? i : -1;
    }

    public static char? ItemKey(int index) =>
        index >= 0 && index < ItemKeys.Length ? ItemKeys[index] : null;

    public static int ItemIndex(char c) => ItemKeys.IndexOf(char.ToUpperInvariant(c));

    public static string FormatTab(int index, string title) =>
        TabKey(index) is { } key ? $"[{key}] {title}" : title;

    public static string FormatItem(int index, string label) =>
        ItemKey(index) is { } key ? $"[{key}] {label}" : $"    {label}";
}
