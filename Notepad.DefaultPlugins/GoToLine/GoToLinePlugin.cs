using Microsoft.UI.Xaml;
using Notepad.Abstractions.Plugins;
using Notepad.Abstractions.Services;
using Windows.System;

namespace Notepad.DefaultPlugins.GoToLine;

/// <summary>
/// Plugin that provides Go to Line functionality.
/// </summary>
public sealed class GoToLinePlugin(IMenuService menuService) : IPlugin
{

    private GoToLinePluginControl? _control;

    /// <inheritdoc/>
    public string Id => "Notepad.GoToLine";

    /// <inheritdoc/>
    public string Name => "Go to Line";

    /// <inheritdoc/>
    public void Initialize()
    {
        _control = menuService.RegisterPluginControl<GoToLinePluginControl>();

        menuService.RegisterMenuItem(new PluginMenuItem
        {
            Category = "Go",
            Text = "Go to Line...",
            Shortcut = new PluginShortcut(VirtualKey.G, VirtualKeyModifiers.Control),
            Execute = ShowGoToLine,
            Order = 20
        });
    }

    private void ShowGoToLine()
    {
        menuService.HideAllOverlays();
        _control?.Show();
    }
}
