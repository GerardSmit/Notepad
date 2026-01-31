using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Notepad.Abstractions.Plugins;
using Notepad.Abstractions.Services;
using Notepad.DefaultPlugins.Services;
using WinUIEditor;

namespace Notepad.DefaultPlugins.FindReplace;

/// <summary>
/// A user control that provides Find and Replace functionality as a plugin.
/// </summary>
public sealed partial class FindReplacePluginControl : IPluginControl
{
    private readonly IDocumentService _documentService;
    private readonly IEditorService _editorService;
    private int _totalMatchCount;
    private bool _hasMoreMatches;
    private int _currentMatchIndex = -1;
    private int _selectionStartForInSelection;
    private int _selectionEndForInSelection;
    private const int MatchIndicator = 8;
    private bool _showReplace;

    // Debounce and async search support
    private CancellationTokenSource? _countCts;
    private CancellationTokenSource? _debounceCts;
    private readonly object _searchLock = new();
    private CodeEditorControl? _subscribedEditor;
    private const int SearchDebounceMs = 150;

    // Scroll/highlight optimization
    private long _lastHighlightedStart = -1;
    private long _lastHighlightedEnd = -1;
    private bool _highlightsNeedFullRefresh = true;
    private CancellationTokenSource? _scrollDebounce;
    private const int MaxHighlightMatches = 500; // Limit visual highlighting per visible range
    private const int MaxCountMatches = 99999; // Limit for counting (show "99999+" for more)

    /// <summary>
    /// Initializes a new instance of the <see cref="FindReplacePluginControl"/> class.
    /// </summary>
    public FindReplacePluginControl(IDocumentService documentService, IEditorService editorService)
    {
        _documentService = documentService;
        _editorService = editorService;
        InitializeComponent();
    }

    /// <inheritdoc/>
    public bool IsOpen => Visibility == Visibility.Visible;

    /// <inheritdoc/>
    public bool AutoHide => false;

    /// <summary>
    /// Gets or sets whether to show the replace row when showing the control.
    /// </summary>
    public bool ShowReplace
    {
        get => _showReplace;
        set => _showReplace = value;
    }

    private CodeEditorControl? CurrentEditor => _documentService.CurrentEditor;

    /// <inheritdoc/>
    public void Show()
    {
        var editor = CurrentEditor;
        if (editor is not null)
        {
            var selStart = (int)editor.Editor.SelectionStart;
            var selEnd = (int)editor.Editor.SelectionEnd;

            if (selEnd > selStart)
            {
                _selectionStartForInSelection = selStart;
                _selectionEndForInSelection = selEnd;
            }

            // Pre-fill search box with selected text if it's a single line
            var selectedText = GetSelectedTextSingleLine(editor);
            if (!string.IsNullOrEmpty(selectedText))
            {
                FindTextBox.Text = selectedText;
            }
        }

        ReplaceRow.Visibility = _showReplace ? Visibility.Visible : Visibility.Collapsed;
        FindReplaceToggleIcon.Glyph = _showReplace ? "\uE70D" : "\uE76C";

        Visibility = Visibility.Visible;

        SubscribeToEditorEvents();

        // Defer focus to allow the control to complete layout after becoming visible
        _editorService.DispatcherQueue.TryEnqueue(() =>
        {
            FindTextBox.Focus(FocusState.Programmatic);
            FindTextBox.SelectAll();
        });

        UpdateFindMatches();
    }

    /// <inheritdoc/>
    public void Hide()
    {
        Visibility = Visibility.Collapsed;
        CancelPendingSearch();
        UnsubscribeFromEditorEvents();
        ClearHighlights();
    }

    /// <summary>
    /// Called when the editor changes to update the find matches.
    /// </summary>
    public void OnEditorChanged()
    {
        if (IsOpen)
        {
            UnsubscribeFromEditorEvents();
            SubscribeToEditorEvents();
            UpdateFindMatches();
        }
    }

    private void SubscribeToEditorEvents()
    {
        var editor = CurrentEditor;
        if (editor is null || editor == _subscribedEditor) return;

        UnsubscribeFromEditorEvents();
        _subscribedEditor = editor;
        editor.Editor.UpdateUI += Editor_UpdateUI;
    }

    private void UnsubscribeFromEditorEvents()
    {
        if (_subscribedEditor is not null)
        {
            _subscribedEditor.Editor.UpdateUI -= Editor_UpdateUI;
            _subscribedEditor = null;
        }
    }

