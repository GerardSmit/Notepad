using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Notepad.Tests;

/// <summary>
/// Represents a recently opened file entry (mirrors RecentFileEntry in DefaultPlugins).
/// </summary>
file sealed class RecentFileEntry
{
    public string FilePath { get; set; } = string.Empty;
    public DateTime LastOpened { get; set; }
}

/// <summary>
/// Represents the persisted state for recent files (mirrors RecentFilesState in DefaultPlugins).
/// </summary>
file sealed class RecentFilesState
{
    public List<RecentFileEntry> Entries { get; set; } = [];
}

/// <summary>
/// UI tests for the Recent Files feature.
/// </summary>
[TestClass]
public sealed class RecentFilesUITests : UITestBase
{
    /// <summary>
    /// Verifies that opening a file via File > Open adds it to recent files history.
    /// </summary>
    [TestMethod]
    public void OpenFile_AddsToRecentFiles()
    {
        // Arrange
        var testFile = CreateTestFile("test-recent.txt", "Hello, World!");
        
        // Ensure recent files is empty at start (fresh test session)
        Assert.IsFalse(File.Exists(RecentFilesJsonPath), "Recent files should not exist at start of test");
        
        // Act - Open the file via Ctrl+O
        OpenFile(testFile);
        
        // Wait for file to be processed
        Thread.Sleep(200);
        
        // Assert - Check that the file was added to recent files
        Assert.IsTrue(File.Exists(RecentFilesJsonPath), "Recent files JSON should be created");
        
        var json = File.ReadAllText(RecentFilesJsonPath);
        var state = JsonSerializer.Deserialize<RecentFilesState>(json);
        
        Assert.IsNotNull(state, "Recent files state should not be null");
        Assert.IsTrue(state.Entries.Count > 0, "Recent files should have entries");
        Assert.IsTrue(state.Entries.Exists(e => e.FilePath == testFile), $"Recent files should contain '{testFile}'");
    }

    /// <summary>
    /// Verifies that opening multiple files adds all of them to recent files history.
    /// </summary>
    [TestMethod]
    public void OpenMultipleFiles_AllAddedToRecentFiles()
    {
        // Arrange
        var testFile1 = CreateTestFile("file1.txt", "Content 1");
        var testFile2 = CreateTestFile("file2.txt", "Content 2");
        var testFile3 = CreateTestFile("file3.txt", "Content 3");
        
        // Act - Open all files
        OpenFile(testFile1);
        Thread.Sleep(100);
        OpenFile(testFile2);
        Thread.Sleep(100);
        OpenFile(testFile3);
        Thread.Sleep(200);
        
        // Assert - All files should be in recent files
        Assert.IsTrue(File.Exists(RecentFilesJsonPath), "Recent files JSON should be created");
        
        var json = File.ReadAllText(RecentFilesJsonPath);
        var state = JsonSerializer.Deserialize<RecentFilesState>(json);
        
        Assert.IsNotNull(state, "Recent files state should not be null");
        Assert.IsTrue(state.Entries.Exists(e => e.FilePath == testFile1), $"Recent files should contain '{testFile1}'");
        Assert.IsTrue(state.Entries.Exists(e => e.FilePath == testFile2), $"Recent files should contain '{testFile2}'");
        Assert.IsTrue(state.Entries.Exists(e => e.FilePath == testFile3), $"Recent files should contain '{testFile3}'");
    }

    /// <summary>
    /// Verifies that opening a file selects its tab (makes it the active tab).
    /// </summary>
    [TestMethod]
    public void OpenFile_SelectsNewTab()
    {
        // Arrange
        var testFile1 = CreateTestFile("first.txt", "First file content");
        var testFile2 = CreateTestFile("second.txt", "Second file content");
        
        // Act - Open first file
        OpenFile(testFile1);
        Thread.Sleep(100);
        
        // Open second file
        OpenFile(testFile2);
        Thread.Sleep(200);
        
        // Assert - The second file's tab should be selected
        // We verify this by checking that the window title contains the second file name
        var windowTitle = Driver!.Title;
        Assert.IsTrue(windowTitle.Contains("second.txt"), 
            $"Window title should contain 'second.txt' but was '{windowTitle}'");
    }

