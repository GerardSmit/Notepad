using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Notepad.Abstractions.Models;
using Notepad.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace Notepad.ViewModels;

/// <summary>
/// ViewModel for the main window.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IFileService _fileService;
    private readonly IDialogService _dialogService;
    private readonly SessionService _sessionService;

    /// <summary>
    /// Gets the collection of document tabs.
    /// </summary>
    public ObservableCollection<DocumentTab> Tabs { get; } = [];

    /// <summary>
    /// Gets or sets the currently selected tab.
    /// </summary>
    [ObservableProperty]
    public partial DocumentTab? SelectedTab { get; set; }

    /// <summary>
    /// Gets or sets the window handle for file picker initialization.
    /// </summary>
    public nint WindowHandle { get; set; }

    /// <summary>
    /// Action to call after saving to sync editor save point.
    /// </summary>
    public Action? OnFileSaved { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel"/> class.
    /// </summary>
    /// <param name="fileService">The file service.</param>
    /// <param name="dialogService">The dialog service.</param>
    /// <param name="sessionService">The session service.</param>
    public MainViewModel(IFileService fileService, IDialogService dialogService, SessionService sessionService)
    {
        _fileService = fileService;
        _dialogService = dialogService;
        _sessionService = sessionService;

        CreateNewTab();
    }

    /// <summary>
    /// Creates a new empty tab.
    /// </summary>
    [RelayCommand]
    public void CreateNewTab()
    {
        var tab = new DocumentTab
        {
            Title = $"Untitled {Tabs.Count + 1}"
        };

        Tabs.Add(tab);
        SelectedTab = tab;
    }

    /// <summary>
    /// Opens a file in a new tab.
    /// </summary>
    [RelayCommand]
    public async Task OpenFileAsync()
    {
        var picker = new FileOpenPicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add(".txt");
        picker.FileTypeFilter.Add(".md");
        picker.FileTypeFilter.Add(".log");
        picker.FileTypeFilter.Add(".xml");
        picker.FileTypeFilter.Add(".json");
        picker.FileTypeFilter.Add(".cs");
        picker.FileTypeFilter.Add(".xaml");
        picker.FileTypeFilter.Add("*");

        WinRT.Interop.InitializeWithWindow.Initialize(picker, WindowHandle);

        var file = await picker.PickSingleFileAsync();

        if (file is not null)
        {
            await OpenFileInNewTabAsync(file);
        }
    }

    /// <summary>
    /// Opens a file in a new tab.
    /// </summary>
    /// <param name="file">The file to open.</param>
    public async Task OpenFileInNewTabAsync(StorageFile file)
    {
        var content = await _fileService.ReadFileAsync(file);

        var tab = new DocumentTab
        {
            Title = file.Name,
            Content = content,
            FilePath = file.Path,
            IsModified = false
        };

        Tabs.Add(tab);
        SelectedTab = tab;
    }

    /// <summary>
    /// Saves the current tab.
    /// </summary>
    [RelayCommand]
    public async Task SaveAsync()
    {
        if (SelectedTab is null) return;

        if (string.IsNullOrEmpty(SelectedTab.FilePath))
        {
            await SaveAsAsync();
        }
        else
        {
            await _fileService.WriteFileAsync(SelectedTab.FilePath, SelectedTab.Content);
            SelectedTab.IsModified = false;
            OnFileSaved?.Invoke();
        }
    }

    /// <summary>
    /// Saves the current tab with a new file name.
    /// </summary>
    [RelayCommand]
    public async Task SaveAsAsync()
    {
        if (SelectedTab is null) return;

        var picker = new FileSavePicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeChoices.Add("Text Document", new[] { ".txt" });
        picker.FileTypeChoices.Add("Markdown", new[] { ".md" });
        picker.FileTypeChoices.Add("All Files", new[] { "." });
        picker.SuggestedFileName = SelectedTab.Title;

        WinRT.Interop.InitializeWithWindow.Initialize(picker, WindowHandle);

        var file = await picker.PickSaveFileAsync();

        if (file is not null)
        {
            await _fileService.WriteFileAsync(file.Path, SelectedTab.Content);
            SelectedTab.FilePath = file.Path;
            SelectedTab.Title = file.Name;
            SelectedTab.IsModified = false;
            OnFileSaved?.Invoke();
        }
    }

    /// <summary>
    /// Closes the specified tab.
    /// </summary>
    /// <param name="tab">The tab to close.</param>
    [RelayCommand]
    public async Task CloseTabAsync(DocumentTab? tab)
    {
        if (tab is null) return;

        if (tab.IsModified)
        {
            var result = await _dialogService.ShowSaveConfirmationAsync(tab.Title);

            switch (result)
            {
                case SaveConfirmationResult.Save:
                    var previousTab = SelectedTab;
                    SelectedTab = tab;
                    await SaveAsync();
                    SelectedTab = previousTab;
                    break;
                case SaveConfirmationResult.Cancel:
                    return;
                case SaveConfirmationResult.DontSave:
                    break;
            }
        }

        // Clean up temporary session file for this tab
        _sessionService.DeleteTabSession(tab.Id);

        var index = Tabs.IndexOf(tab);
        Tabs.Remove(tab);

        if (Tabs.Count == 0)
        {
            CreateNewTab();
        }
        else if (SelectedTab == tab)
        {
            SelectedTab = Tabs[Math.Max(0, index - 1)];
        }
    }

    /// <summary>
    /// Updates the content of the selected tab.
    /// </summary>
    /// <param name="content">The new content buffer.</param>
    public void UpdateContent(IBuffer content)
    {
        if (SelectedTab is not null && SelectedTab.Content != content)
        {
            SelectedTab.Content = content;
            SelectedTab.IsModified = true;
        }
    }
}
