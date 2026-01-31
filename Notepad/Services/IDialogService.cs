namespace Notepad.Services;

/// <summary>
/// Result of a save confirmation dialog.
/// </summary>
public enum SaveConfirmationResult
{
    /// <summary>
    /// User wants to save the file.
    /// </summary>
    Save,

    /// <summary>
    /// User wants to discard changes.
    /// </summary>
    DontSave,

    /// <summary>
    /// User cancelled the operation.
    /// </summary>
    Cancel
}

/// <summary>
/// Service interface for dialogs.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows a save confirmation dialog.
    /// </summary>
    /// <param name="documentName">The name of the document.</param>
    /// <returns>The user's choice.</returns>
    Task<SaveConfirmationResult> ShowSaveConfirmationAsync(string documentName);
}
