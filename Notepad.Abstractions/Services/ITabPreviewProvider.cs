using Microsoft.UI.Xaml;
using Notepad.Abstractions.Models;

namespace Notepad.Abstractions.Services;

/// <summary>
/// Provides tab preview functionality.
/// </summary>
public interface ITabPreviewProvider : IDisposable
{
    /// <summary>
    /// Gets the priority of this provider. Higher priority providers are checked first.
    /// Default providers should use priority 0, specialized providers should use higher values.
    /// </summary>
    int Priority => 0;

    /// <summary>
    /// Determines whether this provider can provide a preview for the specified tab.
    /// </summary>
    /// <param name="tab">The tab to check.</param>
    /// <returns>true if this provider can provide a preview for the tab; otherwise, false.</returns>
    bool Supports(DocumentTab tab);

    /// <summary>
    /// Gets the preview UI element for the specified tab.
    /// </summary>
    /// <param name="tab">The tab to preview.</param>
    /// <returns>A UIElement containing the preview content.</returns>
    UIElement GetPreview(DocumentTab tab);
}
