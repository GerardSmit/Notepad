using Microsoft.Extensions.DependencyInjection;

namespace Notepad.Abstractions.Plugins;

/// <summary>
/// Defines the interface for a Notepad plugin.
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// Gets the unique identifier for the plugin.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the display name of the plugin.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Configures services for the plugin. Called during application startup before services are built.
    /// Plugins should register their services and dependencies during this call.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    void ConfigureServices(IServiceCollection services)
    {
    }

    /// <summary>
    /// Initializes the plugin. Called once when the plugin is loaded after services are built.
    /// Plugins should register their menu items and controls during this call.
    /// </summary>
    void Initialize()
    {
    }
}