    /// <summary>
    /// Verifies that opening the same file twice only opens it once (deduplication).
    /// The second open should just switch to the existing tab.
    /// </summary>
    [TestMethod]
    public void OpenFileTwice_OnlyOpensOnce()
    {
        // Arrange
        var testFile = CreateTestFile("duplicate-test.txt", "This file should only open once");
        
        // Act - Open the same file twice
        OpenFile(testFile);
        Thread.Sleep(100);
        
        // Verify it's selected
        var titleAfterFirst = Driver!.Title;
        Assert.IsTrue(titleAfterFirst.Contains("duplicate-test.txt"), 
            $"File should be opened, but title was '{titleAfterFirst}'");
        
        // Open again
        OpenFile(testFile);
        Thread.Sleep(200);
        
        // Assert - Should still be on the same file (no new tab created)
        // The window title should still show the same file
        var titleAfterSecond = Driver!.Title;
        Assert.IsTrue(titleAfterSecond.Contains("duplicate-test.txt"), 
            $"Should still show duplicate-test.txt, but title was '{titleAfterSecond}'");
        
        // Also verify via recent files that it only appears once
        Assert.IsTrue(File.Exists(RecentFilesJsonPath), "Recent files JSON should exist");
        var json = File.ReadAllText(RecentFilesJsonPath);
        var state = JsonSerializer.Deserialize<RecentFilesState>(json);
        
        Assert.IsNotNull(state, "Recent files state should not be null");
        var matchingEntries = state.Entries.Count(e => e.FilePath == testFile);
        Assert.AreEqual(1, matchingEntries, 
            $"Should have exactly 1 entry for the file in recent files, but found {matchingEntries}");
    }

    /// <summary>
    /// Verifies that switching to a tab updates it to the top of recent files history.
    /// </summary>
    [TestMethod]
    public void SwitchingTabs_UpdatesRecentFilesOrder()
    {
        // Arrange
        var testFile1 = CreateTestFile("order1.txt", "First file");
        var testFile2 = CreateTestFile("order2.txt", "Second file");
        
        // Open both files (file2 will be most recent after this)
        OpenFile(testFile1);
        Thread.Sleep(100);
        OpenFile(testFile2);
        Thread.Sleep(200);
        
        // Verify order2.txt is currently selected
        var currentTitle = Driver!.Title;
        Assert.IsTrue(currentTitle.Contains("order2.txt"), 
            $"order2.txt should be selected, but title was '{currentTitle}'");
        
        // Act - Switch back to the first file by opening it again (which switches to existing tab)
        OpenFile(testFile1);
        Thread.Sleep(200);
        
        // Verify order1.txt is now selected
        currentTitle = Driver!.Title;
        Assert.IsTrue(currentTitle.Contains("order1.txt"), 
            $"order1.txt should be selected after switch, but title was '{currentTitle}'");
        
        // Assert - The first file should now be at the top of recent files
        Assert.IsTrue(File.Exists(RecentFilesJsonPath), "Recent files JSON should exist");
        
        var json = File.ReadAllText(RecentFilesJsonPath);
        var state = JsonSerializer.Deserialize<RecentFilesState>(json);
        
        Assert.IsNotNull(state, "Recent files state should not be null");
        Assert.IsTrue(state.Entries.Count >= 2, "Should have at least 2 recent file entries");
        
        // The first entry (most recent) should be the file we just switched to
        var mostRecent = state.Entries[0];
        Assert.AreEqual(testFile1, mostRecent.FilePath, 
            $"Most recent file should be '{testFile1}' but was '{mostRecent.FilePath}'");
    }
}
