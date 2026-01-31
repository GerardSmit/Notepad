using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;

namespace Notepad.Tests;

/// <summary>
/// Base class for WinAppDriver-based UI integration tests.
/// Provides helper methods for interacting with the Notepad application.
/// </summary>
public abstract class UITestBase : IDisposable
{
    private static readonly string AppPath = GetAppPath();
    private const string WindowsApplicationDriverUrl = "http://127.0.0.1:4723";
    
    protected Process? AppProcess { get; private set; }
    protected WindowsDriver<WindowsElement>? Driver { get; private set; }

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
        // Could be: Notepad.Tests/bin/Debug/net10.0-windows10.0.26100.0/ (4 levels up)
        // Or: Notepad.Tests/bin/x64/Debug/net10.0-windows10.0.26100.0/ (5 levels up)
        // Try both to handle different build configurations
        var solutionDir4 = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
        var solutionDir5 = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
        
        // Try different build configurations from both possible solution directories
        var possiblePaths = new[]
        {
            // From 5-level path (when platform is specified like x64)
            Path.Combine(solutionDir5, "Notepad", "bin", "x64", "Debug", "net10.0-windows10.0.26100.0", "win-x64", "Notepad.exe"),
            Path.Combine(solutionDir5, "Notepad", "bin", "x64", "Release", "net10.0-windows10.0.26100.0", "win-x64", "Notepad.exe"),
            Path.Combine(solutionDir5, "Notepad", "bin", "Debug", "net10.0-windows10.0.26100.0", "win-x64", "Notepad.exe"),
            Path.Combine(solutionDir5, "Notepad", "bin", "Release", "net10.0-windows10.0.26100.0", "win-x64", "Notepad.exe"),
            
            // From 4-level path (when no platform specified)
            Path.Combine(solutionDir4, "Notepad", "bin", "x64", "Debug", "net10.0-windows10.0.26100.0", "win-x64", "Notepad.exe"),
            Path.Combine(solutionDir4, "Notepad", "bin", "x64", "Release", "net10.0-windows10.0.26100.0", "win-x64", "Notepad.exe"),
            Path.Combine(solutionDir4, "Notepad", "bin", "Debug", "net10.0-windows10.0.26100.0", "win-x64", "Notepad.exe"),
            Path.Combine(solutionDir4, "Notepad", "bin", "Release", "net10.0-windows10.0.26100.0", "win-x64", "Notepad.exe"),
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
        sb.AppendLine($"SolutionDir (4 levels): {solutionDir4}");
        sb.AppendLine($"SolutionDir (5 levels): {solutionDir5}");
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

        // Let WinAppDriver launch the application
        var appDir = Path.GetDirectoryName(AppPath)!;
        
        // Set up WinAppDriver options to launch the app (using v4.x API)
        var options = new AppiumOptions();
        options.AddAdditionalCapability("app", AppPath);
        options.AddAdditionalCapability("platformName", "Windows");
        options.AddAdditionalCapability("appArguments", $"--session-folder \"{_testSessionDir}\"");
        options.AddAdditionalCapability("appWorkingDir", appDir);
        
        // Connect to WinAppDriver - it will launch the app
        Driver = new WindowsDriver<WindowsElement>(new Uri(WindowsApplicationDriverUrl), options);
        Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(1);
        
        // Wait for app to start
        Thread.Sleep(3000);
        
        // Try to find the app process
        var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(AppPath));
        AppProcess = processes.FirstOrDefault();
        Assert.IsNotNull(AppProcess, "Failed to find Notepad process after launch");

        // Increase implicit wait for UI automation
        Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(2);
        
