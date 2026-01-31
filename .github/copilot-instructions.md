# GitHub Copilot Instructions for Notepad Project

## Project Overview
Modern WinUI 3 Notepad with **plugin architecture** and **NativeAOT compilation**. Three projects:
- `Notepad/` - Main WinUI app
- `Notepad.Abstractions/` - Plugin interfaces & models
- `Notepad.DefaultPlugins/` - Built-in plugins (Find/Replace, Go To Line, Quick Open, Tab Preview, Recent Files, About)

**Target**: .NET 10, Windows 10.0.26100.0, PublishAOT enabled

## Critical Architecture Patterns

### Service Initialization Flow (Multi-Stage)
Services have **delayed initialization** because WinUI requires MainWindow before full setup:

1. **App.xaml.cs** `ConfigureServices()`:
   - Register core services as singletons
   - Register plugins via `AddDefaultPlugins()`
   - Call `plugin.ConfigureServices(services)` on each plugin
   - Build service provider → stored in `App.Services`

2. **MainWindow constructor**:
   - Get services from `App.Services`
   - Configure partial service state (e.g., `EditorService.SetDispatcherQueue()`)

3. **MainWindow.OnContentLoaded**:
   - Create `MainViewModel` with FileService/DialogService
   - **Configure DocumentService** with ViewModel delegates:
     ```csharp
     _documentService.SetTabsProvider(() => _viewModel.Tabs.ToList());
     _documentService.SetSelectedTabProvider(() => _viewModel.SelectedTab);
     _documentService.SetSelectTabAction(tab => { _viewModel.SelectedTab = tab; });
     ```
   - Configure MenuService with UI references (MenuBar, Grid)
   - Call `plugin.Initialize()` on each plugin
   - `MenuService.ApplyMenuItems()` adds plugin menu items to UI

**Why**: Services need ViewModels and WinUI elements that don't exist during DI registration.

### Plugin System

**Plugins implement `IPlugin`** with primary constructors for DI:
```csharp
public sealed class MyPlugin(IDocumentService docService, IMenuService menuService) : IPlugin
{
    public string Id => "Notepad.MyPlugin";
    public string Name => "My Feature";
    
    public void ConfigureServices(IServiceCollection services) 
    { 
        // Register plugin-specific services (optional)
    }
    
    public void Initialize() 
    {
        // Register UI: controls + menu items
        var control = menuService.RegisterPluginControl<MyControl>();
        menuService.RegisterMenuItem(new PluginMenuItem { ... });
    }
}
```

**Plugin controls implement `IPluginControl`**:
- Registered via `menuService.RegisterPluginControl<T>()` which adds to MainWindow grid
- `Show()`/`Hide()` control visibility
- `AutoHide` property determines click-outside behavior
- Always call `menuService.HideAllOverlays()` before showing your control

### Editor Integration (WinUIEdit)
Uses **WinUIEdit** package (v0.0.4-prerelease) from https://github.com/BreeceW/WinUIEdit

Access editor:
```csharp
var editor = _documentService.CurrentEditor; // CodeEditorControl from WinUIEdit
if (editor is not null)
{
    var text = editor.Editor.Text;
    var selStart = editor.Editor.SelectionStart;
    // Check WinUIEdit repo for API reference
}
```

**Consult WinUIEdit GitHub** for editor-specific APIs (find, replace, syntax highlighting, etc.)

### Service Provider Pattern
Core services expose **setters for delegates** to avoid circular dependencies:
- `DocumentService`: Uses function providers for Tabs, SelectedTab, CurrentEditor
- `EditorService`: Uses action for FocusEditor logic
- `MenuService`: Uses setters for MenuBar/Grid references

This allows services to be registered in DI before their dependencies exist.

