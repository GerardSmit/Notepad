using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Text.Json.Serialization;
using Notepad.Abstractions;
using Notepad.Abstractions.Models;
using Windows.Storage.Streams;

namespace Notepad.Services;

/// <summary>
/// JSON serializer context for AOT-compatible serialization.
/// </summary>
[JsonSerializable(typeof(SessionService.SessionState))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal sealed partial class SessionJsonContext : JsonSerializerContext;

/// <summary>
/// Service for persisting and restoring application session state.
/// </summary>
public sealed class SessionService
{
    private static string SessionFolder => AppConfiguration.SessionFolder;
    private static string SessionIndexFile => Path.Combine(SessionFolder, "session.json");
    private static string UserSettingsFile => Path.Combine(SessionFolder, "settings.json");

    private readonly Dictionary<string, string> _userSettings = new();

    /// <summary>
    /// Represents a persisted tab state.
    /// </summary>
    public sealed class TabState
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? FilePath { get; set; }
        public string? TempFilePath { get; set; }
        public bool IsModified { get; set; }
    }

    /// <summary>
    /// Represents the session state.
    /// </summary>
    public sealed class SessionState
    {
        public List<TabState> Tabs { get; set; } = [];
        public Guid? SelectedTabId { get; set; }
    }

    /// <summary>
    /// Saves the current session state.
    /// </summary>
    /// <param name="tabs">The tabs to save.</param>
    /// <param name="selectedTabId">The ID of the selected tab.</param>
    public async Task SaveSessionAsync(IEnumerable<DocumentTab> tabs, Guid? selectedTabId)
    {
        try
        {
            Directory.CreateDirectory(SessionFolder);

            var sessionState = new SessionState
            {
                SelectedTabId = selectedTabId,
                Tabs = []
            };

            foreach (var tab in tabs)
            {
                var tabState = new TabState
                {
                    Id = tab.Id,
                    Title = tab.Title,
                    FilePath = tab.FilePath,
                    IsModified = tab.IsModified
                };

                // Save content to temp file using streaming to avoid memory copy
                var tempFileName = $"{tab.Id}.txt";
                var tempFilePath = Path.Combine(SessionFolder, tempFileName);
                await using (var fileStream = File.Create(tempFilePath))
                {
                    using var dataWriter = new DataWriter(fileStream.AsOutputStream());
                    dataWriter.WriteBuffer(tab.Content);
                    await dataWriter.StoreAsync();
                    await dataWriter.FlushAsync();
                }
                tabState.TempFilePath = tempFilePath;

                sessionState.Tabs.Add(tabState);
            }

            var json = JsonSerializer.Serialize(sessionState, SessionJsonContext.Default.SessionState);
            await File.WriteAllTextAsync(SessionIndexFile, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save session: {ex.Message}");
        }
    }

    /// <summary>
    /// Restores the session state.
    /// </summary>
    /// <returns>The restored tabs and selected tab ID.</returns>
    public async Task<(List<DocumentTab> Tabs, Guid? SelectedTabId)> RestoreSessionAsync()
    {
        var tabs = new List<DocumentTab>();
        Guid? selectedTabId = null;

        try
        {
            if (!File.Exists(SessionIndexFile))
            {
                return (tabs, selectedTabId);
            }

            var json = await File.ReadAllTextAsync(SessionIndexFile);
            var sessionState = JsonSerializer.Deserialize(json, SessionJsonContext.Default.SessionState);

            if (sessionState is null)
            {
                return (tabs, selectedTabId);
            }

            selectedTabId = sessionState.SelectedTabId;

            foreach (var tabState in sessionState.Tabs)
            {
                IBuffer content = new Windows.Storage.Streams.Buffer(0);
                bool isModified = tabState.IsModified;

                if (!string.IsNullOrEmpty(tabState.TempFilePath) && File.Exists(tabState.TempFilePath))
                {
                    var bytes = await File.ReadAllBytesAsync(tabState.TempFilePath);
                    content = bytes.AsBuffer();
                }

                // Unsaved sessions (no FilePath) should always be marked as modified
                if (string.IsNullOrEmpty(tabState.FilePath))
                {
                    isModified = true;
                }

                var tab = new DocumentTab
                {
                    Id = tabState.Id,
                    Title = tabState.Title,
                    FilePath = tabState.FilePath,
                    Content = content,
                    IsModified = isModified
                };

                tabs.Add(tab);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to restore session: {ex.Message}");
        }

        return (tabs, selectedTabId);
    }

    /// <summary>
    /// Deletes the temporary session file for a closed tab.
    /// </summary>
    /// <param name="tabId">The ID of the tab being closed.</param>
    public void DeleteTabSession(Guid tabId)
    {
        try
        {
            var tempFileName = $"{tabId}.txt";
            var tempFilePath = Path.Combine(SessionFolder, tempFileName);
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to delete tab session: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears the session data.
    /// </summary>
    public void ClearSession()
    {
        try
        {
            if (Directory.Exists(SessionFolder))
            {
                Directory.Delete(SessionFolder, recursive: true);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to clear session: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads user settings from disk.
    /// </summary>
    public async Task LoadUserSettingsAsync()
    {
        try
        {
            if (File.Exists(UserSettingsFile))
            {
                var json = await File.ReadAllTextAsync(UserSettingsFile);
                var settings = JsonSerializer.Deserialize(json, SessionJsonContext.Default.DictionaryStringString);
                if (settings is not null)
                {
                    foreach (var kvp in settings)
                    {
                        _userSettings[kvp.Key] = kvp.Value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load user settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves user settings to disk.
    /// </summary>
    private async Task SaveUserSettingsAsync()
    {
        try
        {
            Directory.CreateDirectory(SessionFolder);
            var json = JsonSerializer.Serialize(_userSettings, SessionJsonContext.Default.DictionaryStringString);
            await File.WriteAllTextAsync(UserSettingsFile, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save user settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets a user setting value.
    /// </summary>
    /// <param name="key">The setting key.</param>
    /// <returns>The setting value, or null if not found.</returns>
    public string? GetUserSetting(string key)
    {
        return _userSettings.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// Sets a user setting value and saves to disk.
    /// </summary>
    /// <param name="key">The setting key.</param>
    /// <param name="value">The setting value.</param>
    public void SetUserSetting(string key, string value)
    {
        _userSettings[key] = value;
        _ = SaveUserSettingsAsync(); // Fire and forget
    }
}
