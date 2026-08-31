using System;
using System.Reflection;
using Pannella.Models.Settings;
using Terminal.Gui.Drawing;

namespace Pannella.TUI;

internal abstract class SettingsRow
{
    /// <summary>Column where a value row's value starts, so every group lines up.</summary>
    protected const int ValueColumn = 56;

    /// <summary>False for group headers: navigation skips them and they can't be activated.</summary>
    public virtual bool Selectable => true;

    public abstract string Render();

    /// <summary>Space/Enter on the row. Returns true if the pending value changed.</summary>
    public abstract bool Activate();

    /// <summary>Re-reads the pending value from the live config.</summary>
    public virtual void Reload(Config config) { }

    /// <summary>Writes the pending value into the config.</summary>
    public virtual void Commit(Config config) { }
}

internal sealed class SettingsHeaderRow : SettingsRow
{
    private readonly string text;

    public SettingsHeaderRow(string title) => text = $"── {title} ".PadRight(ValueColumn + 8, '─');

    public override bool Selectable => false;

    public override string Render() => text;

    public override bool Activate() => false;
}

internal sealed class SettingsToggleRow : SettingsRow
{
    private readonly PropertyInfo property;
    private readonly string label;

    public SettingsToggleRow(PropertyInfo property, string label)
    {
        this.property = property;
        this.label = label;
    }

    public string PropertyName => property.Name;

    public bool Value { get; set; }

    public override string Render() =>
        $"  {(Value ? Glyphs.CheckStateChecked : Glyphs.CheckStateUnChecked)} {label}";

    public override bool Activate()
    {
        Value = !Value;
        return true;
    }

    public override void Reload(Config config) => Value = (bool)(property.GetValue(config) ?? false);

    public override void Commit(Config config) => property.SetValue(config, Value);
}

/// <summary>
/// A non-toggle setting: the label, dot leaders, then the pending value. <c>edit</c> opens the
/// editor (prompt or picker), sets <see cref="Value"/>, and returns true if it changed.
/// </summary>
internal sealed class SettingsValueRow : SettingsRow
{
    private readonly PropertyInfo property;
    private readonly string label;
    private readonly Func<SettingsValueRow, bool> edit;
    private readonly Func<object, string> display;

    public SettingsValueRow(PropertyInfo property, string label,
        Func<SettingsValueRow, bool> edit, Func<object, string> display)
    {
        this.property = property;
        this.label = label;
        this.edit = edit;
        this.display = display;
    }

    public object Value { get; set; }

    public override string Render() => $"    {label} ".PadRight(ValueColumn, '.') + $" {display(Value)}";

    public override bool Activate() => edit(this);

    public override void Reload(Config config) => Value = property.GetValue(config);

    public override void Commit(Config config) => property.SetValue(config, Value);
}

/// <summary>A row that runs something (a diagnostic, not an edit) rather than holding a value.</summary>
internal sealed class SettingsActionRow : SettingsRow
{
    private readonly string label;
    private readonly Action run;

    public SettingsActionRow(string label, Action run)
    {
        this.label = label;
        this.run = run;
    }

    public override string Render() => $"    {label}";

    public override bool Activate()
    {
        run();
        return false;
    }
}