        // Wait for window to be fully ready
        Thread.Sleep(2000);
    }

    /// <summary>
    /// Closes the application after each test.
    /// </summary>
    [TestCleanup]
    public virtual void TestCleanup()
    {
        try
        {
            Driver?.Quit();
        }
        catch
        {
            // Ignore cleanup errors
        }
        
        try
        {
            // Close the app gracefully
            if (AppProcess is not null && !AppProcess.HasExited)
            {
                AppProcess.CloseMainWindow();
                Thread.Sleep(500);
                
                // Force kill if still running
                if (!AppProcess.HasExited)
                {
                    AppProcess.Kill();
                }
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
        
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
    protected void PressShortcut(string modifier, string key)
    {
        var actions = new Actions(Driver);
        actions.KeyDown(modifier).SendKeys(key).KeyUp(modifier).Perform();
        Thread.Sleep(100);
    }
    /// <summary>
    /// Checks if tests are running in a CI environment.
    /// Detects common CI environment variables set by GitHub Actions, Azure DevOps, Jenkins, etc.
    /// GitHub Actions automatically sets GITHUB_ACTIONS=true.
    /// </summary>
    private static bool IsRunningInCi()
    {
        // Check common CI environment variables
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) ||
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")) ||
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_BUILD")) ||
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JENKINS_URL"));
    }

    /// <summary>
    /// Opens a file directly by restarting the app with the file as a command-line argument.
    /// This bypasses the FileOpenPicker dialog which doesn't work in CI environments.
    /// </summary>
    private void OpenFileDirectly(string filePath)
    {
        // Close current session
        try
        {
            Driver?.Quit();
        }
        catch
        {
            // Ignore cleanup errors
        }

        Thread.Sleep(500);

        // Kill any remaining processes
        try
        {
            AppProcess?.Kill();
            AppProcess?.WaitForExit(1000);
        }
        catch
        {
            // Ignore
        }

        Thread.Sleep(500);

        // Launch app with file argument
        var appDir = Path.GetDirectoryName(AppPath)!;
        var options = new AppiumOptions();
        options.AddAdditionalCapability("app", AppPath);
        options.AddAdditionalCapability("platformName", "Windows");
        options.AddAdditionalCapability("appArguments", $"--session-folder \"{_testSessionDir}\" --open \"{filePath}\"");
        options.AddAdditionalCapability("appWorkingDir", appDir);

        Driver = new WindowsDriver<WindowsElement>(new Uri(WindowsApplicationDriverUrl), options);
        Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(2);

        // Wait for app to start and file to load
        Thread.Sleep(3000);

        var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(AppPath));
        AppProcess = processes.FirstOrDefault();
    }
    /// <summary>
    /// Opens a file using the File Open dialog via Ctrl+O.
    /// In CI environments, bypasses the dialog and opens the file directly.
    /// </summary>
    protected void OpenFile(string filePath)
    {
        // In CI environments, use direct file opening to bypass FileOpenPicker
        if (IsRunningInCi())
        {
            OpenFileDirectly(filePath);
            return;
        }

        // Wait for window to be active
        Thread.Sleep(300);
        
        // Press Ctrl+O
        PressShortcut(Keys.Control, "o");
        
        // Wait for file dialog to appear
        Thread.Sleep(1000);
        
        // Find the file dialog
        IWebElement? fileDialog = null;
        
        try
        {
            fileDialog = Driver!.FindElement(By.Name("Open"));
        }
        catch
        {
            // Dialog might have a different name
        }
        
        if (fileDialog is null)
        {
            try
            {
                fileDialog = Driver!.FindElement(By.Name("Open File"));
            }
            catch
            {
                // Try finding by partial name
            }
        }
        
        Assert.IsNotNull(fileDialog, "File dialog should appear");
        
        // Find the filename combo box (AutomationId "1148" for standard Windows dialogs)
        try
        {
            var filenameBox = fileDialog.FindElement(MobileBy.AccessibilityId("1148"));
            filenameBox.Click();
            Thread.Sleep(100);
            
            // Select all and type path
            var actions = new Actions(Driver);
            actions.KeyDown(Keys.Control).SendKeys("a").KeyUp(Keys.Control).Perform();
            Thread.Sleep(50);
            actions.SendKeys(filePath).Perform();
            Thread.Sleep(300);
        }
        catch
        {
            // Fallback: Try Alt+N to focus filename field
            var actions = new Actions(Driver);
            actions.KeyDown(Keys.Alt).SendKeys("n").KeyUp(Keys.Alt).Perform();
            Thread.Sleep(100);
            actions.SendKeys(filePath).Perform();
            Thread.Sleep(300);
        }
        
        // Press Enter to open
        var enterAction = new Actions(Driver);
        enterAction.SendKeys(Keys.Return).Perform();
        
        // Wait for dialog to close and file to load
        Thread.Sleep(1000);
    }

    /// <summary>
    /// Opens the Recent Files panel using Ctrl+R.
    /// </summary>
    protected IWebElement? OpenRecentFilesPanel()
    {
        PressShortcut(Keys.Control, "r");
        Thread.Sleep(300);
        
        // Find the recent files panel
        try
        {
            return Driver!.FindElement(MobileBy.AccessibilityId("RecentFilesPanel"));
        }
        catch
        {
            try
            {
                return Driver!.FindElement(By.Name("Recent Files"));
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Gets the currently selected tab header text.
    /// </summary>
    protected string? GetSelectedTabHeader()
    {
        try
        {
            var tabs = Driver!.FindElements(By.ClassName("TabViewItem"));
            foreach (var tab in tabs)
            {
                if (tab.Selected)
                {
                    return tab.Text;
                }
            }
        }
        catch
        {
            // Fall back to window title
        }
        
        return null;
    }

    /// <summary>
    /// Gets all tab header texts.
    /// </summary>
    protected string[] GetAllTabHeaders()
    {
        try
        {
            var tabs = Driver!.FindElements(By.ClassName("TabViewItem"));
            return tabs.Select(t => t.Text).ToArray();
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Clicks on a tab by its header text.
    /// </summary>
    protected void ClickTab(string headerText)
    {
        try
        {
            var tabs = Driver!.FindElements(By.ClassName("TabViewItem"));
            foreach (var tab in tabs)
            {
                if (tab.Text == headerText || tab.Text.Contains(headerText))
                {
                    tab.Click();
                    Thread.Sleep(200);
                    return;
                }
            }
        }
        catch
        {
            // Could not find tab
        }
        
        Assert.Fail($"Tab '{headerText}' not found");
    }

    /// <summary>
    /// Closes the current tab using Ctrl+W.
    /// </summary>
    protected void CloseCurrentTab()
    {
        PressShortcut(Keys.Control, "w");
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
                Driver?.Quit();
                AppProcess?.Dispose();
            }
            _disposed = true;
        }
    }
}
