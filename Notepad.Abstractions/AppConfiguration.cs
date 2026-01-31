namespace Notepad.Abstractions;

/// <summary>
/// Global application configuration that can be set at startup.
/// </summary>
public static class AppConfiguration
{
    private static readonly string DefaultSessionFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Notepad",
        "Session");

    /// <summary>
    /// Gets or sets the session folder path.
    /// This is where session data, recent files, and settings are stored.
    /// </summary>
    public static string SessionFolder { get; set; } = DefaultSessionFolder;

    /// <summary>
    /// Gets or sets the list of files to open on startup.
    /// </summary>
    public static List<string> FilesToOpen { get; } = [];

    /// <summary>
    /// Initializes configuration from command-line arguments.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    public static void InitializeFromCommandLine(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--session-folder" && i + 1 < args.Length && !string.IsNullOrWhiteSpace(args[i + 1]))
            {
                SessionFolder = args[i + 1];
                i++; // Skip next argument
            }
            else if (args[i] == "--open" && i + 1 < args.Length && !string.IsNullOrWhiteSpace(args[i + 1]))
            {
                FilesToOpen.Add(args[i + 1]);
                i++; // Skip next argument
            }
            else if (!args[i].StartsWith('-') && File.Exists(args[i]))
            {
                // Support passing file paths directly (skip the exe itself at index 0)
                if (i > 0)
                {
                    FilesToOpen.Add(args[i]);
                }
            }
        }
    }
}
