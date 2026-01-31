using Windows.System;

namespace Notepad.Abstractions.Plugins;

/// <summary>
/// Represents a keyboard shortcut for a plugin command.
/// </summary>
/// <param name="Key">The key for the shortcut.</param>
/// <param name="Modifiers">The modifier keys (Ctrl, Shift, Alt).</param>
public record PluginShortcut(VirtualKey Key, VirtualKeyModifiers Modifiers = VirtualKeyModifiers.None);

/// <summary>
/// Represents a menu item registration for a plugin.
/// </summary>
public record PluginMenuItem
{
    /// <summary>
    /// Gets or sets the menu category (e.g., "Edit", "View", "Go").
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// Gets or sets the text displayed in the menu.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Gets or sets the optional keyboard shortcut.
    /// </summary>
    public PluginShortcut? Shortcut { get; init; }

    /// <summary>
    /// Gets or sets the action to execute when the menu item is clicked.
    /// </summary>
    public required Action Execute { get; init; }

    /// <summary>
    /// Gets or sets the order within the menu category. Lower values appear first.
    /// </summary>
    public int Order { get; init; } = 100;
}
