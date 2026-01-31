using Notepad.Abstractions.Plugins;
using Notepad.Abstractions.Services;
using Notepad.DefaultPlugins.Services;
using Windows.System;

namespace Notepad.DefaultPlugins.RecentFiles;

/// <summary>
/// Plugin that provides Recent Files functionality with Ctrl+R shortcut.
/// </summary>
public sealed class RecentFilesPlugin(
    IMenuService menuService,
    IDocumentService documentService,
    RecentFilesService recentFilesService) : IPlugin
{
    private RecentFilesPluginControl? _control;

    /// <inheritdoc/>
    public string Id => "Notepad.RecentFiles";

    /// <inheritdoc/>
    public string Name => "Recent Files";

    /// <inheritdoc/>
    public void Initialize()
    {
        _control = menuService.RegisterPluginControl<RecentFilesPluginControl>();

        menuService.RegisterMenuItem(new PluginMenuItem
        {
            Category = "File",
            Text = "Recent Files...",
            Shortcut = new PluginShortcut(VirtualKey.R, VirtualKeyModifiers.Control),
            Execute = ShowRecentFiles,
            Order = 50
        });

        // Subscribe to file open events to track history
        documentService.SelectedTabChanged += OnSelectedTabChanged;

        // Start loading recent files in background (Show() will await completion)
        _ = recentFilesService.EnsureLoadedAsync();
    }

    private void OnSelectedTabChanged(object? sender, Abstractions.Models.DocumentTab? tab)
    {
        if (tab?.FilePath is { } filePath && !string.IsNullOrWhiteSpace(filePath))
        {
            _ = recentFilesService.AddFileAsync(filePath);
        }
    }

    private void ShowRecentFiles()
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
