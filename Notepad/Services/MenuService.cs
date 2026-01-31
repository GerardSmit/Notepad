using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Notepad.Abstractions.Plugins;
using Notepad.Abstractions.Services;
using Windows.System;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics.CodeAnalysis; 

namespace Notepad.Services;

/// <summary>
/// Service for managing menu items and overlays.
/// </summary>
public sealed class MenuService : IMenuService
{
    private readonly List<PluginMenuItem> _menuItems = [];
    private readonly List<IPluginControl> _overlayControls = [];
    private MenuBar? _menuBar;
    private UIElement? _acceleratorScope;
    private Panel? _tabContentGrid;
    private readonly IServiceProvider _serviceProvider;
    private readonly IEditorService _editorService;

    public MenuService(IServiceProvider serviceProvider, IEditorService editorService)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _editorService = editorService ?? throw new ArgumentNullException(nameof(editorService));
    }

    /// <summary>
    /// Gets the registered menu items.
    /// </summary>
    public IReadOnlyList<PluginMenuItem> MenuItems => _menuItems;

    /// <summary>
    /// Gets the registered overlay controls.
    /// </summary>
    public IReadOnlyList<IPluginControl> OverlayControls => _overlayControls;

    /// <inheritdoc/>
    public void RegisterMenuItem(PluginMenuItem menuItem)
    {
        _menuItems.Add(menuItem);
    }

    /// <summary>
    /// Registers an overlay control for click-outside handling.
    /// </summary>
    /// <param name="control">The overlay control to register.</param>
    private void RegisterOverlayControl(IPluginControl control)
    {
        _overlayControls.Add(control);
    }

    /// <summary>
    /// Registers a plugin control by adding it to the grid and registering it as an overlay.
    /// </summary>
    /// <param name="control">The UI element to add to the grid.</param>
    public T RegisterPluginControl<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : UIElement, IPluginControl
    {
        var control = ActivatorUtilities.CreateInstance<T>(_serviceProvider);

        if (_tabContentGrid is not null)
        {
            _tabContentGrid.Children.Add(control);
        }

        RegisterOverlayControl(control);
        
        // Hook into visibility changes to restore editor focus when plugin hides
        control.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, OnPluginVisibilityChanged);

        return control;
    }
    
    private void OnPluginVisibilityChanged(DependencyObject sender, DependencyProperty dp)
    {
        if (sender is UIElement element)
        {
            if (element.Visibility == Visibility.Collapsed)
            {
                _editorService.DispatcherQueue.TryEnqueue(() => _editorService.FocusEditor());
            }
        }
    }

    /// <inheritdoc/>
    public void HideAllOverlays()
    {
        foreach (var control in _overlayControls)
        {
            if (control.IsOpen)
            {
                control.Hide();
            }
        }
    }

    /// <summary>
    /// Sets the menu bar to add menu items to.
    /// </summary>
    /// <param name="menuBar">The menu bar.</param>
    public void SetMenuBar(MenuBar menuBar)
    {
        _menuBar = menuBar;
    }

    /// <summary>
    /// Sets the scope element for keyboard accelerators.
    /// </summary>
    /// <param name="scope">The UI element to use as the accelerator scope.</param>
    public void SetAcceleratorScope(UIElement scope)
    {
        _acceleratorScope = scope;
    }

    /// <summary>
    /// Sets the panel where plugin controls should be added.
    /// </summary>
    /// <param name="panel">The panel to add plugin controls to.</param>
    public void SetTabContentGrid(Panel panel)
    {
        _tabContentGrid = panel;
    }

    /// <summary>
    /// Applies all registered menu items to the menu bar.
    /// </summary>
    public void ApplyMenuItems()
    {
        if (_menuBar is null) return;

        var groupedItems = _menuItems
            .GroupBy(m => m.Category)
            .ToDictionary(g => g.Key, g => g.OrderBy(m => m.Order).ToList());

        foreach (var (category, items) in groupedItems)
        {
            var menu = _menuBar.Items
                .OfType<MenuBarItem>()
                .FirstOrDefault(m => m.Title == category);

            if (menu is null)
            {
                menu = new MenuBarItem { Title = category };
                _menuBar.Items.Add(menu);
            }
            else if (items.Count > 0)
            {
                // Add separator before plugin items in existing menus
                menu.Items.Add(new MenuFlyoutSeparator());
            }

            foreach (var item in items)
            {
                var menuFlyoutItem = new MenuFlyoutItem
                {
                    Text = item.Text
                };

                if (item.Shortcut is not null)
                {
                    var accelerator = new KeyboardAccelerator
                    {
                        Key = item.Shortcut.Key,
                        Modifiers = item.Shortcut.Modifiers
                    };

                    menuFlyoutItem.KeyboardAccelerators.Add(accelerator);

                    // Also add to the scope element to ensure it works globally
                    if (_acceleratorScope is not null)
                    {
                        var scopeAccelerator = new KeyboardAccelerator
                        {
                            Key = item.Shortcut.Key,
                            Modifiers = item.Shortcut.Modifiers
                        };
                        scopeAccelerator.Invoked += (_, args) =>
                        {
                            item.Execute?.Invoke();
                            args.Handled = true;
                        };
                        _acceleratorScope.KeyboardAccelerators.Add(scopeAccelerator);
                    }
                }

                menuFlyoutItem.Click += (_, _) => item.Execute?.Invoke();
                menu.Items.Add(menuFlyoutItem);
            }
        }
    }
}
