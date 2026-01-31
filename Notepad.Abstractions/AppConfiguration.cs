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
    /// Initializes configuration from command-line arguments.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    public static void InitializeFromCommandLine(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--session-folder" && !string.IsNullOrWhiteSpace(args[i + 1]))
            {
                SessionFolder = args[i + 1];
                break;
            }
        }
    }
}
