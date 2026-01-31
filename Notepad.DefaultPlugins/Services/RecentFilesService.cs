using System.Text.Json;
using System.Text.Json.Serialization;
using Notepad.Abstractions;

namespace Notepad.DefaultPlugins.Services;

/// <summary>
/// JSON serializer context for AOT-compatible serialization of recent files.
/// </summary>
[JsonSerializable(typeof(RecentFilesState))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal sealed partial class RecentFilesJsonContext : JsonSerializerContext;

/// <summary>
/// Represents a recently opened file entry.
/// </summary>
public sealed class RecentFileEntry
{
    /// <summary>
    /// Gets or sets the file path.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the file was last opened.
    /// </summary>
    public DateTime LastOpened { get; set; }
}

/// <summary>
/// Represents the persisted state for recent files.
/// </summary>
public sealed class RecentFilesState
{
    /// <summary>
    /// Gets or sets the list of recent file entries.
    /// </summary>
    public List<RecentFileEntry> Entries { get; set; } = [];
}

/// <summary>
/// Service for tracking and persisting recently opened files.
/// </summary>
public sealed class RecentFilesService
{
    private const int MaxRecentFiles = 50;

    private static string DataFolder => AppConfiguration.SessionFolder;
    private static string RecentFilesPath => Path.Combine(DataFolder, "recent-files.json");

    private readonly List<RecentFileEntry> _entries = [];
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private bool _isLoaded;

    /// <summary>
    /// Gets the recent file entries, ordered by most recently opened first.
    /// </summary>
    public IReadOnlyList<RecentFileEntry> Entries => _entries;

    /// <summary>
    /// Ensures the recent files are loaded from disk.
    /// </summary>
    public async Task EnsureLoadedAsync()
    {
        if (_isLoaded)
        {
            return;
        }

        await _loadLock.WaitAsync();
        try
        {
            if (_isLoaded)
            {
                return;
            }

            await LoadCoreAsync();
            _isLoaded = true;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    /// <summary>
    /// Loads the recent files from disk.
    /// </summary>
    [Obsolete("Use EnsureLoadedAsync instead")]
    public async Task LoadAsync()
    {
        await EnsureLoadedAsync();
    }

    private async Task LoadCoreAsync()
    {
        try
        {
            if (File.Exists(RecentFilesPath))
            {
                var json = await File.ReadAllTextAsync(RecentFilesPath);
                var state = JsonSerializer.Deserialize(json, RecentFilesJsonContext.Default.RecentFilesState);
                if (state?.Entries is not null)
                {
                    _entries.Clear();
                    _entries.AddRange(state.Entries.OrderByDescending(e => e.LastOpened));
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load recent files: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves the recent files to disk.
    /// </summary>
    private async Task SaveAsync()
    {
        try
        {
            Directory.CreateDirectory(DataFolder);

            var state = new RecentFilesState { Entries = [.. _entries] };
            var json = JsonSerializer.Serialize(state, RecentFilesJsonContext.Default.RecentFilesState);
            await File.WriteAllTextAsync(RecentFilesPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save recent files: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds or updates a file in the recent files list.
    /// </summary>
    /// <param name="filePath">The file path to add or update.</param>
    public async Task AddFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        // Ensure the service is loaded before adding
        await EnsureLoadedAsync();

        // Normalize the path
        var normalizedPath = Path.GetFullPath(filePath);

        // Remove existing entry if present
        var existingIndex = _entries.FindIndex(e =>
            string.Equals(e.FilePath, normalizedPath, StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
        {
            _entries.RemoveAt(existingIndex);
        }

        // Add new entry at the beginning
        _entries.Insert(0, new RecentFileEntry
        {
            FilePath = normalizedPath,
            LastOpened = DateTime.UtcNow
        });

        // Trim to max size
        while (_entries.Count > MaxRecentFiles)
        {
            _entries.RemoveAt(_entries.Count - 1);
        }

        // Fire and forget save
        _ = SaveAsync();
    }

    /// <summary>
    /// Removes a file from the recent files list (e.g., if it no longer exists).
    /// </summary>
    /// <param name="filePath">The file path to remove.</param>
    public void RemoveFile(string filePath)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        var index = _entries.FindIndex(e =>
            string.Equals(e.FilePath, normalizedPath, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
        {
            _entries.RemoveAt(index);
            _ = SaveAsync();
        }
    }

    /// <summary>
    /// Clears all recent files.
    /// </summary>
    public void Clear()
    {
        _entries.Clear();
        _ = SaveAsync();
    }
}
