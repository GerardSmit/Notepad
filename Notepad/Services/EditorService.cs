using Microsoft.UI.Dispatching;
using Notepad.Abstractions.Services;

namespace Notepad.Services;

/// <summary>
/// Service for editor-related operations.
/// </summary>
public sealed class EditorService : IEditorService
{
    private DispatcherQueue? _dispatcherQueue;
    private Action? _focusEditorAction;

    /// <inheritdoc/>
    public DispatcherQueue DispatcherQueue => _dispatcherQueue ?? throw new InvalidOperationException("DispatcherQueue has not been set. Call SetDispatcherQueue first.");

    /// <inheritdoc/>
    public void FocusEditor()
    {
        _focusEditorAction?.Invoke();
    }

    /// <summary>
    /// Sets the dispatcher queue.
    /// </summary>
    /// <param name="dispatcherQueue">The dispatcher queue.</param>
    public void SetDispatcherQueue(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }

    /// <summary>
    /// Sets the action to focus the editor.
    /// </summary>
    /// <param name="action">The focus action.</param>
    public void SetFocusEditorAction(Action action)
    {
        _focusEditorAction = action;
    }
}
