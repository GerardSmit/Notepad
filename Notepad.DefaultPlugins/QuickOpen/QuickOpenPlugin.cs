using Notepad.Abstractions.Plugins;
using Notepad.Abstractions.Services;
using Windows.System;

namespace Notepad.DefaultPlugins.QuickOpen;

/// <summary>
/// Plugin that provides Quick Open (file switching) functionality.
/// </summary>
public sealed class QuickOpenPlugin(IMenuService menuService) : IPlugin
{
    private QuickOpenPluginControl? _control;

    /// <inheritdoc/>
    public string Id => "Notepad.QuickOpen";

    /// <inheritdoc/>
    public string Name => "Quick Open";

    /// <inheritdoc/>
    public void Initialize()
    {
        _control = menuService.RegisterPluginControl<QuickOpenPluginControl>();

        menuService.RegisterMenuItem(new PluginMenuItem
        {
            Category = "Go",
            Text = "Quick Open...",
            Shortcut = new PluginShortcut(VirtualKey.P, VirtualKeyModifiers.Control),
            Execute = ShowQuickOpen,
            Order = 10
        });
    }

    private void ShowQuickOpen()
    {
        if (_control?.IsOpen == true)
        {
            _control.Hide();
        }
        else
        {
            menuService.HideAllOverlays();
            _control?.Show();
        }
    }
}
