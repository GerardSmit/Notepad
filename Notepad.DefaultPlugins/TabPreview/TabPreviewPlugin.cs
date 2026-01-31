using Microsoft.Extensions.DependencyInjection;
using Notepad.Abstractions.Plugins;
using Notepad.Abstractions.Services;
using Notepad.DefaultPlugins.Services;

namespace Notepad.DefaultPlugins.TabPreview;

/// <summary>
/// Plugin that provides tab preview functionality.
/// </summary>
public sealed class TabPreviewPlugin : IPlugin
{
    /// <inheritdoc/>
    public string Id => "Notepad.TabPreview";

    /// <inheritdoc/>
    public string Name => "Tab Preview";

    /// <inheritdoc/>
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ITabPreviewProvider, DefaultTabPreviewProvider>();
    }
}
