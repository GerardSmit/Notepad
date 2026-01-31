using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Windows.Storage.Streams;

namespace Notepad.Abstractions.Models;

/// <summary>
/// Represents a document tab in the notepad.
/// </summary>
public partial class DocumentTab : ObservableObject
{
    /// <summary>
    /// Gets or sets the unique identifier for this tab.
    /// </summary>
    [ObservableProperty]
    public partial Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the title of the document.
    /// </summary>
    [ObservableProperty]
    public partial string Title { get; set; } = "Untitled";

    /// <summary>
    /// Gets or sets the content of the document as a UTF-8 buffer.
    /// </summary>
    [ObservableProperty]
    public partial IBuffer Content { get; set; } = new Windows.Storage.Streams.Buffer(0);

    /// <summary>
    /// Gets or sets the file path of the document.
    /// </summary>
    [ObservableProperty]
    public partial string? FilePath { get; set; }

    /// <summary>
    /// Gets a value indicating whether the document has a file path (for UI visibility binding).
    /// </summary>
    public Visibility HasFilePath => string.IsNullOrEmpty(FilePath) ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>
    /// Gets or sets a value indicating whether the document has unsaved changes.
    /// </summary>
    [ObservableProperty]
    public partial bool IsModified { get; set; }

    /// <summary>
    /// Gets the display title including modification indicator.
    /// </summary>
    public string DisplayTitle => IsModified ? $"{Title}*" : Title;

    partial void OnFilePathChanged(string? value)
    {
        OnPropertyChanged(nameof(HasFilePath));
    }

    partial void OnIsModifiedChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayTitle));
    }

    partial void OnTitleChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayTitle));
    }
}
