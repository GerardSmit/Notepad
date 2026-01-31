using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Notepad.Abstractions;
using Notepad.Abstractions.Models;
using Notepad.Abstractions.Plugins;
using Notepad.Abstractions.Services;
using Notepad.Services;
using Notepad.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using WinRT.Interop;
using WinUIEditor;

namespace Notepad;

/// <summary>
/// Main window of the Notepad application.
/// </summary>
public sealed partial class MainWindow
{
    private readonly ILogger<MainWindow> _logger;
    private MainViewModel _viewModel = null!;
    private CodeEditorControl? _currentEditor;
    private readonly SessionService _sessionService;
    private readonly DocumentService _documentService;
    private readonly MenuService _menuService;
    private readonly IEnumerable<IPlugin> _plugins;
    private readonly List<ITabPreviewProvider> _tabPreviewProviders;
    private AppWindow? _appWindow;
    private DispatcherTimer? _tabTooltipTimer;
    private TabViewItem? _hoveredTabItem;
    private DocumentTab? _hoveredTab;
    private UIElement? _currentTooltipPreview;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        _logger = App.GetLogger<MainWindow>();

        try
        {
            _logger.LogInformation("Constructor starting");
            InitializeComponent();
            _logger.LogDebug("InitializeComponent completed");

            Title = "Notepad";
            ExtendsContentIntoTitleBar = true;

            // Load title bar icon from file (ms-appx doesn't work for unpackaged apps)
            LoadTitleBarIcon();

            // Enable Windows 11 Mica backdrop for acrylic blur effect
            SystemBackdrop = new MicaBackdrop();

            _logger.LogDebug("Getting services from DI");
            _sessionService = App.Services.GetRequiredService<SessionService>();
            _documentService = App.Services.GetRequiredService<DocumentService>();
            var editorService = App.Services.GetRequiredService<EditorService>();
            _menuService = App.Services.GetRequiredService<MenuService>();
            _plugins = App.Services.GetServices<IPlugin>();
            _tabPreviewProviders = App.Services.GetServices<ITabPreviewProvider>()
                .OrderByDescending(p => p.Priority)
                .ToList();
            _logger.LogDebug("Services obtained");

            // Configure editor service (document service is configured after ViewModel is created)
            editorService.SetDispatcherQueue(DispatcherQueue);
            editorService.SetFocusEditorAction(FocusCurrentEditor);

            SetupTitleBar();
            _logger.LogDebug("TitleBar set up");

            if (Content is FrameworkElement fe)
            {
                fe.Loaded += OnContentLoaded;
            }

            // Add handler for closing overlays when clicking outside
            if (Content is not null)
            {
                Content.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnWindowPointerPressed),
                    handledEventsToo: true);
            }

            Closed += OnWindowClosed;
            _logger.LogInformation("Constructor completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MainWindow constructor failed");
            throw;
        }
    }

    private void SetupTitleBar()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        if (_appWindow is not null && AppWindowTitleBar.IsCustomizationSupported())
        {
            var titleBar = _appWindow.TitleBar;
            titleBar.ExtendsContentIntoTitleBar = true;

            // Make title bar buttons blend with the app
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }

        SetTitleBar(AppTitleBar);
    }

    private void LoadTitleBarIcon()
    {
        var exePath = Environment.ProcessPath;
        if (exePath is null) return;

        var iconPath = Path.Combine(Path.GetDirectoryName(exePath)!, "Assets", "notebook.png");
        if (File.Exists(iconPath))
        {
            TitleBarIcon.Source = new BitmapImage(new Uri(iconPath));
        }
    }

    private void OnContentLoaded(object sender, RoutedEventArgs e)
    {
        _ = OnContentLoadedAsync();
    }

    private async Task OnContentLoadedAsync()
    {
        try
        {
            var fileService = new FileService();
            var dialogService = new DialogService(Content.XamlRoot);

            _viewModel = new MainViewModel(fileService, dialogService, _sessionService)
            {
                WindowHandle = WindowNative.GetWindowHandle(this),
                OnFileSaved = () => _currentEditor?.Editor.SetSavePoint()
            };

            // Configure document service now that ViewModel exists
            _documentService.SetTabsProvider(() => _viewModel.Tabs.ToList());
            _documentService.SetSelectedTabProvider(() => _viewModel.SelectedTab);
            _documentService.SetSelectTabAction(tab =>
            {
                _viewModel.SelectedTab = tab;
                SelectTabInView(tab);
            });
            _documentService.SetCurrentEditorProvider(() => _currentEditor);
            _documentService.SetOpenFileAction(OpenFileByPathAsync);

            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.Tabs.CollectionChanged += OnTabsCollectionChanged;

            await _sessionService.LoadUserSettingsAsync();

        var (restoredTabs, selectedTabId) = await _sessionService.RestoreSessionAsync();

        if (restoredTabs.Count > 0)
        {
            _viewModel.Tabs.Clear();
            foreach (var tab in restoredTabs)
            {
                // Just add to collection - OnTabsCollectionChanged will add to TabView
                _viewModel.Tabs.Add(tab);
            }

            var selectedTab = _viewModel.Tabs.FirstOrDefault(t => t.Id == selectedTabId)
                              ?? _viewModel.Tabs.FirstOrDefault();
            if (selectedTab is not null)
            {
                _viewModel.SelectedTab = selectedTab;
                SelectTabInView(selectedTab);
            }
        }
        else
        {
            foreach (var tab in _viewModel.Tabs)
            {
                AddTabToView(tab);
            }

            if (_viewModel.SelectedTab is not null)
            {
                SelectTabInView(_viewModel.SelectedTab);
            }
        }

            FocusCurrentEditor();

            InitializeTheme();

            InitializePlugins();
            
            // Open any files passed via command line
            await OpenCommandLineFilesAsync();
            
            _logger.LogInformation("OnContentLoaded completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OnContentLoaded failed");
        }
    }

    private void InitializePlugins()
    {
        try
        {
            _logger.LogInformation("InitializePlugins starting");

            // Set up menu service
            _menuService.SetMenuBar(MainMenuBar);
            _menuService.SetAcceleratorScope(Content);
            _menuService.SetTabContentGrid(TabContentGrid);

            foreach (var plugin in _plugins)
            {
                try
                {
                    _logger.LogDebug("Initializing plugin {PluginName}", plugin.GetType().Name);
                    plugin.Initialize();
                    _logger.LogDebug("Plugin {PluginName} initialized", plugin.GetType().Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize plugin {PluginName}", plugin.GetType().Name);
                }
            }

            _logger.LogDebug("Applying menu items");
            _menuService.ApplyMenuItems();
            _logger.LogInformation("InitializePlugins completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InitializePlugins failed");
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _ = _sessionService.SaveSessionAsync(_viewModel.Tabs, _viewModel.SelectedTab?.Id);
        App.CloseLogWriter();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedTab))
        {
            if (_viewModel.SelectedTab is not null)
            {
                // Defer selection to ensure the tab has been fully added to the UI
                var tabToSelect = _viewModel.SelectedTab;
                DispatcherQueue.TryEnqueue(() =>
                {
                    SelectTabInView(tabToSelect);
                    
                    // Notify document service so plugins (like Recent Files) can respond
                    _documentService.NotifySelectedTabChanged(tabToSelect);
                });
            }
            
            UpdateStatusBar();
            UpdateWindowTitle();
        }
    }

    private void OnTabsCollectionChanged(object? sender,
        System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems is not null)
        {
            foreach (DocumentTab tab in e.NewItems)
            {
                AddTabToView(tab);
                
                // Notify about the new tab if it has a file path (for Recent Files tracking)
                if (!string.IsNullOrEmpty(tab.FilePath))
                {
                    _documentService.NotifySelectedTabChanged(tab);
                }
            }
        }
        else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove &&
                 e.OldItems is not null)
        {
            foreach (DocumentTab tab in e.OldItems)
            {
                RemoveTabFromView(tab);
            }
        }
        else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
        {
            TabViewControl.TabItems.Clear();
        }
    }

    private void AddTabToView(DocumentTab tab)
    {
        var editor = new CodeEditorControl
        {
            Tag = tab.Id,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            MinHeight = 100
        };

        // Subscribe to save point events to properly track modification state
        editor.Editor.SavePointLeft += (_, _) =>
        {
            if (editor.Tag is Guid editorTabId)
            {
                var editorTab = _viewModel.Tabs.FirstOrDefault(t => t.Id == editorTabId);
                if (editorTab is not null)
                {
                    editorTab.IsModified = true;
                }

                UpdateStatusBar();
            }
        };

        editor.Editor.SavePointReached += (_, _) =>
        {
            if (editor.Tag is Guid editorTabId)
            {
                var editorTab = _viewModel.Tabs.FirstOrDefault(t => t.Id == editorTabId);
                if (editorTab is not null)
                {
                    // Only mark as unmodified if the tab has a file path.
                    // Unsaved tabs should remain modified until saved to a file.
                    if (!string.IsNullOrEmpty(editorTab.FilePath))
                    {
                        editorTab.IsModified = false;
                    }
                }

                UpdateStatusBar();
            }
        };

        // Subscribe to text changes to keep content buffer in sync
        editor.Editor.Modified += (_, _) =>
        {
            if (editor.Tag is Guid editorTabId)
            {
                var editorTab = _viewModel.Tabs.FirstOrDefault(t => t.Id == editorTabId);
                if (editorTab is not null)
                {
                    editorTab.Content = GetEditorContentAsBuffer(editor);
                }
            }
        };

        SetEditorContent(editor, tab.Content);

        // Clear undo history
        editor.Editor.EmptyUndoBuffer();

        // Set save point - this marks current state as "saved"
        // For modified tabs, SavePointReached won't clear IsModified (see handler above)
        editor.Editor.SetSavePoint();

        editor.Editor.WrapMode = WordWrapToggle.IsChecked ? Wrap.Word : Wrap.None;

        if (LineNumbersToggle.IsChecked)
        {
            editor.Editor.SetMarginWidthN(0, 40);
        }
        else
        {
            editor.Editor.SetMarginWidthN(0, 0);
        }

        editor.Editor.UpdateUI += (_, _) => { UpdateStatusBar(); };

        editor.GotFocus += (_, _) => { _currentEditor = editor; };

        // Wrap editor in a Grid to ensure proper stretching within TabViewItem
        var contentGrid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        // Set initial background and subscribe to theme changes
        void UpdateBackground(FrameworkElement element)
        {
            // Create brush based on actual theme - Application.Current.Resources doesn't update with theme
            var color = element.ActualTheme == ElementTheme.Light
                ? Windows.UI.Color.FromArgb(255, 249, 249, 249) // Light: SolidBackgroundFillColorTertiary
                : Windows.UI.Color.FromArgb(255, 40, 40, 40); // Dark: SolidBackgroundFillColorTertiary
            contentGrid.Background = new SolidColorBrush(color);
        }

        if (Content is FrameworkElement root)
        {
            UpdateBackground(root);
        }

        contentGrid.ActualThemeChanged += (sender, _) => { UpdateBackground(sender!); };

        contentGrid.Children.Add(editor);

        var tabViewItem = new TabViewItem
        {
            Header = tab.DisplayTitle,
            Content = contentGrid,
            Tag = tab.Id,
            IsClosable = true
        };

        // Disable built-in tooltip - we use a custom popover instead
        ToolTipService.SetToolTip(tabViewItem, null);

        tabViewItem.PointerEntered += (_, _) => OnTabPointerEntered(tabViewItem, tab);
        tabViewItem.PointerExited += (_, _) => OnTabPointerExited();

        tab.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DocumentTab.DisplayTitle))
            {
                tabViewItem.Header = tab.DisplayTitle;
                UpdateWindowTitle();
            }
        };

        TabViewControl.TabItems.Add(tabViewItem);
    }

    private void RemoveTabFromView(DocumentTab tab)
    {
        var tabItem = TabViewControl.TabItems
            .Cast<TabViewItem>()
            .FirstOrDefault(t => t.Tag is Guid id && id == tab.Id);

        if (tabItem is not null)
        {
            TabViewControl.TabItems.Remove(tabItem);
        }
    }

    private void SelectTabInView(DocumentTab tab)
    {
        var tabItem = TabViewControl.TabItems
            .Cast<TabViewItem>()
            .FirstOrDefault(t => t.Tag is Guid id && id == tab.Id);

        if (tabItem is not null)
        {
            TabViewControl.SelectedItem = tabItem;
            // Focus is now handled in TabView_SelectionChanged
        }
    }

    private void FocusCurrentEditor()
    {
        if (TabViewControl.SelectedItem is TabViewItem tabViewItem &&
            GetEditorFromTabContent(tabViewItem.Content) is { } editor)
        {
            _currentEditor = editor;
            editor.Focus(FocusState.Programmatic);
        }
    }

    private void UpdateStatusBar()
    {
        if (_viewModel.SelectedTab is not null)
        {
            FilePathText.Text = _viewModel.SelectedTab.FilePath ?? "New file";
            CharCountText.Text = $"{_viewModel.SelectedTab.Content.Length} bytes";

            if (_currentEditor is not null)
            {
                var currentPos = _currentEditor.Editor.CurrentPos;
                var line = _currentEditor.Editor.LineFromPosition(currentPos) + 1;
                var lineStartPos = _currentEditor.Editor.PositionFromLine(line - 1);
                var column = currentPos - lineStartPos + 1;
                LineColumnText.Text = $"Ln {line}, Col {column}";
            }
        }
    }

    private void UpdateWindowTitle()
    {
        if (_viewModel.SelectedTab is not null)
        {
            Title = $"{_viewModel.SelectedTab.DisplayTitle} - Notepad";
        }
        else
        {
            Title = "Notepad";
        }
    }

    private static CodeEditorControl? GetEditorFromTabContent(object? content)
    {
        if (content is Grid grid && grid.Children.Count > 0 && grid.Children[0] is CodeEditorControl editor)
        {
            return editor;
        }

        return content as CodeEditorControl;
    }

    /// <summary>
    /// Sets the editor content from an IBuffer.
    /// </summary>
    private static void SetEditorContent(CodeEditorControl editor, IBuffer content)
    {
        if (content.Length == 0)
        {
            // Don't call ClearAll on empty content - editor starts empty anyway
            return;
        }

        editor.Editor.SetTextFromBuffer(content);
    }

    /// <summary>
    /// Gets the editor content as an IBuffer.
    /// </summary>
    private static IBuffer GetEditorContentAsBuffer(CodeEditorControl editor)
    {
        var length = editor.Editor.TextLength;
        if (length == 0)
        {
            return new Windows.Storage.Streams.Buffer(0);
        }

        // Use target to get text as raw bytes - set target range first
        editor.Editor.TargetStart = 0;
        editor.Editor.TargetEnd = length;

        // Create buffer for the content (+1 for null terminator that Scintilla adds)
        var buffer = new Windows.Storage.Streams.Buffer((uint)(length + 1));
        editor.Editor.GetTargetTextWriteBuffer(buffer);

        // Adjust length to exclude null terminator
        buffer.Length = (uint)length;
        return buffer;
    }

    private void OnTabPointerEntered(TabViewItem tabViewItem, DocumentTab tab)
    {
        _hoveredTabItem = tabViewItem;
        _hoveredTab = tab;

        _tabTooltipTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _tabTooltipTimer.Stop();
        _tabTooltipTimer.Tick -= OnTabTooltipTimerTick;
        _tabTooltipTimer.Tick += OnTabTooltipTimerTick;
        _tabTooltipTimer.Start();
    }

    private void OnTabPointerExited()
    {
        _tabTooltipTimer?.Stop();
        _hoveredTabItem = null;
        _hoveredTab = null;
        TabTooltipPopup.IsOpen = false;

        CleanupTooltipPreview();
    }

    private void OnTabTooltipTimerTick(object? sender, object e)
    {
        _tabTooltipTimer?.Stop();

        if (_hoveredTabItem is null || _hoveredTab is null)
        {
            return;
        }

        ShowTabTooltip(_hoveredTabItem, _hoveredTab);
    }

    private void ShowTabTooltip(TabViewItem tabViewItem, DocumentTab tab)
    {
        ITabPreviewProvider? provider = null;
        foreach (var p in _tabPreviewProviders)
        {
            if (p.Supports(tab))
            {
                provider = p;
                break;
            }
        }

        if (provider is null)
        {
            return;
        }

        CleanupTooltipPreview();

        var previewElement = provider.GetPreview(tab);
        _currentTooltipPreview = previewElement;

        TabTooltipContent.Content = previewElement;

        var transform = tabViewItem.TransformToVisual(Content);
        var position = transform.TransformPoint(new Windows.Foundation.Point(0, 0));

        TabTooltipPopup.HorizontalOffset = position.X;
        TabTooltipPopup.VerticalOffset = position.Y + tabViewItem.ActualHeight + 4;

        TabTooltipContent.MinWidth = Math.Max(0, tabViewItem.ActualWidth - 24);

        TabTooltipPopup.IsOpen = true;
    }

    private void CleanupTooltipPreview()
    {
        if (_currentTooltipPreview is not null)
        {
            TabTooltipContent.Content = null;
            _currentTooltipPreview = null;
        }
    }

    private void TabView_AddTabButtonClick(TabView sender, object args)
    {
        _viewModel.CreateNewTab();
        if (_viewModel.SelectedTab is not null)
        {
            SelectTabInView(_viewModel.SelectedTab);
        }
    }

    private void TabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Tab.Tag is Guid tabId)
        {
            var tab = _viewModel.Tabs.FirstOrDefault(t => t.Id == tabId);
            if (tab is not null)
            {
                _ = _viewModel.CloseTabCommand.ExecuteAsync(tab);
            }
        }
    }

    private void TabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TabViewControl.SelectedItem is TabViewItem tabViewItem && tabViewItem.Tag is Guid tabId)
        {
            var tab = _viewModel.Tabs.FirstOrDefault(t => t.Id == tabId);
            if (tab is not null)
            {
                _viewModel.SelectedTab = tab;
                
                // Always notify about tab change for plugins like Recent Files
                _documentService.NotifySelectedTabChanged(tab);

                if (GetEditorFromTabContent(tabViewItem.Content) is { } editor)
                {
                    _currentEditor = editor;
                    
                    // Focus the editor when tab is selected
                    editor.Focus(FocusState.Programmatic);
                }
            }
        }

        UpdateStatusBar();
    }

    private void TabView_TabItemsChanged(TabView sender, Windows.Foundation.Collections.IVectorChangedEventArgs args)
    {
        // Sync the ViewModel's Tabs order when tabs are reordered via drag/drop
        if (args.CollectionChange == Windows.Foundation.Collections.CollectionChange.ItemChanged)
        {
            return; // Only handle reordering, not content changes
        }

        var tabViewOrder = TabViewControl.TabItems
            .Cast<TabViewItem>()
            .Select(t => t.Tag)
            .OfType<Guid>()
            .ToList();

        var viewModelOrder = _viewModel.Tabs.Select(t => t.Id).ToList();

        if (tabViewOrder.Count == viewModelOrder.Count && !tabViewOrder.SequenceEqual(viewModelOrder))
        {
            var reorderedTabs = tabViewOrder
                .Select(id => _viewModel.Tabs.FirstOrDefault(t => t.Id == id))
                .Where(t => t is not null)
                .Cast<DocumentTab>()
                .ToList();

            // Temporarily unsubscribe to avoid recursive updates
            _viewModel.Tabs.CollectionChanged -= OnTabsCollectionChanged;

            _viewModel.Tabs.Clear();
            foreach (var tab in reorderedTabs)
            {
                _viewModel.Tabs.Add(tab);
            }

            _viewModel.Tabs.CollectionChanged += OnTabsCollectionChanged;
        }
    }

    private void FocusTrap_GotFocus(object sender, RoutedEventArgs e)
    {
        // Redirect focus to the editor whenever this trap catches focus
        FocusCurrentEditor();
    }

    private void NewTab_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CreateNewTab();
        if (_viewModel.SelectedTab is not null)
        {
            SelectTabInView(_viewModel.SelectedTab);
        }
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        _ = _viewModel.OpenFileCommand.ExecuteAsync(null);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _ = _viewModel.SaveCommand.ExecuteAsync(null);
    }

    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        _ = _viewModel.SaveAsCommand.ExecuteAsync(null);
    }

    private async Task<DocumentTab?> OpenFileByPathAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(filePath);
            await _viewModel.OpenFileInNewTabAsync(file);
            return _viewModel.SelectedTab;
        }
        catch
        {
            return null;
        }
    }

    private async Task OpenCommandLineFilesAsync()
    {
        foreach (var filePath in AppConfiguration.FilesToOpen)
        {
            try
            {
                _logger.LogInformation("Opening command-line file: {FilePath}", filePath);
                
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("Command-line file does not exist: {FilePath}", filePath);
                    continue;
                }

                // Read file content directly instead of using StorageFile API
                // This is more reliable in test/CI environments
                await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                var buffer = new Windows.Storage.Streams.Buffer((uint)fileStream.Length);
                await using var stream = buffer.AsStream();
                await fileStream.CopyToAsync(stream);

                var fileName = Path.GetFileName(filePath);

                var tab = new DocumentTab
                {
                    Title = fileName,
                    Content = buffer,
                    FilePath = filePath,
                    IsModified = false
                };

                _viewModel.Tabs.Add(tab);
                _viewModel.SelectedTab = tab;
                SelectTabInView(tab);
                
                _logger.LogInformation("Opened command-line file successfully: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open command-line file: {FilePath}", filePath);
            }
        }
    }

    private void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        _ = _viewModel.CloseTabCommand.ExecuteAsync(_viewModel.SelectedTab);
    }

    /// <summary>
    /// Returns true if any overlay is currently open (Quick Open, Find/Replace, etc.)
    /// </summary>
    private bool IsAnyOverlayOpen => _menuService.OverlayControls.Any(c => c.IsOpen);

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (IsAnyOverlayOpen) return;
        _currentEditor?.Editor.Undo();
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        if (IsAnyOverlayOpen) return;
        _currentEditor?.Editor.Redo();
    }

    private void Cut_Click(object sender, RoutedEventArgs e)
    {
        if (IsAnyOverlayOpen) return;
        _currentEditor?.Editor.Cut();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (IsAnyOverlayOpen) return;
        _currentEditor?.Editor.Copy();
    }

    private void Paste_Click(object sender, RoutedEventArgs e)
    {
        if (IsAnyOverlayOpen) return;
        _currentEditor?.Editor.Paste();
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        if (IsAnyOverlayOpen) return;
        _currentEditor?.Editor.SelectAll();
    }

    private void WordWrap_Click(object sender, RoutedEventArgs e)
    {
        var wrapMode = WordWrapToggle.IsChecked ? Wrap.Word : Wrap.None;

        foreach (TabViewItem tabItem in TabViewControl.TabItems)
        {
            if (GetEditorFromTabContent(tabItem.Content) is { } editor)
            {
                editor.Editor.WrapMode = wrapMode;
            }
        }
    }

    private void LineNumbers_Click(object sender, RoutedEventArgs e)
    {
        var marginWidth = LineNumbersToggle.IsChecked ? 40 : 0;

        foreach (TabViewItem tabItem in TabViewControl.TabItems)
        {
            if (GetEditorFromTabContent(tabItem.Content) is { } editor)
            {
                editor.Editor.SetMarginWidthN(0, marginWidth);
            }
        }
    }

    private void StatusBar_Click(object sender, RoutedEventArgs e)
    {
        StatusBar.Visibility = StatusBarToggle.IsChecked ? Visibility.Visible : Visibility.Collapsed;
    }

    private void InitializeTheme()
    {
        var savedTheme = _sessionService.GetUserSetting("Theme");
        ElementTheme theme = ElementTheme.Default;

        if (!string.IsNullOrEmpty(savedTheme) && Enum.TryParse<ElementTheme>(savedTheme, out var parsedTheme))
        {
            theme = parsedTheme;
        }

        if (Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = theme;
        }

        UpdateThemeMenuChecks(theme);

        UpdateTitleBarButtonColors(theme);

        _logger.LogInformation($"Initialized theme: {theme}");
    }

    private void UpdateThemeMenuChecks(ElementTheme theme)
    {
        ThemeAuto.Icon = null;
        ThemeDark.Icon = null;
        ThemeLight.Icon = null;

        var checkIcon = new FontIcon { Glyph = "\uE73E" };

        switch (theme)
        {
            case ElementTheme.Default:
                ThemeAuto.Icon = checkIcon;
                break;
            case ElementTheme.Dark:
                ThemeDark.Icon = checkIcon;
                break;
            case ElementTheme.Light:
                ThemeLight.Icon = checkIcon;
                break;
        }
    }

    private void ThemeAuto_Click(object sender, RoutedEventArgs e)
    {
        ApplyTheme(ElementTheme.Default);
    }

    private void ThemeDark_Click(object sender, RoutedEventArgs e)
    {
        ApplyTheme(ElementTheme.Dark);
    }

    private void ThemeLight_Click(object sender, RoutedEventArgs e)
    {
        ApplyTheme(ElementTheme.Light);
    }

    private void ApplyTheme(ElementTheme theme)
    {
        _logger.LogDebug($"Applying theme: {theme}");

        // Apply theme to the root content - elements with ActualThemeChanged handlers will update automatically
        if (Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = theme;
        }

        UpdateThemeMenuChecks(theme);

        UpdateTitleBarButtonColors(theme);

        _sessionService.SetUserSetting("Theme", theme.ToString());

        _logger.LogInformation($"Theme switched to {theme}");
    }

    private void UpdateTitleBarButtonColors(ElementTheme theme)
    {
        if (_appWindow?.TitleBar is null || !AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        var titleBar = _appWindow.TitleBar;

        var isDark = theme == ElementTheme.Dark ||
                     (theme == ElementTheme.Default && Application.Current.RequestedTheme == ApplicationTheme.Dark);

        if (isDark)
        {
            titleBar.ButtonForegroundColor = Colors.White;
            titleBar.ButtonHoverForegroundColor = Colors.White;
            titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(50, 255, 255, 255);
            titleBar.ButtonPressedForegroundColor = Colors.White;
            titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(30, 255, 255, 255);
        }
        else
        {
            titleBar.ButtonForegroundColor = Colors.Black;
            titleBar.ButtonHoverForegroundColor = Colors.Black;
            titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(50, 0, 0, 0);
            titleBar.ButtonPressedForegroundColor = Colors.Black;
            titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(30, 0, 0, 0);
        }
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        var filePath = _viewModel.SelectedTab?.FilePath;
        if (!string.IsNullOrEmpty(filePath))
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(filePath);
            Clipboard.SetContent(dataPackage);
        }
    }

    private void CopyPathLocation_Click(object sender, RoutedEventArgs e)
    {
        var filePath = _viewModel.SelectedTab?.FilePath;
        if (!string.IsNullOrEmpty(filePath) && _currentEditor is not null)
        {
            var currentPos = _currentEditor.Editor.CurrentPos;
            var line = _currentEditor.Editor.LineFromPosition(currentPos) + 1;
            var lineStartPos = _currentEditor.Editor.PositionFromLine(line - 1);
            var column = currentPos - lineStartPos + 1;

            var pathWithLocation = column == 1
                ? $"{filePath}:{line}"
                : $"{filePath}:{line}:{column}";

            var dataPackage = new DataPackage();
            dataPackage.SetText(pathWithLocation);
            Clipboard.SetContent(dataPackage);
        }
    }

    private void OpenDirectory_Click(object sender, RoutedEventArgs e)
    {
        var filePath = _viewModel.SelectedTab?.FilePath;
        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            // Use explorer /select to open directory and highlight the file
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{filePath}\"",
                UseShellExecute = true
            });
        }
        else if (!string.IsNullOrEmpty(filePath))
        {
            // File doesn't exist, just open the directory if it exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = directory,
                    UseShellExecute = true
                });
            }
        }
    }

    #region Window-level click detection for overlay dismissal

    private void OnWindowPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        foreach (var overlayControl in _menuService.OverlayControls)
        {
            if (overlayControl.IsOpen &&
                overlayControl.AutoHide &&
                overlayControl is UserControl userControl &&
                !IsPointerInsideOverlay(userControl, e))
            {
                overlayControl.Hide();
            }
        }
    }

    private bool IsPointerInsideOverlay(UserControl overlay, PointerRoutedEventArgs e)
    {
        // Use hit testing to check if pointer hit a visible element within the overlay
        var elements = VisualTreeHelper.FindElementsInHostCoordinates(
            e.GetCurrentPoint(null).Position,
            overlay);
        
        // If any hit element is within the overlay's visual tree, it's inside
        return elements.Any(el => IsDescendantOf(el, overlay));
    }

    private static bool IsDescendantOf(DependencyObject child, DependencyObject parent)
    {
        var current = child;
        while (current is not null)
        {
            if (current == parent)
                return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    #endregion
}