### Session Management
`SessionService` persists state to `%LOCALAPPDATA%\Notepad\Session\`:
- `session.json` - tab metadata + selected tab ID
- `{tabId}.txt` - temp files for each tab's content
- Called automatically on window close, restored on startup

### NativeAOT Considerations
- Uses **JsonSourceGenerator** (`[JsonSerializable]` in `SessionService.cs`) for AOT-compatible serialization
- All reflection-sensitive APIs marked with `[DynamicallyAccessedMembers]` (see `MenuService.RegisterPluginControl<T>`)
- `IlcTrimMetadata=false` to preserve some debug info

## Build & Development

**Task**: Use VS Code task "Build Solution" or "Clean Build Solution" (see `.vscode/tasks.json` if exists)

## Key Files

**Entry Points**:
- `Notepad/App.xaml.cs` - DI configuration, plugin registration
- `Notepad/MainWindow.xaml.cs` - UI setup, delayed service configuration, plugin initialization

**Core Services** (in `Notepad/Services/`):
- `DocumentService.cs` - Tab/editor management via delegates
- `MenuService.cs` - Menu registration, plugin control hosting
- `SessionService.cs` - Persistence with AOT-compatible JSON

**Plugin Examples**:
- `Notepad.DefaultPlugins/FindReplace/` - Complex overlay with async search
- `Notepad.DefaultPlugins/GoToLine/` - Simple dialog pattern
- `Notepad.DefaultPlugins/QuickOpen/` - File picker integration

## Common Tasks

### Adding a Plugin
1. Create class implementing `IPlugin` with primary constructor
2. Register in `DefaultPluginsServiceCollectionExtensions.AddDefaultPlugins()`
3. Implement `Initialize()` to register menu items and controls
4. Create control implementing `IPluginControl` (typically UserControl)

### Adding a Menu Item
In plugin's `Initialize()`:
```csharp
menuService.RegisterMenuItem(new PluginMenuItem
{
    Category = "Edit", // File, Edit, View, Go
    Text = "My Action...",
    Shortcut = new PluginShortcut(VirtualKey.M, VirtualKeyModifiers.Control),
    Execute = MyAction,
    Order = 100 // Lower = appears first
});
```

### Working with Document Tabs
```csharp
// Get all tabs
var tabs = _documentService.Tabs;

// Get/set selected tab
var current = _documentService.SelectedTab;
_documentService.SelectedTab = someTab;

// Listen for tab changes
_documentService.SelectedTabChanged += (s, tab) => { /* ... */ };

// Open file (selects if already open)
await _documentService.OpenFileAsync(path);
```

### Focus Management
Always return focus after closing overlays:
```csharp
_editorService.FocusEditor();
```

MenuService automatically does this when plugin control visibility changes to Collapsed.

## Conventions
- **Primary constructors** for services/plugins (C# 12)
- **File-scoped namespaces**
- **Nullable reference types** enabled
- **ImplicitUsings** enabled
- **ObservableObject** from CommunityToolkit.Mvvm for ViewModels
- All plugin IDs use format: `"Notepad.{FeatureName}"`
- Menu categories: File, Edit, View, Go, Help

## Build & Release

### Build Commands
```bash
# Standard build
dotnet build

# Clean build (removes obj/bin from all projects)
rm -rf */bin */obj && dotnet build

# Publish NativeAOT (single-file executable)
dotnet publish -c Release -r win-x64
```

### Creating Installer
Uses InnoSetup for creating Windows installers:
```bash
# Publish first
dotnet publish Notepad/Notepad.csproj -c Release -r win-x64 -o ./publish

# Create installer (requires InnoSetup installed)
iscc installer.iss
```

### CI/CD
GitHub Actions workflow in `.github/workflows/build.yml`:
- Builds for x64 and ARM64
- Creates InnoSetup installer on version tags
- Uploads artifacts and creates releases

## Icon Attribution
Application icon by Dwi ridwanto from Flaticon:
https://www.flaticon.com/free-icon/notebook_13454528

# After making changes
Update `.github/copilot-instructions.md` if relevant details about architecture, patterns, or development practices have changed.