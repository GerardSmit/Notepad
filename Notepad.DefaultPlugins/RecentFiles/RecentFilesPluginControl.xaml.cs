using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Notepad.Abstractions.Plugins;
using Notepad.Abstractions.Services;
using Notepad.DefaultPlugins.Services;

namespace Notepad.DefaultPlugins.RecentFiles;

/// <summary>
/// A user control that provides Recent Files functionality as a plugin.
/// </summary>
public sealed partial class RecentFilesPluginControl : IPluginControl
{
    private readonly IDocumentService _documentService;
    private readonly IEditorService _editorService;
    private readonly RecentFilesService _recentFilesService;
    private List<RecentFileEntry> _filteredEntries = [];
    private int _selectedIndex = -1;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecentFilesPluginControl"/> class.
    /// </summary>
    public RecentFilesPluginControl(
        IDocumentService documentService,
        IEditorService editorService,
        RecentFilesService recentFilesService)
    {
        _documentService = documentService;
        _editorService = editorService;
        _recentFilesService = recentFilesService;
        InitializeComponent();
    }

    /// <inheritdoc/>
    public bool IsOpen => Visibility == Visibility.Visible;

    /// <inheritdoc/>
    public bool AutoHide => true;

    /// <inheritdoc/>
    public async void Show()
    {
        // Ensure recent files are loaded before showing
        await _recentFilesService.EnsureLoadedAsync();

        SearchBox.Text = string.Empty;
        UpdateResultsList(string.Empty);
        Visibility = Visibility.Visible;

        // Defer focus to allow the control to complete layout after becoming visible
        _editorService.DispatcherQueue.TryEnqueue(() => SearchBox.Focus(FocusState.Programmatic));
    }

    /// <inheritdoc/>
    public void Hide()
    {
        Visibility = Visibility.Collapsed;
    }

    private void UpdateResultsList(string filter)
    {
        var entries = _recentFilesService.Entries.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            entries = entries.Where(e =>
                Path.GetFileName(e.FilePath).Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                e.FilePath.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        // Filter out files that are already open in the editor
        var openFilePaths = _documentService.Tabs
            .Where(t => !string.IsNullOrWhiteSpace(t.FilePath))
            .Select(t => t.FilePath!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        entries = entries.Where(e => !openFilePaths.Contains(e.FilePath));

        _filteredEntries = entries.ToList();

        ResultsPanel.Children.Clear();

        if (_filteredEntries.Count == 0)
        {
            EmptyMessage.Visibility = Visibility.Visible;
            _selectedIndex = -1;
            return;
        }

        EmptyMessage.Visibility = Visibility.Collapsed;

        for (var i = 0; i < _filteredEntries.Count; i++)
        {
            var entry = _filteredEntries[i];
            var fileName = Path.GetFileName(entry.FilePath);
            var directory = Path.GetDirectoryName(entry.FilePath) ?? string.Empty;

            // Outer grid for item + delete button
            var itemGrid = new Grid
            {
                Tag = entry,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
            };
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            itemGrid.PointerEntered += ItemPanel_PointerEntered;
            itemGrid.PointerExited += ItemPanel_PointerExited;
            itemGrid.PointerPressed += ItemPanel_PointerPressed;

            // Content panel
            var contentPanel = new StackPanel
            {
                Padding = new Thickness(12, 8, 8, 8),
                Spacing = 2
            };

            contentPanel.Children.Add(new TextBlock
            {
                Text = fileName,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });

            contentPanel.Children.Add(new TextBlock
            {
                Text = directory,
                FontSize = 11,
                Opacity = 0.7,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            Grid.SetColumn(contentPanel, 0);
            itemGrid.Children.Add(contentPanel);

            // Delete button
            var deleteButton = new Button
            {
                Content = new FontIcon { Glyph = "\uE711", FontSize = 12 },
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed,
                Tag = entry
            };
            deleteButton.Click += DeleteButton_Click;
            ToolTipService.SetToolTip(deleteButton, "Remove from recent files");
            Grid.SetColumn(deleteButton, 1);
            itemGrid.Children.Add(deleteButton);

            ResultsPanel.Children.Add(itemGrid);
        }

        SetSelectedIndex(0);
    }

    private void SetSelectedIndex(int index)
    {
        if (_selectedIndex >= 0 && _selectedIndex < ResultsPanel.Children.Count)
        {
            if (ResultsPanel.Children[_selectedIndex] is Grid oldGrid)
            {
                oldGrid.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            }
        }

        _selectedIndex = index;

        if (_selectedIndex >= 0 && _selectedIndex < ResultsPanel.Children.Count)
        {
            if (ResultsPanel.Children[_selectedIndex] is Grid newGrid)
            {
                newGrid.Background = (Brush)Resources["ListViewItemBackgroundSelected"];
            }
        }
    }

    private static void ShowDeleteButton(Grid itemGrid, bool show)
    {
        // Find the delete button in the grid
        foreach (var child in itemGrid.Children)
        {
            if (child is Button button && button.Tag is RecentFileEntry)
            {
                button.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                break;
            }
        }
    }

    private void ItemPanel_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid grid)
        {
            var index = ResultsPanel.Children.IndexOf(grid);
            if (index >= 0)
            {
                SetSelectedIndex(index);
                ShowDeleteButton(grid, true);
            }
        }
    }

    private void ItemPanel_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid grid)
        {
            ShowDeleteButton(grid, false);
        }
    }

    private async void ItemPanel_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid { Tag: RecentFileEntry entry })
        {
            await OpenFileAsync(entry);
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: RecentFileEntry entry })
        {
            _recentFilesService.RemoveFile(entry.FilePath);
            UpdateResultsList(SearchBox.Text);
        }
    }

    private void ClearAllButton_Click(object sender, RoutedEventArgs e)
    {
        _recentFilesService.Clear();
        UpdateResultsList(SearchBox.Text);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateResultsList(SearchBox.Text);
    }

    private async void SearchBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Down:
                if (_filteredEntries.Count > 0)
                {
                    SetSelectedIndex((_selectedIndex + 1) % _filteredEntries.Count);
                }
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.Up:
                if (_filteredEntries.Count > 0)
                {
                    SetSelectedIndex(_selectedIndex <= 0 ? _filteredEntries.Count - 1 : _selectedIndex - 1);
                }
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.Delete:
                if (_selectedIndex >= 0 && _selectedIndex < _filteredEntries.Count)
                {
                    _recentFilesService.RemoveFile(_filteredEntries[_selectedIndex].FilePath);
                    UpdateResultsList(SearchBox.Text);
                }
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.Enter:
                if (_selectedIndex >= 0 && _selectedIndex < _filteredEntries.Count)
                {
                    await OpenFileAsync(_filteredEntries[_selectedIndex]);
                }
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.Escape:
                Hide();
                _editorService.FocusEditor();
                e.Handled = true;
                break;
        }
    }

    private async Task OpenFileAsync(RecentFileEntry entry)
    {
        Hide();

        // Check if file exists
        if (!File.Exists(entry.FilePath))
        {
            // Remove from recent files if it no longer exists
            _recentFilesService.RemoveFile(entry.FilePath);
            return;
        }

        await _documentService.OpenFileAsync(entry.FilePath);
        _editorService.FocusEditor();
    }
}
