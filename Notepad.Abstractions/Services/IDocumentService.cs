using Notepad.Abstractions.Models;
using WinUIEditor;

namespace Notepad.Abstractions.Services;

/// <summary>
/// Provides access to document/tab management.
/// </summary>
public interface IDocumentService
{
    /// <summary>
    /// Gets all open document tabs.
    /// </summary>
    IReadOnlyList<DocumentTab> Tabs { get; }

    /// <summary>
    /// Gets or sets the currently selected tab.
    /// </summary>
    DocumentTab? SelectedTab { get; set; }

    /// <summary>
    /// Gets the current code editor control.
    /// </summary>
    CodeEditorControl? CurrentEditor { get; }

    /// <summary>
    /// Event raised when the selected tab changes.
    /// </summary>
    event EventHandler<DocumentTab?>? SelectedTabChanged;

    /// <summary>
    /// Selects the specified tab.
    /// </summary>
    /// <param name="tab">The tab to select.</param>
    void SelectTab(DocumentTab tab);

    /// <summary>
    /// Opens a file by its path. If the file is already open, selects that tab.
    /// </summary>
    /// <param name="filePath">The absolute path to the file.</param>
    /// <returns>The opened document tab, or null if the operation failed.</returns>
    Task<DocumentTab?> OpenFileAsync(string filePath);
}
