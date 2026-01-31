using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Notepad.Abstractions.Plugins;
using Notepad.Abstractions.Services;

namespace Notepad.DefaultPlugins.GoToLine;

/// <summary>
/// A user control that provides Go to Line functionality as a plugin.
/// </summary>
public sealed partial class GoToLinePluginControl : IPluginControl
{
    private readonly IDocumentService _documentService;
    private readonly IEditorService _editorService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GoToLinePluginControl"/> class.
    /// </summary>
    public GoToLinePluginControl(IDocumentService documentService, IEditorService editorService)
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
        var editor = _documentService.CurrentEditor;
        if (editor is null) return;

        var lineCount = editor.Editor.LineCount;
        HintText.Text = $"Enter line number (1-{lineCount}) or line:column (e.g. 10 or 10:5)";

        InputTextBox.Text = string.Empty;
        Visibility = Visibility.Visible;

        _editorService.DispatcherQueue.TryEnqueue(() => InputTextBox.Focus(FocusState.Programmatic));
    }

    /// <inheritdoc/>
    public void Hide()
    {
        Visibility = Visibility.Collapsed;
    }

    private void ExecuteGoToLine()
    {
        var editor = _documentService.CurrentEditor;
        if (editor is null)
        {
            Hide();
            return;
        }

        var input = InputTextBox.Text.Trim();
        if (string.IsNullOrEmpty(input))
        {
            Hide();
            return;
        }

        int line;
        int column = 1;

        if (input.Contains(':'))
        {
            var parts = input.Split(':');
            if (parts.Length >= 2 &&
                int.TryParse(parts[0], out line) &&
                int.TryParse(parts[1], out column))
            {
            }
            else
            {
                if (!int.TryParse(parts[0], out line))
                {
                    Hide();
                    return;
                }
            }
        }
        else
        {
            if (!int.TryParse(input, out line))
            {
                Hide();
                return;
            }
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

        Hide();
    }

    #region Event Handlers

    private void InputTextBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            ExecuteGoToLine();
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            Hide();
            e.Handled = true;
        }
    }

    private void InputTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter || e.Key == Windows.System.VirtualKey.Escape)
        {
            e.Handled = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void Go_Click(object sender, RoutedEventArgs e)
    {
        ExecuteGoToLine();
    }

    #endregion
}
