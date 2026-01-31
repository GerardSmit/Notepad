namespace Notepad.Abstractions.Plugins;

/// <summary>
/// Defines the interface for a plugin control that can be shown and hidden.
/// </summary>
public interface IPluginControl
{
    /// <summary>
    /// Gets a value indicating whether the control is currently visible/open.
    /// </summary>
    bool IsOpen { get; }

    /// <summary>
    /// Gets a value indicating whether the control should auto-hide when clicking outside of it.
    /// </summary>
    bool AutoHide { get; }

    /// <summary>
    /// Shows the control.
    /// </summary>
    void Show();

    /// <summary>
    /// Hides the control.
    /// </summary>
    void Hide();
}
