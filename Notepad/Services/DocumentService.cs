using Notepad.Abstractions.Models;
using Notepad.Abstractions.Services;
using WinUIEditor;

namespace Notepad.Services;

/// <summary>
/// Service for managing documents and tabs.
/// </summary>
public sealed class DocumentService : IDocumentService
{
    private Func<IReadOnlyList<DocumentTab>>? _tabsProvider;
    private Func<DocumentTab?>? _selectedTabProvider;
    private Action<DocumentTab>? _selectTabAction;
    private Func<CodeEditorControl?>? _currentEditorProvider;
    private Func<string, Task<DocumentTab?>>? _openFileAction;

    /// <inheritdoc/>
    public IReadOnlyList<DocumentTab> Tabs => _tabsProvider?.Invoke() ?? [];

    /// <inheritdoc/>
    public DocumentTab? SelectedTab
    {
        get => _selectedTabProvider?.Invoke();
        set
        {
            if (value is not null)
            {
                _selectTabAction?.Invoke(value);
            }
        }
    }

    /// <inheritdoc/>
    public CodeEditorControl? CurrentEditor => _currentEditorProvider?.Invoke();

    /// <inheritdoc/>
    public event EventHandler<DocumentTab?>? SelectedTabChanged;

    /// <inheritdoc/>
    public void SelectTab(DocumentTab tab)
    {
        _selectTabAction?.Invoke(tab);
    }

    /// <inheritdoc/>
    public async Task<DocumentTab?> OpenFileAsync(string filePath)
    {
        var existingTab = Tabs.FirstOrDefault(t =>
            string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

        if (existingTab is not null)
        {
            SelectTab(existingTab);
            return existingTab;
        }

        if (_openFileAction is not null)
        {
            return await _openFileAction(filePath);
        }

        return null;
    }

    /// <summary>
    /// Sets the provider for the tabs collection.
    /// </summary>
    /// <param name="provider">The provider function.</param>
    public void SetTabsProvider(Func<IReadOnlyList<DocumentTab>> provider)
    {
        _tabsProvider = provider;
    }

    /// <summary>
    /// Sets the provider for the selected tab.
    /// </summary>
    /// <param name="provider">The provider function.</param>
    public void SetSelectedTabProvider(Func<DocumentTab?> provider)
    {
        _selectedTabProvider = provider;
    }

    /// <summary>
    /// Sets the action to select a tab.
    /// </summary>
    /// <param name="action">The action to invoke.</param>
    public void SetSelectTabAction(Action<DocumentTab> action)
    {
        _selectTabAction = action;
    }

    /// <summary>
    /// Sets the provider for the current editor.
    /// </summary>
    /// <param name="provider">The provider function.</param>
    public void SetCurrentEditorProvider(Func<CodeEditorControl?> provider)
    {
        _currentEditorProvider = provider;
    }

    /// <summary>
    /// Sets the action to open a file by path.
    /// </summary>
    /// <param name="action">The action to invoke.</param>
    public void SetOpenFileAction(Func<string, Task<DocumentTab?>> action)
    {
        _openFileAction = action;
    }

    /// <summary>
    /// Notifies the service that the selected tab has changed from an external source.
    /// </summary>
    /// <param name="tab">The newly selected tab.</param>
    public void NotifySelectedTabChanged(DocumentTab? tab)
    {
        SelectedTabChanged?.Invoke(this, tab);
    }
}
