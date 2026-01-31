using Microsoft.Extensions.DependencyInjection;
using Notepad.Abstractions.Plugins;
using Notepad.DefaultPlugins.About;
using Notepad.DefaultPlugins.FindReplace;
using Notepad.DefaultPlugins.GoToLine;
using Notepad.DefaultPlugins.QuickOpen;
using Notepad.DefaultPlugins.RecentFiles;
using Notepad.DefaultPlugins.Services;
using Notepad.DefaultPlugins.TabPreview;

namespace Notepad.DefaultPlugins;

/// <summary>
/// Extension methods for registering default plugins with dependency injection.
/// </summary>
public static class DefaultPluginsServiceCollectionExtensions
{
    /// <summary>
    /// Adds the default plugins to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDefaultPlugins(this IServiceCollection services)
    {
        // Services
        services.AddSingleton<RecentFilesService>();

        // Plugins
        services.AddSingleton<IPlugin, GoToLinePlugin>();
        services.AddSingleton<IPlugin, QuickOpenPlugin>();
        services.AddSingleton<IPlugin, FindReplacePlugin>();
        services.AddSingleton<IPlugin, TabPreviewPlugin>();
        services.AddSingleton<IPlugin, RecentFilesPlugin>();
        services.AddSingleton<IPlugin, AboutPlugin>();

        return services;
    }
}
