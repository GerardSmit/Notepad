namespace Notepad.Abstractions.Services;

/// <summary>
/// Provides access to editor focus management.
/// </summary>
public interface IEditorService
{
    /// <summary>
    /// Gets the dispatcher queue for scheduling UI operations.
    /// </summary>
    Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; }

    /// <summary>
    /// Requests focus to be returned to the current editor.
    /// </summary>
    void FocusEditor();
}
