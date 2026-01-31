# Notepad

A modern, lightweight text editor built with WinUI 3 and .NET 10.

![Notepad Icon](Notepad/Assets/notebook.png)

## Features

- **Tabbed Interface** - Work with multiple files simultaneously
- **NativeAOT Compilation** - Fast startup and low memory footprint
- **Plugin Architecture** - Extensible through a simple plugin system
- **Session Persistence** - Automatically saves and restores your workspace
- **Modern UI** - Windows 11 Mica backdrop with WinUI 3 controls

### Built-in Plugins

- **Find & Replace** - Search and replace text with keyboard shortcuts
- **Go to Line** - Jump to specific line numbers
- **Quick Open** - Rapidly open files from your filesystem
- **Recent Files** - Quick access to recently opened files
- **Tab Preview** - Preview tab contents on hover

## Requirements

- Windows 10 version 1809 or later
- [Windows App SDK Runtime 1.8](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads) (automatically installed by the installer)

## Installation

Download the latest installer from the [Releases](https://github.com/user/notepad/releases) page and run `NotepadSetup-x.x.x.exe`.

The installer will automatically download and install the Windows App SDK Runtime if needed.

## Building from Source

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Inno Setup 6](https://jrsoftware.org/isinfo.php) (for creating the installer)

### Build Commands

```bash
# Debug build
dotnet build

# Release build
dotnet build --configuration Release

# Publish NativeAOT (single-file executable)
dotnet publish Notepad/Notepad.csproj -c Release -r win-x64 --self-contained -p:PublishAot=true -o ./publish
```

### Creating the Installer

```bash
# First, publish the application
dotnet publish Notepad/Notepad.csproj -c Release -r win-x64 --self-contained -p:PublishAot=true -o ./publish

# Then create the installer (requires Inno Setup)
iscc installer.iss

# With custom version:
iscc /DMyAppVersion=1.2.3 installer.iss
```

The installer will be created in the `./installer` directory.

## Project Structure

- `Notepad/` - Main WinUI 3 application
- `Notepad.Abstractions/` - Plugin interfaces and models
- `Notepad.DefaultPlugins/` - Built-in plugins

## License

This project is licensed under the MIT License - see the [LICENSE.txt](LICENSE.txt) file for details.

## Credits

### Icon

The application icon is designed by [Dwi ridwanto](https://www.flaticon.com/authors/dwi-ridwanto) from [Flaticon](https://www.flaticon.com/free-icon/notebook_13454528).

Licensed under Flaticon's Free License - attribution required.

### Third-Party Libraries

This application uses the following open-source libraries:

- **CommunityToolkit.Mvvm** - MIT License - [GitHub](https://github.com/CommunityToolkit/dotnet)
- **Microsoft.Extensions.DependencyInjection** - MIT License - [GitHub](https://github.com/dotnet/runtime)
- **Microsoft.Extensions.Logging** - MIT License - [GitHub](https://github.com/dotnet/runtime)
- **Microsoft.WindowsAppSDK** - MIT License - [GitHub](https://github.com/microsoft/WindowsAppSDK)
- **WinUIEdit** - MIT License - [GitHub](https://github.com/BreeceW/WinUIEdit)