    private void Editor_UpdateUI(Editor sender, UpdateUIEventArgs args)
    {
        // Re-highlight when scrolling (SC_UPDATE_V_SCROLL = 0x04 or SC_UPDATE_H_SCROLL = 0x08)
        var scrollMask = (int)Update.VScroll | (int)Update.HScroll;
        if ((args.Updated & scrollMask) != 0)
        {
            // Debounce scroll events to avoid excessive re-highlighting
            _scrollDebounce?.Cancel();
            _scrollDebounce = new CancellationTokenSource();
            var token = _scrollDebounce.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(50, token);
                    if (!token.IsCancellationRequested)
                    {
                        _editorService.DispatcherQueue.TryEnqueue(HighlightVisibleMatches);
                    }
                }
                catch (OperationCanceledException) { }
            }, token);
        }
    }

    private void CancelPendingSearch()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = null;

        lock (_searchLock)
        {
            _countCts?.Cancel();
            _countCts?.Dispose();
            _countCts = null;
        }
        _scrollDebounce?.Cancel();
        _scrollDebounce?.Dispose();
        _scrollDebounce = null;
    }

    private static string? GetSelectedTextSingleLine(CodeEditorControl editor)
    {
        var selStart = editor.Editor.SelectionStart;
        var selEnd = editor.Editor.SelectionEnd;

        if (selEnd <= selStart) return null;

        // Use Scintilla's TargetFromSelection + GetTargetText to avoid UTF-8/UTF-16 mismatch
        // SelectionStart/End are UTF-8 byte positions, not UTF-16 char indices
        editor.Editor.TargetFromSelection();
        var selectedText = editor.Editor.GetTargetText();

        if (string.IsNullOrEmpty(selectedText)) return null;

        if (selectedText.Contains('\n') || selectedText.Contains('\r'))
        {
            return null;
        }

        return selectedText;
    }

    private FindOptions GetFindOptions()
    {
        return new FindOptions
        {
            MatchCase = MatchCaseToggle.IsChecked == true,
            MatchWholeWord = MatchWholeWordToggle.IsChecked == true,
            UseRegex = UseRegexToggle.IsChecked == true,
            InSelection = InSelectionToggle.IsChecked == true,
            SelectionStart = _selectionStartForInSelection,
            SelectionEnd = _selectionEndForInSelection
        };
    }

    private Task UpdateFindMatches()
    {
        return UpdateFindMatchesAsync();
    }

    private async Task UpdateFindMatchesAsync()
    {
        var editor = CurrentEditor;
        if (editor is null || string.IsNullOrEmpty(FindTextBox.Text))
        {
            _totalMatchCount = 0;
            _hasMoreMatches = false;
            _currentMatchIndex = -1;
            MatchCountText.Text = string.Empty;
            ClearHighlights();
            return;
        }

        var scintilla = editor.Editor;
        var options = GetFindOptions();
        var searchText = FindTextBox.Text;

        CancellationTokenSource cts;
        lock (_searchLock)
        {
            _countCts?.Cancel();
            _countCts?.Dispose();
            cts = new CancellationTokenSource();
            _countCts = cts;
        }

        var ct = cts.Token;

        // Show "searching..." for large files
        var textLength = scintilla.TextLength;
        if (textLength > 100_000)
        {
            MatchCountText.Text = "Searching...";
        }

        _highlightsNeedFullRefresh = true;
        _lastHighlightedStart = -1;
        _lastHighlightedEnd = -1;

        try
        {
            var (count, hasMore) = await Task.Run(() =>
            {
                return CountMatchesNative(scintilla, searchText, options, MaxCountMatches, ct);
            }, ct);

            if (ct.IsCancellationRequested)
            {
                return;
            }

            _totalMatchCount = count;
            _hasMoreMatches = hasMore;

            scintilla.IndicatorCurrent = MatchIndicator;
            scintilla.IndicatorClearRange(0, scintilla.TextLength);

            scintilla.IndicSetStyle(MatchIndicator, IndicatorStyle.RoundBox);
            scintilla.IndicSetFore(MatchIndicator, 0x00FFFF80); // Light yellow (BGR format)
            scintilla.IndicSetAlpha(MatchIndicator, (Alpha)100);
            scintilla.IndicSetOutlineAlpha(MatchIndicator, (Alpha)255);
            scintilla.IndicSetUnder(MatchIndicator, true);

            HighlightVisibleMatches();

            if (_totalMatchCount > 0)
            {
                var currentPos = scintilla.CurrentPos;
                var matchPos = FindNextMatchNative(scintilla, searchText, options, currentPos);
                if (matchPos >= 0)
                {
                    var (indexCount, _) = CountMatchesNative(scintilla, searchText, options, MaxCountMatches, ct, endPos: matchPos);
                    _currentMatchIndex = indexCount;
                }
                else
                {
                    _currentMatchIndex = 0;
                }
            }
            else
            {
                _currentMatchIndex = -1;
            }

            UpdateMatchCountDisplay();
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>
    /// Counts matches using Scintilla's native SearchInTarget.
    /// </summary>
    private static (int count, bool hasMore) CountMatchesNative(
        Editor scintilla,
        string searchText,
        FindOptions options,
        int maxCount,
        CancellationToken ct,
        long? endPos = null)
    {
        var count = 0;
        long searchStart = options.InSelection ? options.SelectionStart : 0;
        var searchEnd = endPos ?? (options.InSelection ? options.SelectionEnd : scintilla.TextLength);

        var flags = GetScintillaSearchFlags(options);
        scintilla.SearchFlags = flags;

        var pos = searchStart;
        while (pos < searchEnd && count < maxCount)
        {
            ct.ThrowIfCancellationRequested();

            scintilla.SetTargetRange(pos, searchEnd);
            var foundPos = scintilla.SearchInTarget(Encoding.UTF8.GetByteCount(searchText), searchText);

            if (foundPos < 0)
            {
                break;
            }

            count++;
            pos = scintilla.TargetEnd;

            // Avoid infinite loop for zero-length matches
            if (scintilla.TargetEnd <= scintilla.TargetStart)
            {
                pos++;
            }
        }

        var hasMore = count >= maxCount && pos < searchEnd;
        return (count, hasMore);
    }

    /// <summary>
    /// Finds the next match starting from a position using native Scintilla search.
    /// Returns the start position of the match, or -1 if not found.
    /// </summary>
    private static long FindNextMatchNative(Editor scintilla, string searchText, FindOptions options, long startPos)
    {
        var searchEnd = options.InSelection ? options.SelectionEnd : scintilla.TextLength;

        var flags = GetScintillaSearchFlags(options);
        scintilla.SearchFlags = flags;

        scintilla.SetTargetRange(startPos, searchEnd);
        var foundPos = scintilla.SearchInTarget(Encoding.UTF8.GetByteCount(searchText), searchText);

        return foundPos;
    }

    /// <summary>
    /// Finds the previous match before a position using native Scintilla search.
    /// Returns the start position of the match, or -1 if not found.
    /// </summary>
    private static long FindPreviousMatchNative(Editor scintilla, string searchText, FindOptions options, long beforePos)
    {
        var searchStart = options.InSelection ? options.SelectionStart : 0;

        var flags = GetScintillaSearchFlags(options);
        scintilla.SearchFlags = flags;

        // Search backwards by setting target from end to start
        scintilla.SetTargetRange(beforePos, searchStart);
        var foundPos = scintilla.SearchInTarget(Encoding.UTF8.GetByteCount(searchText), searchText);

        return foundPos;
    }

    /// <summary>
    /// Converts FindOptions to Scintilla FindOption flags.
    /// </summary>
    private static FindOption GetScintillaSearchFlags(FindOptions options)
    {
        var flags = FindOption.None;

        if (options.MatchCase)
        {
            flags |= FindOption.MatchCase;
        }

        if (options.MatchWholeWord)
        {
            flags |= FindOption.WholeWord;
        }

        if (options.UseRegex)
        {
            flags |= FindOption.RegExp;
        }

        return flags;
    }

    private void UpdateMatchCountDisplay()
    {
        var suffix = _hasMoreMatches ? "+" : "";

        if (_totalMatchCount == 0)
        {
            MatchCountText.Text = string.IsNullOrEmpty(FindTextBox.Text) ? string.Empty : "No results";
        }
        else if (_currentMatchIndex >= 0)
        {
            MatchCountText.Text = $"{_currentMatchIndex + 1} of {_totalMatchCount}{suffix}";
        }
        else
        {
            MatchCountText.Text = $"{_totalMatchCount}{suffix} results";
        }
    }

    private void HighlightVisibleMatches()
    {
        var editor = CurrentEditor;
        if (editor is null || _totalMatchCount == 0 || string.IsNullOrEmpty(FindTextBox.Text)) return;

        var scintilla = editor.Editor;
        var searchText = FindTextBox.Text;
        var options = GetFindOptions();

        var firstVisibleDisplayLine = scintilla.FirstVisibleLine;
        var linesOnScreen = scintilla.LinesOnScreen;

        // Convert to document lines - DocLineFromVisible handles wrapping/folding
        var firstDocLine = scintilla.DocLineFromVisible(firstVisibleDisplayLine);
        var lastDocLine = scintilla.DocLineFromVisible(firstVisibleDisplayLine + linesOnScreen);

        // Add buffer for smooth scrolling
        var bufferLines = linesOnScreen;
        var bufferedFirstLine = Math.Max(0, firstDocLine - bufferLines);
        var bufferedLastLine = lastDocLine + bufferLines;

        var visibleStart = scintilla.PositionFromLine(bufferedFirstLine);
        var visibleEnd = scintilla.PositionFromLine(bufferedLastLine + 1);
        if (visibleEnd <= visibleStart)
        {
            visibleEnd = scintilla.TextLength;
        }

        var overlapThreshold = (visibleEnd - visibleStart) / 4;
        if (!_highlightsNeedFullRefresh &&
            visibleStart >= _lastHighlightedStart - overlapThreshold &&
            visibleEnd <= _lastHighlightedEnd + overlapThreshold)
        {
            return;
        }

        // Clear only the visible area we're about to update (much faster than full clear)
        scintilla.IndicatorCurrent = MatchIndicator;

        if (_highlightsNeedFullRefresh)
        {
            scintilla.IndicatorClearRange(0, scintilla.TextLength);
            _highlightsNeedFullRefresh = false;
        }

        scintilla.IndicSetStyle(MatchIndicator, IndicatorStyle.RoundBox);
        scintilla.IndicSetFore(MatchIndicator, 0x00FFFF80);
        scintilla.IndicSetAlpha(MatchIndicator, (Alpha)100);
        scintilla.IndicSetOutlineAlpha(MatchIndicator, (Alpha)255);
        scintilla.IndicSetUnder(MatchIndicator, true);

        var flags = GetScintillaSearchFlags(options);
        var searchByteCount = Encoding.UTF8.GetByteCount(searchText);

        var highlightCount = 0;
        var pos = visibleStart;
        while (pos < visibleEnd && highlightCount < MaxHighlightMatches)
        {
            scintilla.SearchFlags = flags;
            scintilla.SetTargetRange(pos, visibleEnd);
            var foundPos = scintilla.SearchInTarget(searchByteCount, searchText);

            if (foundPos < 0)
            {
                break;
            }

            // Save target values immediately - other Scintilla calls may modify them
            var matchStart = scintilla.TargetStart;
            var matchEnd = scintilla.TargetEnd;
            var matchLength = matchEnd - matchStart;

            scintilla.IndicatorFillRange(matchStart, matchLength);
            highlightCount++;

            pos = matchEnd;

            // Avoid infinite loop for zero-length matches
            if (matchLength <= 0)
            {
                pos++;
            }
        }

        _lastHighlightedStart = visibleStart;
        _lastHighlightedEnd = visibleEnd;
    }

    private void ClearHighlights()
    {
        _lastHighlightedStart = -1;
        _lastHighlightedEnd = -1;
        _highlightsNeedFullRefresh = true;

        var editor = CurrentEditor;
        if (editor is null) return;

        var scintilla = editor.Editor;
        scintilla.IndicatorCurrent = MatchIndicator;
        scintilla.IndicatorClearRange(0, scintilla.TextLength);
    }

    private void NavigateToNextMatch()
    {
        var editor = CurrentEditor;
        if (editor is null || _totalMatchCount == 0 || string.IsNullOrEmpty(FindTextBox.Text)) return;

        var scintilla = editor.Editor;
        var searchText = FindTextBox.Text;
        var options = GetFindOptions();

        var startPos = Math.Max(scintilla.SelectionEnd, scintilla.CurrentPos);

        var foundPos = FindNextMatchNative(scintilla, searchText, options, startPos);

        if (foundPos < 0)
        {
            foundPos = FindNextMatchNative(scintilla, searchText, options, 0);
        }

        if (foundPos >= 0)
        {
            scintilla.SetSel(scintilla.TargetStart, scintilla.TargetEnd);
            scintilla.ScrollCaret();

            _currentMatchIndex = (_currentMatchIndex + 1) % _totalMatchCount;
            if (_currentMatchIndex < 0) _currentMatchIndex = 0;
            UpdateMatchCountDisplay();
        }
    }

    private void NavigateToPreviousMatch()
    {
        var editor = CurrentEditor;
        if (editor is null || _totalMatchCount == 0 || string.IsNullOrEmpty(FindTextBox.Text)) return;

        var scintilla = editor.Editor;
        var searchText = FindTextBox.Text;
        var options = GetFindOptions();

        // Start searching backwards from current position
        var startPos = scintilla.SelectionStart;
        if (startPos <= 0)
        {
            startPos = scintilla.TextLength;
        }

        var foundPos = FindPreviousMatchNative(scintilla, searchText, options, startPos);

        if (foundPos < 0)
        {
            // Wrap around to end
            foundPos = FindPreviousMatchNative(scintilla, searchText, options, scintilla.TextLength);
        }

        if (foundPos >= 0)
        {
            scintilla.SetSel(scintilla.TargetStart, scintilla.TargetEnd);
            scintilla.ScrollCaret();

            // Update the current match index (approximately)
            _currentMatchIndex = (_currentMatchIndex - 1 + _totalMatchCount) % _totalMatchCount;
            UpdateMatchCountDisplay();
        }
    }

    private void FindNextMatch()
    {
        if (_totalMatchCount == 0)
        {
            UpdateFindMatches();
        }
        NavigateToNextMatch();
    }

    private void FindPreviousMatch()
    {
        if (_totalMatchCount == 0)
        {
            UpdateFindMatches();
        }
        NavigateToPreviousMatch();
    }

    private void ReplaceCurrentMatch()
    {
        var editor = CurrentEditor;
        if (editor is null || _totalMatchCount == 0 || string.IsNullOrEmpty(FindTextBox.Text))
        {
            return;
        }

        var scintilla = editor.Editor;
        var searchText = FindTextBox.Text;
        var replaceText = ReplaceTextBox.Text;
        var options = GetFindOptions();

        // Check if current selection matches the search text
        var selStart = scintilla.SelectionStart;
        var selEnd = scintilla.SelectionEnd;

        if (selEnd > selStart)
        {
            // Verify the selection is a match by searching at that position
            var flags = GetScintillaSearchFlags(options);
            scintilla.SearchFlags = flags;
            scintilla.SetTargetRange(selStart, selEnd);
            var foundPos = scintilla.SearchInTarget(Encoding.UTF8.GetByteCount(searchText), searchText);

            if (foundPos == selStart && scintilla.TargetEnd == selEnd)
            {
                var replaceByteCount = Encoding.UTF8.GetByteCount(replaceText);
                if (options.UseRegex)
                {
                    scintilla.ReplaceTargetRE(replaceByteCount, replaceText);
                }
                else
                {
                    scintilla.ReplaceTarget(replaceByteCount, replaceText);
                }

                UpdateFindMatches();
                NavigateToNextMatch();
                return;
            }
        }

        NavigateToNextMatch();
    }

    private void ReplaceAllMatches()
    {
        var editor = CurrentEditor;
        if (editor is null || _totalMatchCount == 0 || string.IsNullOrEmpty(FindTextBox.Text))
        {
            return;
        }

        var scintilla = editor.Editor;
        var searchText = FindTextBox.Text;
        var replaceText = ReplaceTextBox.Text;
        var options = GetFindOptions();
        var replaceCount = 0;

        var flags = GetScintillaSearchFlags(options);
        scintilla.SearchFlags = flags;

        long searchStart = options.InSelection ? options.SelectionStart : 0;
        long searchEnd = options.InSelection ? options.SelectionEnd : scintilla.TextLength;

        scintilla.BeginUndoAction();
        try
        {
            var pos = searchStart;
            while (pos < searchEnd)
            {
                scintilla.SetTargetRange(pos, searchEnd);
                var foundPos = scintilla.SearchInTarget(Encoding.UTF8.GetByteCount(searchText), searchText);

                if (foundPos < 0)
                {
                    break;
                }

                var matchLength = scintilla.TargetEnd - scintilla.TargetStart;

                var replaceByteCount = Encoding.UTF8.GetByteCount(replaceText);
                long replacedLength;
                if (options.UseRegex)
                {
                    replacedLength = scintilla.ReplaceTargetRE(replaceByteCount, replaceText);
                }
                else
                {
                    replacedLength = scintilla.ReplaceTarget(replaceByteCount, replaceText);
                }

                replaceCount++;

                // Adjust position and search end for the replacement length difference
                var lengthDelta = replacedLength - matchLength;
                pos = scintilla.TargetStart + replacedLength;
                searchEnd += lengthDelta;

                // Safety: prevent infinite loop for zero-length replacements
                if (replacedLength <= 0 && matchLength <= 0)
                {
                    pos++;
                }
            }
        }
        finally
        {
            scintilla.EndUndoAction();
        }

        UpdateFindMatches();
        MatchCountText.Text = $"Replaced {replaceCount} occurrences";
    }

    private bool HandleAltShortcuts(Windows.System.VirtualKey key)
    {
        switch (key)
        {
            case Windows.System.VirtualKey.C:
                MatchCaseToggle.IsChecked = !MatchCaseToggle.IsChecked;
                UpdateFindMatches();
                return true;
            case Windows.System.VirtualKey.W:
                MatchWholeWordToggle.IsChecked = !MatchWholeWordToggle.IsChecked;
                UpdateFindMatches();
                return true;
            case Windows.System.VirtualKey.R:
                UseRegexToggle.IsChecked = !UseRegexToggle.IsChecked;
                UpdateFindMatches();
                return true;
            case Windows.System.VirtualKey.L:
                InSelectionToggle.IsChecked = !InSelectionToggle.IsChecked;
                if (InSelectionToggle.IsChecked == true)
                {
                    var editor = CurrentEditor;
                    if (editor is not null)
                    {
                        var selStart = (int)editor.Editor.SelectionStart;
                        var selEnd = (int)editor.Editor.SelectionEnd;
                        if (selEnd > selStart)
                        {
                            _selectionStartForInSelection = selStart;
                            _selectionEndForInSelection = selEnd;
                        }
                    }
                }
                UpdateFindMatches();
                return true;
        }
        return false;
    }

    #region Event Handlers

    private void FindReplaceToggle_Click(object sender, RoutedEventArgs e)
    {
        var isReplaceVisible = ReplaceRow.Visibility == Visibility.Visible;
        ReplaceRow.Visibility = isReplaceVisible ? Visibility.Collapsed : Visibility.Visible;
        FindReplaceToggleIcon.Glyph = isReplaceVisible ? "\uE76C" : "\uE70D";
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void FindPrevious_Click(object sender, RoutedEventArgs e)
    {
        FindPreviousMatch();
    }

    private void FindNext_Click(object sender, RoutedEventArgs e)
    {
        FindNextMatch();
    }

    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        ReplaceCurrentMatch();
    }

    private void ReplaceAll_Click(object sender, RoutedEventArgs e)
    {
        ReplaceAllMatches();
    }

    private void FindTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Use debounced search to avoid blocking UI on rapid typing
        UpdateFindMatchesDebounced();
    }

    private void UpdateFindMatchesDebounced()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = new CancellationTokenSource();
        var ct = _debounceCts.Token;

        _ = UpdateFindMatchesDebouncedAsync(ct);
    }

    private async Task UpdateFindMatchesDebouncedAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(SearchDebounceMs, ct);

            if (!ct.IsCancellationRequested)
            {
                await UpdateFindMatches();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void FindTextBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Handle Alt shortcuts first - using PreviewKeyDown to prevent system beep
        var altDown = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (altDown && HandleAltShortcuts(e.Key))
        {
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case Windows.System.VirtualKey.Enter:
                if (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
                    .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
                {
                    FindPreviousMatch();
                }
                else
                {
                    FindNextMatch();
                }
                e.Handled = true;
                break;
            case Windows.System.VirtualKey.Escape:
                Hide();
                e.Handled = true;
                break;
        }
    }

    private void ReplaceTextBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Handle Alt shortcuts first - using PreviewKeyDown to prevent system beep
        var altDown = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (altDown && HandleAltShortcuts(e.Key))
        {
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case Windows.System.VirtualKey.Enter:
                ReplaceCurrentMatch();
                e.Handled = true;
                break;
            case Windows.System.VirtualKey.Escape:
                Hide();
                e.Handled = true;
                break;
        }
    }

    private void FindOption_Changed(object sender, RoutedEventArgs e)
    {
        UpdateFindMatches();
    }

    private void InSelection_Changed(object sender, RoutedEventArgs e)
    {
        if (InSelectionToggle.IsChecked == true)
        {
            var editor = CurrentEditor;
            if (editor is not null)
            {
                var selStart = (int)editor.Editor.SelectionStart;
                var selEnd = (int)editor.Editor.SelectionEnd;

                if (selEnd > selStart)
                {
                    _selectionStartForInSelection = selStart;
                    _selectionEndForInSelection = selEnd;
                }
            }
        }

        UpdateFindMatches();
    }

    #endregion
}
