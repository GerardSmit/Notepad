using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Notepad.Abstractions.Models;
using Notepad.Abstractions.Plugins;
using Notepad.Abstractions.Services;

namespace Notepad.DefaultPlugins.QuickOpen;

/// <summary>
/// A user control that provides Quick Open (file switching) functionality as a plugin.
/// </summary>
public sealed partial class QuickOpenPluginControl : IPluginControl
{
    private readonly IDocumentService _documentService;
    private readonly IEditorService _editorService;
    private List<DocumentTab> _tabs = [];
    private List<DocumentTab> _filteredTabs = [];
    private DocumentTab? _selectedTab;
    private int _selectedIndex = -1;
    private bool _mouseMovedSinceOpen;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuickOpenPluginControl"/> class.
    /// </summary>
    public QuickOpenPluginControl(IDocumentService documentService, IEditorService editorService)
    {
        _documentService = documentService;
        _editorService = editorService;
        InitializeComponent();
    }

    /// <inheritdoc/>
    public bool IsOpen => Visibility == Visibility.Visible;

    /// <inheritdoc/>
    public bool AutoHide => true;

    /// <inheritdoc/>
    public void Show()
    {
        _tabs = _documentService.Tabs.ToList();
        _selectedTab = _documentService.SelectedTab;

        SearchBox.Text = string.Empty;
        UpdateResultsList(string.Empty);
        Visibility = Visibility.Visible;
        _mouseMovedSinceOpen = false;

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
        IEnumerable<DocumentTab> filteredTabs = _tabs;

        if (!string.IsNullOrWhiteSpace(filter))
        {
            filteredTabs = filteredTabs.Where(t =>
                t.Title.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                (t.FilePath?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var tabList = filteredTabs.ToList();
        _filteredTabs = tabList;

        ResultsPanel.Children.Clear();
        foreach (var tab in tabList)
        {
            var panel = new StackPanel 
            { 
                Padding = new Thickness(12, 8, 12, 8), 
                Spacing = 2,
                Tag = tab,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
            };
            panel.PointerEntered += ItemPanel_PointerEntered;
            panel.PointerPressed += ItemPanel_PointerPressed;
            
            panel.Children.Add(new TextBlock { Text = tab.DisplayTitle, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            if (!string.IsNullOrEmpty(tab.FilePath))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = tab.FilePath,
                    FontSize = 11,
                    Opacity = 0.7,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
            }
            ResultsPanel.Children.Add(panel);
        }

        if (tabList.Count > 0)
        {
            var idx = _selectedTab is not null ? tabList.IndexOf(_selectedTab) : -1;
            SetSelectedIndex(idx >= 0 ? idx : 0);
        }
        else
        {
            _selectedIndex = -1;
        }
    }

    private void SetSelectedIndex(int index)
    {
        if (_selectedIndex >= 0 && _selectedIndex < ResultsPanel.Children.Count)
        {
            if (ResultsPanel.Children[_selectedIndex] is StackPanel oldPanel)
            {
                oldPanel.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            }
        }

        _selectedIndex = index;

        if (_selectedIndex >= 0 && _selectedIndex < ResultsPanel.Children.Count)
        {
            if (ResultsPanel.Children[_selectedIndex] is StackPanel newPanel)
            {
                newPanel.Background = (Brush)Resources["ListViewItemBackgroundSelected"];
            }
        }
    }

    /// <summary>
    /// Tries to parse a file path with optional line and column numbers.
    /// Supports formats: C:\path\file.txt, C:\path\file.txt:7, C:\path\file.txt:7:2
    /// </summary>
    private static (string? FilePath, int Line, int Column) TryParseFilePath(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return (null, 0, 0);
        }

        // Regex pattern for Windows absolute path with optional :line or :line:column
        // Matches: C:\path\file.txt or C:\path\file.txt:7 or C:\path\file.txt:7:2
        var match = FilePathRegex().Match(input.Trim());
        if (!match.Success)
        {
            return (null, 0, 0);
        }

        var filePath = match.Groups["path"].Value;
        var line = 0;
        var column = 0;

        if (match.Groups["line"].Success)
        {
            int.TryParse(match.Groups["line"].Value, out line);
        }

        if (match.Groups["column"].Success)
        {
            int.TryParse(match.Groups["column"].Value, out column);
        }

        return (filePath, line, column);
    }

    [GeneratedRegex(@"^(?<path>[A-Za-z]:\\[^:*?""<>|\r\n]+?)(?::(?<line>\d+))?(?::(?<column>\d+))?$")]
    private static partial Regex FilePathRegex();

    private void SelectCurrentItem()
    {
        _ = SelectCurrentItemAsync();
    }

    private async Task SelectCurrentItemAsync()
    {
        var searchText = SearchBox.Text.Trim();
        var (filePath, line, column) = TryParseFilePath(searchText);

        if (filePath is not null)
        {
            Hide();

            var existingTab = _documentService.Tabs.FirstOrDefault(t =>
                string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

            DocumentTab? tab;
            if (existingTab is not null)
            {
                _documentService.SelectTab(existingTab);
                tab = existingTab;
            }
            else
            {
                // Open the file
                tab = await _documentService.OpenFileAsync(filePath);
            }

            // Navigate to line/column if specified
            if (tab is not null && line > 0)
            {
                // Defer navigation to ensure editor is ready
                _editorService.DispatcherQueue.TryEnqueue(() => NavigateToPosition(line, column));
            }

            return;
        }

        // Default behavior: select from filtered tabs list
        if (_selectedIndex >= 0 && _selectedIndex < _filteredTabs.Count)
        {
            Hide();
            _documentService.SelectTab(_filteredTabs[_selectedIndex]);
        }
    }

    private void NavigateToPosition(int line, int column)
    {
        var editor = _documentService.CurrentEditor;
        if (editor is null)
        {
            return;
        }

        var lineCount = (int)editor.Editor.LineCount;
        line = Math.Clamp(line, 1, lineCount);
        column = Math.Max(1, column);

        var targetLine = line - 1;
        var lineStartPos = (int)editor.Editor.PositionFromLine(targetLine);

        int lineEndPos;
        if (targetLine < lineCount - 1)
        {
            lineEndPos = (int)editor.Editor.PositionFromLine(targetLine + 1) - 1;
        }
        else
        {
            lineEndPos = (int)editor.Editor.TextLength;
        }

        var lineLength = lineEndPos - lineStartPos;
        var targetColumn = Math.Min(column - 1, lineLength);
        var targetPos = lineStartPos + targetColumn;

        editor.Editor.GotoPos(targetPos);
        editor.Editor.ScrollCaret();
    }

    #region Event Handlers

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateResultsList(SearchBox.Text);
    }

    private void SearchBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var isShiftPressed = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        switch (e.Key)
        {
            case Windows.System.VirtualKey.Enter:
                SelectCurrentItem();
                e.Handled = true;
                break;
            case Windows.System.VirtualKey.Tab:
                if (_filteredTabs.Count > 0)
                {
                    if (isShiftPressed)
                    {
                        // Shift+Tab: Move to previous item
                        SetSelectedIndex(Math.Max(_selectedIndex - 1, 0));
                    }
                    else
                    {
                        // Tab: Move to next item
                        SetSelectedIndex(Math.Min(_selectedIndex + 1, _filteredTabs.Count - 1));
                    }
                }
                e.Handled = true;
                break;
            case Windows.System.VirtualKey.Down:
                if (_filteredTabs.Count > 0)
                {
                    SetSelectedIndex(Math.Min(_selectedIndex + 1, _filteredTabs.Count - 1));
                }
                e.Handled = true;
                break;
            case Windows.System.VirtualKey.Up:
                if (_filteredTabs.Count > 0)
                {
                    SetSelectedIndex(Math.Max(_selectedIndex - 1, 0));
                }
                e.Handled = true;
                break;
            case Windows.System.VirtualKey.Escape:
                Hide();
                e.Handled = true;
                break;
        }
    }

    private void ItemPanel_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        // Only respond to hover if the mouse has actually moved since opening
        if (!_mouseMovedSinceOpen)
        {
            _mouseMovedSinceOpen = true;
            return;
        }

        if (sender is StackPanel panel)
        {
            var index = ResultsPanel.Children.IndexOf(panel);
            if (index >= 0)
            {
                SetSelectedIndex(index);
            }
        }
    }

    private void ItemPanel_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is StackPanel panel && panel.Tag is DocumentTab tab)
        {
            Hide();
            _documentService.SelectTab(tab);
        }
    }

    #endregion
}
