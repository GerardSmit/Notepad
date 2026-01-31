using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Notepad.Services;

/// <summary>
/// Service for showing dialogs.
/// </summary>
public sealed class DialogService : IDialogService
{
    private readonly XamlRoot _xamlRoot;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialogService"/> class.
    /// </summary>
    /// <param name="xamlRoot">The XAML root for dialogs.</param>
    public DialogService(XamlRoot xamlRoot)
    {
        _xamlRoot = xamlRoot;
    }

    /// <inheritdoc/>
    public async Task<SaveConfirmationResult> ShowSaveConfirmationAsync(string documentName)
    {
        var dialog = new ContentDialog
        {
            Title = "Save changes?",
            Content = $"Do you want to save changes to {documentName}?",
            PrimaryButtonText = "Save",
            SecondaryButtonText = "Don't Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _xamlRoot
        };

        var result = await dialog.ShowAsync();

        return result switch
        {
            ContentDialogResult.Primary => SaveConfirmationResult.Save,
            ContentDialogResult.Secondary => SaveConfirmationResult.DontSave,
            _ => SaveConfirmationResult.Cancel
        };
    }
}
