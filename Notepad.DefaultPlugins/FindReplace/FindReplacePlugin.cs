using Microsoft.UI.Xaml;
using Notepad.Abstractions.Models;
using Notepad.Abstractions.Plugins;
using Notepad.Abstractions.Services;
using Windows.System;

namespace Notepad.DefaultPlugins.FindReplace;

/// <summary>
/// Plugin that provides Find and Replace functionality.
/// </summary>
public sealed class FindReplacePlugin(IDocumentService documentService, IMenuService menuService) : IPlugin
{
    private FindReplacePluginControl? _control; 

    /// <inheritdoc/>
    public string Id => "Notepad.FindReplace";

    /// <inheritdoc/>
    public string Name => "Find and Replace";

    /// <inheritdoc/>
    public void Initialize()
    {
        _control = menuService.RegisterPluginControl<FindReplacePluginControl>();

        menuService.RegisterMenuItem(new PluginMenuItem
        {
            Category = "Edit",
            Text = "Find...",
            Shortcut = new PluginShortcut(VirtualKey.F, VirtualKeyModifiers.Control),
            Execute = ShowFind,
            Order = 100
        });

        menuService.RegisterMenuItem(new PluginMenuItem
        {
            Category = "Edit",
            Text = "Replace...",
            Shortcut = new PluginShortcut(VirtualKey.H, VirtualKeyModifiers.Control),
            Execute = ShowReplace,
            Order = 110
        });

        documentService.SelectedTabChanged += OnSelectedTabChanged;
    }

    private void ShowFind()
    {
        if (_control is null) return;

        menuService.HideAllOverlays();
        _control.ShowReplace = false;
        _control.Show();
    }

    private void ShowReplace()
    {
        if (_control is null) return;

        menuService.HideAllOverlays();
        _control.ShowReplace = true;
        _control.Show();
    }

    private void OnSelectedTabChanged(object? sender, DocumentTab? tab)
    {
        _control?.OnEditorChanged();
    }
}
