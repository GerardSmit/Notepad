using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;

namespace Notepad.Tests;

/// <summary>
/// UI tests for keyboard shortcuts and plugin features.
/// </summary>
[TestClass]
public sealed class KeyboardShortcutsUITests : UITestBase
{
    /// <summary>
    /// Verifies that Ctrl+G opens the Go To Line dialog.
    /// </summary>
    [TestMethod]
    public void CtrlG_OpensGoToLineDialog()
    {
        // Arrange - Create and open a file with multiple lines
        var testFile = CreateTestFile("multiline.txt", "Line 1\nLine 2\nLine 3\nLine 4\nLine 5");
        OpenFile(testFile);
        Thread.Sleep(200);
        
        // Act - Press Ctrl+G
        PressShortcut(Keys.Control, "g");
        Thread.Sleep(200);
        
        // Assert - Go To Line dialog should appear
        var goToLineControl = FindDescendantByPartialName("Go to Line")
            ?? FindDescendantByPartialName("Line number");
        
        // If we can't find by name, at least verify an overlay/dialog appeared
        if (goToLineControl is null)
        {
            Assert.IsTrue(IsOverlayVisible(), "Go To Line dialog should appear (detected via overlay check)");
        }
        
        // Press Escape to close
        var actions = new Actions(Driver);
        actions.SendKeys(Keys.Escape).Perform();
        Thread.Sleep(200);
    }

    /// <summary>
    /// Verifies that Ctrl+P opens the Quick Open dialog.
    /// </summary>
    [TestMethod]
    public void CtrlP_OpensQuickOpenDialog()
    {
        // Arrange - Have the app open
        Thread.Sleep(100);
        
        // Act - Press Ctrl+P
        PressShortcut(Keys.Control, "p");
        Thread.Sleep(200);
        
        // Assert - Quick Open dialog should appear
        var searchBox = FindDescendantByPartialName("search")
            ?? FindDescendantByPartialName("Type to");
        
        // If we can't find by name, at least verify an overlay/dialog appeared
        if (searchBox is null)
        {
            Assert.IsTrue(IsOverlayVisible(), "Quick Open dialog should appear (detected via overlay check)");
        }
        
        // Press Escape to close
        var actions = new Actions(Driver);
        actions.SendKeys(Keys.Escape).Perform();
        Thread.Sleep(200);
    }

    /// <summary>
    /// Verifies that Ctrl+H opens the Find and Replace dialog.
    /// </summary>
    [TestMethod]
    public void CtrlH_OpensFindReplaceDialog()
    {
        // Arrange - Create and open a file
        var testFile = CreateTestFile("findreplace.txt", "Hello World");
        OpenFile(testFile);
        Thread.Sleep(200);
        
        // Act - Press Ctrl+H
        PressShortcut(Keys.Control, "h");
        Thread.Sleep(200);
        
        // Assert - Find and Replace dialog should appear
        var findReplaceControl = FindDescendantByPartialName("Replace")
            ?? FindDescendantByPartialName("Find");
        
        // If we can't find by name, at least verify an overlay/dialog appeared
        if (findReplaceControl is null)
        {
            Assert.IsTrue(IsOverlayVisible(), "Find and Replace dialog should appear (detected via overlay check)");
        }
        
        // Press Escape to close
        var actions = new Actions(Driver);
        actions.SendKeys(Keys.Escape).Perform();
        Thread.Sleep(200);
    }

    /// <summary>
    /// Verifies that Ctrl+F opens the Find dialog.
    /// </summary>
    [TestMethod]
    public void CtrlF_OpensFindDialog()
    {
        // Arrange - Create and open a file
        var testFile = CreateTestFile("find.txt", "Hello World");
        OpenFile(testFile);
        Thread.Sleep(200);
        
        // Act - Press Ctrl+F
        PressShortcut(Keys.Control, "f");
        Thread.Sleep(200);
        
        // Assert - Find dialog should appear
        var findControl = FindDescendantByPartialName("Find")
            ?? FindDescendantByPartialName("Search");
        
        // If we can't find by name, at least verify an overlay/dialog appeared
        if (findControl is null)
        {
            Assert.IsTrue(IsOverlayVisible(), "Find dialog should appear (detected via overlay check)");
        }
        
        // Press Escape to close
        var actions = new Actions(Driver);
        actions.SendKeys(Keys.Escape).Perform();
        Thread.Sleep(200);
    }

    /// <summary>
    /// Verifies that opening an existing file and pressing Ctrl+S saves without dialog.
    /// </summary>
    [TestMethod]
    public void CtrlS_OnExistingFile_SavesWithoutDialog()
    {
        // Arrange - Create and open a file
        var testFile = CreateTestFile("save-test.txt", "Original content");
        OpenFile(testFile);
        Thread.Sleep(500);
        
        // Wait for file to be fully loaded
        Thread.Sleep(1000);
        
        // Type some additional content by sending keys to the window
        var actions = new Actions(Driver);
        actions.SendKeys("Modified ").Perform();
        Thread.Sleep(200);
        
        // Act - Press Ctrl+S
        PressShortcut(Keys.Control, "s");
        Thread.Sleep(500);
        
        // Assert - No Save As dialog should appear (file already exists)
        // Set a short implicit wait since we expect the element to NOT exist
        Driver!.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(500);
        try
        {
            var saveAsDialog = Driver.FindElements(By.Name("Save As"));
            if (saveAsDialog.Count > 0)
            {
                Assert.Fail("Save As dialog should NOT appear for existing file");
            }
            // No dialog found - this is the expected behavior
        }
        finally
        {
            // Restore normal implicit wait
            Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(2);
        }
    }

    /// <summary>
    /// Helper method to find a descendant element by partial name match.
    /// </summary>
    private IWebElement? FindDescendantByPartialName(string partialName)
    {
        try
        {
            var allElements = Driver!.FindElements(By.XPath("//*"));
            foreach (var e in allElements)
            {
                try
                {
                    var name = e.GetAttribute("Name");
                    if (name?.Contains(partialName, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        return e;
                    }
                }
                catch
                {
                    // Some elements don't support the Name property, skip them
                }
            }
        }
        catch
        {
            // Could not enumerate elements
        }
        
        return null;
    }
    
    /// <summary>
    /// Checks if a dialog/overlay is currently visible by looking for common control types.
    /// </summary>
    private bool IsOverlayVisible()
    {
        try
        {
            var textBoxes = Driver!.FindElements(By.XPath("//Edit"));
            // If we have more than one text box (besides the main editor), a dialog is probably open
            return textBoxes.Count > 1;
        }
        catch
        {
            return false;
        }
    }
}
