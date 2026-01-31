using Microsoft.UI.Xaml;
using Notepad.Abstractions.Plugins;
using Notepad.Abstractions.Services;

namespace Notepad.DefaultPlugins;

// OverlayPluginBase removed
// Plugins now implement `IPlugin` directly and use primary constructors to receive services.
// See commit removing OverlayPluginBase for details.
