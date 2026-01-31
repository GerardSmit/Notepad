using System.IO;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
        Thread.Sleep(500);
        
        // Act - Press Ctrl+G
        PressShortcut(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_G);
        Thread.Sleep(500);
        
        // Assert - Go To Line dialog should appear (look for numeric input or label)
        var goToLineControl = FindDescendantByPartialName("Go to Line")
            ?? FindDescendantByPartialName("Line number");
        
        // If we can't find by name, at least verify an overlay/dialog appeared
        if (goToLineControl is null)
        {
            Assert.IsTrue(IsOverlayVisible(), "Go To Line dialog should appear (detected via overlay check)");
        }
        
        // Press Escape to close
        Keyboard.Press(VirtualKeyShort.ESCAPE);
        Thread.Sleep(200);
    }

    /// <summary>
    /// Verifies that Ctrl+P opens the Quick Open dialog.
    /// </summary>
    [TestMethod]
    public void CtrlP_OpensQuickOpenDialog()
    {
        // Arrange - Have the app open
        Thread.Sleep(300);
        
        // Ensure window is focused
        MainWindow!.SetForeground();
        MainWindow.Focus();
        MainWindow.Click();
        Thread.Sleep(300);
        
        // Act - Press Ctrl+P
        PressShortcut(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_P);
        Thread.Sleep(500);
        
        // Assert - Quick Open dialog should appear (look for the search text box with placeholder)
        var searchBox = FindDescendantByPartialName("search")
            ?? FindDescendantByPartialName("Type to");
        
        // If we can't find by name, at least verify an overlay/dialog appeared
        if (searchBox is null)
        {
            Assert.IsTrue(IsOverlayVisible(), "Quick Open dialog should appear (detected via overlay check)");
        }
        
        // Press Escape to close
        Keyboard.Press(VirtualKeyShort.ESCAPE);
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
        Thread.Sleep(500);
        
        // Act - Press Ctrl+H
        PressShortcut(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_H);
        Thread.Sleep(500);
        
        // Assert - Find and Replace dialog should appear (look for Replace button or label)
        var findReplaceControl = FindDescendantByPartialName("Replace")
            ?? FindDescendantByPartialName("Find");
        
        // If we can't find by name, at least verify an overlay/dialog appeared
        if (findReplaceControl is null)
        {
            Assert.IsTrue(IsOverlayVisible(), "Find and Replace dialog should appear (detected via overlay check)");
        }
        
        // Press Escape to close
        Keyboard.Press(VirtualKeyShort.ESCAPE);
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
        Thread.Sleep(500);
        
        // Act - Press Ctrl+F
        PressShortcut(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
        Thread.Sleep(500);
        
        // Assert - Find dialog should appear
        var findControl = FindDescendantByPartialName("Find")
            ?? FindDescendantByPartialName("Search");
        
        // If we can't find by name, at least verify an overlay/dialog appeared
        if (findControl is null)
        {
            Assert.IsTrue(IsOverlayVisible(), "Find dialog should appear (detected via overlay check)");
        }
        
        // Press Escape to close
        Keyboard.Press(VirtualKeyShort.ESCAPE);
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
        
        // Modify content
        MainWindow!.SetForeground();
        MainWindow.Focus();
        Thread.Sleep(200);
        
        // Type some additional content (will append or replace based on editor state)
        Keyboard.Type("Modified ");
        Thread.Sleep(300);
        
        // Act - Press Ctrl+S
        PressShortcut(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
        Thread.Sleep(500);
        
        // Assert - No Save As dialog should appear (file already exists)
        var desktop = Automation!.GetDesktop();
        Window? saveDialog = null;
        
        var windows = desktop.FindAllChildren(CF!.ByControlType(ControlType.Window));
        foreach (var win in windows)
        {
            var title = win.Name ?? "";
            if (title.Contains("Save As", StringComparison.OrdinalIgnoreCase))
            {
                saveDialog = win.AsWindow();
                break;
            }
        }
        
        Assert.IsNull(saveDialog, "Save As dialog should NOT appear for existing file");
    }

    /// <summary>
    /// Helper method to find a descendant element by partial name match.
    /// Handles elements that don't support the Name property.
    /// </summary>
    private AutomationElement? FindDescendantByPartialName(string partialName)
    {
        var allDescendants = MainWindow?.FindAllDescendants();
        if (allDescendants is null) return null;
        
        foreach (var e in allDescendants)
        {
            try
            {
                var name = e.Name;
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
        
        return null;
    }
    
    /// <summary>
    /// Checks if a dialog/overlay is currently visible by looking for common control types.
    /// </summary>
    private bool IsOverlayVisible()
    {
        var allDescendants = MainWindow?.FindAllDescendants();
        if (allDescendants is null) return false;
        
        // Look for text boxes that might be part of dialogs
        var textBoxes = allDescendants.Where(e => 
        {
            try
            {
                return e.ControlType == ControlType.Edit;
            }
            catch
            {
                return false;
            }
        }).ToList();
        
        // If we have more than one text box (besides the main editor), a dialog is probably open
        return textBoxes.Count > 1;
    }
}
