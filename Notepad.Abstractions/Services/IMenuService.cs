using Microsoft.UI.Xaml;
using Notepad.Abstractions.Plugins;
using System.Diagnostics.CodeAnalysis;

namespace Notepad.Abstractions.Services;

/// <summary>
/// Provides access to menu registration and management.
/// </summary>
public interface IMenuService
{
    /// <summary>
    /// Registers a menu item.
    /// </summary>
    /// <param name="menuItem">The menu item to register.</param>
    void RegisterMenuItem(PluginMenuItem menuItem);

    /// <summary>
    /// Registers a plugin control by creating it via DI, adding it to the grid and registering it as an overlay.
    /// </summary>
    /// <typeparam name="T">The control type to create.</typeparam>
    /// <returns>The created control instance.</returns>
    T RegisterPluginControl<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : UIElement, IPluginControl; 

    /// <summary>
    /// Hides all plugin overlays.
    /// </summary>
    void HideAllOverlays();
}
