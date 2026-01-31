using Notepad.Abstractions.Plugins;
using Notepad.Abstractions.Services;

namespace Notepad.DefaultPlugins.About;

/// <summary>
/// Plugin that provides About dialog functionality.
/// </summary>
public sealed class AboutPlugin(IMenuService menuService) : IPlugin
{
    private AboutPluginControl? _control;

    /// <inheritdoc/>
    public string Id => "Notepad.About";

    /// <inheritdoc/>
    public string Name => "About";

    /// <inheritdoc/>
    public void Initialize()
    {
        _control = menuService.RegisterPluginControl<AboutPluginControl>();

        menuService.RegisterMenuItem(new PluginMenuItem
        {
            Category = "Help",
            Text = "About Notepad",
            Shortcut = null,
            Execute = ShowAbout,
            Order = 100
        });
    }

    private void ShowAbout()
    {
        menuService.HideAllOverlays();
        _control?.Show();
    }
}
