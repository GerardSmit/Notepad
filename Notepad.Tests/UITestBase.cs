using System.Diagnostics;
using System.IO;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Notepad.Tests;

/// <summary>
/// Base class for FlaUI-based UI integration tests.
/// Provides helper methods for interacting with the Notepad application.
/// </summary>
public abstract class UITestBase : IDisposable
{
    private static readonly string AppPath = GetAppPath();
    
    protected Application? App { get; private set; }
    protected UIA3Automation? Automation { get; private set; }
    protected Window? MainWindow { get; private set; }
    protected ConditionFactory? CF { get; private set; }

    private string _testFilesDir = null!;
    private string _testSessionDir = null!;
    private bool _disposed;
    
    /// <summary>
    /// Gets the path to the recent files JSON in the test session folder.
    /// </summary>
    protected string RecentFilesJsonPath => Path.Combine(_testSessionDir, "recent-files.json");

    /// <summary>
    /// Gets the path to the Notepad executable.
    /// </summary>
    private static string GetAppPath()
    {
        // Look for the built executable
        var baseDir = AppContext.BaseDirectory;
        
        // Navigate up from test bin to solution root
        // Test runs from: Notepad.Tests/bin/Debug/net9.0-windows10.0.26100.0/
        var solutionDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
        
        // Try different build configurations
        // Prefer non-x64 build directories first as they are more reliable
        var possiblePaths = new[]
        {
            Path.Combine(solutionDir, "Notepad", "bin", "Debug", "net10.0-windows10.0.26100.0", "win-x64", "Notepad.exe"),
            Path.Combine(solutionDir, "Notepad", "bin", "Release", "net10.0-windows10.0.26100.0", "win-x64", "Notepad.exe"),
            Path.Combine(solutionDir, "Notepad", "bin", "x64", "Debug", "net10.0-windows10.0.26100.0", "win-x64", "Notepad.exe"),
            Path.Combine(solutionDir, "Notepad", "bin", "x64", "Release", "net10.0-windows10.0.26100.0", "win-x64", "Notepad.exe"),
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        // Create detailed error message
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Could not find Notepad.exe. Build the main project first.");
        sb.AppendLine($"BaseDirectory: {baseDir}");
        sb.AppendLine($"SolutionDir: {solutionDir}");
        sb.AppendLine("Searched in:");
        foreach (var path in possiblePaths)
        {
            sb.AppendLine($"  {path} (exists: {File.Exists(path)})");
        }
        
        throw new FileNotFoundException(sb.ToString());
    }

    /// <summary>
    /// Launches the Notepad application before each test.
    /// </summary>
    [TestInitialize]
    public virtual void TestInitialize()
    {
        // Create test files directory
        _testFilesDir = Path.Combine(Path.GetTempPath(), "NotepadUITests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testFilesDir);
        
        // Create isolated session folder for this test run
        _testSessionDir = Path.Combine(Path.GetTempPath(), "NotepadUITestSession_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testSessionDir);

        // Launch the application using Process.Start and then attach
        // WinUI 3 apps can have complex process models
        Automation = new UIA3Automation();
        CF = new ConditionFactory(Automation.PropertyLibrary);
        
        // Set working directory to the app's directory (required for WinUI apps)
        var appDir = Path.GetDirectoryName(AppPath)!;
        
        // Launch with isolated session folder to avoid interference from previous sessions
        var processInfo = new ProcessStartInfo
        {
            FileName = AppPath,
            Arguments = $"--session-folder \"{_testSessionDir}\"",
            WorkingDirectory = appDir,
            UseShellExecute = true  // Use shell execute for proper WinUI launching
        };
        
        Process.Start(processInfo);
        
        // Wait for the process to initialize and have a main window
        // Retry loop because WinUI apps take time to start
        Process? targetProcess = null;
        for (int i = 0; i < 30; i++)  // Up to 15 seconds
        {
            Thread.Sleep(500);
            var notepadProcesses = Process.GetProcessesByName("Notepad");
            foreach (var proc in notepadProcesses)
            {
                try
                {
                    proc.Refresh();
                    if (!proc.HasExited && proc.MainWindowHandle != IntPtr.Zero)
                    {
                        targetProcess = proc;
                        break;
                    }
                }
                catch
                {
                    // Process may have exited or access denied, ignore
                }
            }
            if (targetProcess is not null)
                break;
        }
        
        Assert.IsNotNull(targetProcess, "No Notepad process with main window found after launch.");
        App = Application.Attach(targetProcess);
        
        // Wait for main window
        MainWindow = App.GetMainWindow(Automation, TimeSpan.FromSeconds(10));
        Assert.IsNotNull(MainWindow, "Main window should be found");
        
        // Wait for the app to be ready
        Wait.UntilResponsive(MainWindow, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Closes the application after each test.
    /// </summary>
    [TestCleanup]
    public virtual void TestCleanup()
    {
        try
        {
            // Close the app gracefully
            App?.Close();
            
            // Wait a bit for cleanup
            Thread.Sleep(500);
            
            // Force kill if still running
            if (App?.HasExited == false)
            {
                App.Kill();
            }
        }
        catch
        {
            // Ignore cleanup errors
        }

        Automation?.Dispose();
        
        // Clean up test files
        try
        {
            if (Directory.Exists(_testFilesDir))
            {
                Directory.Delete(_testFilesDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
        
        // Clean up test session folder
        try
        {
            if (Directory.Exists(_testSessionDir))
            {
                Directory.Delete(_testSessionDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    /// <summary>
    /// Creates a test file with the given content.
    /// </summary>
    protected string CreateTestFile(string name, string content = "Test content")
    {
        var path = Path.Combine(_testFilesDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>
    /// Presses a keyboard shortcut.
    /// </summary>
    protected static void PressShortcut(VirtualKeyShort modifier, VirtualKeyShort key)
    {
        Keyboard.Press(modifier);
        Keyboard.Press(key);
        Keyboard.Release(key);
        Keyboard.Release(modifier);
        Thread.Sleep(100);
    }

    /// <summary>
    /// Opens a file using the File Open dialog via Ctrl+O.
    /// </summary>
    protected void OpenFile(string filePath)
    {
        // Ensure main window has focus - use SetForeground to bring to front
        MainWindow!.SetForeground();
        Thread.Sleep(300);
        MainWindow.Focus();
        Thread.Sleep(300);
        
        // Click in the center of the window to ensure it has keyboard focus
        MainWindow.Click();
        Thread.Sleep(200);
        
        // Press Ctrl+O
        PressShortcut(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_O);
        
        // Wait for file dialog to appear
        Thread.Sleep(1000);
        
        // Find the file dialog on the desktop (WinUI file pickers are top-level windows)
        var desktop = Automation!.GetDesktop();
        
        // Try multiple ways to find the file dialog
        Window? fileDialog = null;
        
        // Look for common Open dialog names
        var dialogNames = new[] { "Open", "Open File", "Select file" };
        foreach (var name in dialogNames)
        {
            var found = desktop.FindFirstDescendant(CF!.ByName(name))?.AsWindow();
            if (found is not null)
            {
                fileDialog = found;
                break;
            }
        }
        
        // If still not found, try finding by ClassName
        if (fileDialog is null)
        {
            var windows = desktop.FindAllChildren(CF!.ByControlType(FlaUI.Core.Definitions.ControlType.Window));
            foreach (var win in windows)
            {
                var title = win.Name ?? "";
                if (title.Contains("Open", StringComparison.OrdinalIgnoreCase) || 
                    title.Contains("Select", StringComparison.OrdinalIgnoreCase) ||
                    title.Contains("file", StringComparison.OrdinalIgnoreCase))
                {
                    fileDialog = win.AsWindow();
                    break;
                }
            }
        }
        
        Assert.IsNotNull(fileDialog, $"File dialog should appear. Searched for: {string.Join(", ", dialogNames)}");
        
        // Focus the dialog
        fileDialog.Focus();
        Thread.Sleep(300);
        
        // For Windows File dialogs, the filename combo box has AutomationId "1148"
        // We need to find it and enter the file path
        var filenameCombo = fileDialog.FindFirstDescendant(CF.ByAutomationId("1148"));
        
        if (filenameCombo is not null)
        {
            // Click on the combo box to focus it
            filenameCombo.Click();
            Thread.Sleep(100);
            
            // Select all existing text and replace with our path
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            Thread.Sleep(50);
            
            // Type the file path
            Keyboard.Type(filePath);
            Thread.Sleep(300);
        }
        else
        {
            // Fallback: Try Alt+N to focus filename field, then type
            Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.KEY_N);
            Thread.Sleep(100);
            Keyboard.Type(filePath);
            Thread.Sleep(300);
        }
        
        // Press Enter to open the file
        Keyboard.Press(VirtualKeyShort.ENTER);
        Keyboard.Release(VirtualKeyShort.ENTER);
        
        // Wait for dialog to close and file to load
        Thread.Sleep(1000);
    }

    /// <summary>
    /// Opens the Recent Files panel using Ctrl+R.
    /// </summary>
    protected AutomationElement? OpenRecentFilesPanel()
    {
        PressShortcut(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_R);
        Thread.Sleep(300);
        
        // Find the recent files panel
        var panel = MainWindow!.FindFirstDescendant(CF!.ByAutomationId("RecentFilesPanel"))
            ?? MainWindow.FindFirstDescendant(CF.ByName("Recent Files"));
        
        return panel;
    }

    /// <summary>
    /// Gets the currently selected tab header text.
    /// </summary>
    protected string? GetSelectedTabHeader()
    {
        var tabView = MainWindow!.FindFirstDescendant(CF!.ByControlType(FlaUI.Core.Definitions.ControlType.Tab));
        if (tabView is null) return null;
        
        var selectedTab = tabView.FindFirstChild(CF.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem));
        return selectedTab?.Name;
    }

    /// <summary>
    /// Gets all tab header texts.
    /// </summary>
    protected string[] GetAllTabHeaders()
    {
        var tabView = MainWindow!.FindFirstDescendant(CF!.ByControlType(FlaUI.Core.Definitions.ControlType.Tab));
        if (tabView is null) return [];
        
        var tabs = tabView.FindAllChildren(CF.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem));
        return tabs.Select(t => t.Name).ToArray();
    }

    /// <summary>
    /// Clicks on a tab by its header text.
    /// </summary>
    protected void ClickTab(string headerText)
    {
        var tabView = MainWindow!.FindFirstDescendant(CF!.ByControlType(FlaUI.Core.Definitions.ControlType.Tab));
        Assert.IsNotNull(tabView, "TabView should exist");
        
        var tab = tabView.FindFirstChild(CF.ByName(headerText));
        Assert.IsNotNull(tab, $"Tab '{headerText}' should exist");
        
        tab.Click();
        Thread.Sleep(200);
    }

    /// <summary>
    /// Closes the current tab using Ctrl+W.
    /// </summary>
    protected void CloseCurrentTab()
    {
        PressShortcut(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_W);
        Thread.Sleep(300);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                App?.Dispose();
                Automation?.Dispose();
            }
            _disposed = true;
        }
    }
}
