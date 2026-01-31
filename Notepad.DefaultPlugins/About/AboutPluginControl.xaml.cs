using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Notepad.Abstractions.Plugins;
using Notepad.Abstractions.Services;
using Rectangle = Microsoft.UI.Xaml.Shapes.Rectangle;

namespace Notepad.DefaultPlugins.About;

/// <summary>
/// A user control that displays the About dialog.
/// </summary>
public sealed partial class AboutPluginControl : IPluginControl
{
    private readonly IEditorService _editorService;
    private bool _licensesLoaded;

    /// <summary>
    /// Initializes a new instance of the <see cref="AboutPluginControl"/> class.
    /// </summary>
    public AboutPluginControl(IEditorService editorService)
    {
        _editorService = editorService;
        InitializeComponent();

        // Set version from assembly
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        if (version is not null)
        {
            VersionText.Text = $"Version {version.Major}.{version.Minor}.{version.Build}";
        }

        // Load icon from file
        LoadIcon();

        // Handle Escape key
        KeyDown += OnKeyDown;
    }

    /// <inheritdoc/>
    public bool IsOpen => Visibility == Visibility.Visible;

    /// <inheritdoc/>
    public bool AutoHide => true;

    /// <inheritdoc/>
    public void Show()
    {
        // Load licenses on first show
        if (!_licensesLoaded)
        {
            _licensesLoaded = true;
            LoadLicenses();
        }

        Visibility = Visibility.Visible;
    }

    /// <inheritdoc/>
    public void Hide()
    {
        Visibility = Visibility.Collapsed;
        _editorService.FocusEditor();
    }

    private void LoadIcon()
    {
        var exePath = Environment.ProcessPath;
        if (exePath is null) return;

        var iconPath = Path.Combine(Path.GetDirectoryName(exePath)!, "Assets", "notebook.png");
        if (File.Exists(iconPath))
        {
            var bitmap = new BitmapImage(new Uri(iconPath));
            AppIcon.Source = bitmap;
        }
    }

    private void LoadLicenses()
    {
        var exePath = Environment.ProcessPath;
        if (exePath is null) return;

        var licenseFile = Path.Combine(Path.GetDirectoryName(exePath)!, "LICENSE.txt");
        if (!File.Exists(licenseFile)) return;

        var content = File.ReadAllText(licenseFile);
        var sections = ParseLicenseSections(content);

        foreach (var section in sections)
        {
            if (section.Title == "THIRD-PARTY LICENSES")
            {
                // Add a header for third-party licenses
                LicensesPanel.Children.Add(new TextBlock
                {
                    Text = "Third-Party Licenses",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                });
                continue;
            }

            // Check if this is the icon attribution section
            if (section.Title.Contains("Icon", StringComparison.OrdinalIgnoreCase))
            {
                AddIconAttribution(section);
            }
            else if (!string.IsNullOrEmpty(section.Title) && section.Title != "MIT License")
            {
                // Add as expandable license section
                AddLicenseExpander(section);
            }
        }
    }

    private void AddIconAttribution(LicenseSection section)
    {
        var panel = new StackPanel { Spacing = 4 };

        panel.Children.Add(new TextBlock
        {
            Text = section.Title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        // Extract URL from content if present
        var urlMatch = Regex.Match(section.Content, @"https?://[^\s]+");
        var contentWithoutUrl = urlMatch.Success
            ? section.Content.Replace(urlMatch.Value, "").Trim()
            : section.Content;

        panel.Children.Add(new TextBlock
        {
            Text = contentWithoutUrl,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.8,
            FontSize = 12
        });

        if (urlMatch.Success)
        {
            panel.Children.Add(new HyperlinkButton
            {
                Content = "View on Flaticon",
                NavigateUri = new Uri(urlMatch.Value),
                Padding = new Thickness(0)
            });
        }

        LicensesPanel.Children.Add(panel);

        // Add separator
        LicensesPanel.Children.Add(new Rectangle
        {
            Fill = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
            Height = 1
        });
    }

    private void AddLicenseExpander(LicenseSection section)
    {
        // Extract URL from the first line of content
        var urlMatch = Regex.Match(section.Content, @"https?://[^\s]+");
        
        var panel = new StackPanel { Spacing = 4 };

        // Create grid with title and license type
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleText = new TextBlock
        {
            Text = section.Title,
            VerticalAlignment = VerticalAlignment.Center
        };

        var licenseType = ExtractLicenseType(section.Content);
        var licenseText = new TextBlock
        {
            Text = licenseType,
            Opacity = 0.6,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(licenseText, 1);

        headerGrid.Children.Add(titleText);
        headerGrid.Children.Add(licenseText);
        panel.Children.Add(headerGrid);

        // Add clickable URL if found
        if (urlMatch.Success)
        {
            panel.Children.Add(new HyperlinkButton
            {
                Content = "View License",
                NavigateUri = new Uri(urlMatch.Value),
                Padding = new Thickness(0),
                FontSize = 12
            });
        }

        LicensesPanel.Children.Add(panel);

        // Add separator
        LicensesPanel.Children.Add(new Rectangle
        {
            Fill = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
            Height = 1,
            Margin = new Thickness(0, 4, 0, 0)
        });
    }

    private static List<LicenseSection> ParseLicenseSections(string content)
    {
        var sections = new List<LicenseSection>();
        var separator = new string('-', 80);

        // Split by the separator line
        var parts = content.Split(separator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            var lines = trimmed.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) continue;

            // First line(s) before a dashed underline is the title
            var title = "";
            var contentStartIndex = 0;

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.All(c => c == '-') && line.Length > 3)
                {
                    // Previous line was the title
                    title = i > 0 ? lines[i - 1].Trim() : "";
                    contentStartIndex = i + 1;
                    break;
                }
            }

            // If no dashed underline found, first line is title
            if (string.IsNullOrEmpty(title) && lines.Length > 0)
            {
                title = lines[0].Trim();
                contentStartIndex = 1;
            }

            var sectionContent = string.Join("\n",
                lines.Skip(contentStartIndex).Select(l => l.Trim()));

            sections.Add(new LicenseSection(title, sectionContent));
        }

        return sections;
    }

    private static string ExtractLicenseType(string content)
    {
        if (content.Contains("MIT", StringComparison.OrdinalIgnoreCase))
            return "MIT";
        if (content.Contains("Apache", StringComparison.OrdinalIgnoreCase))
            return "Apache 2.0";
        if (content.Contains("BSD", StringComparison.OrdinalIgnoreCase))
            return "BSD";
        if (content.Contains("Flaticon", StringComparison.OrdinalIgnoreCase))
            return "Flaticon";
        return "License";
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            Hide();
            e.Handled = true;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void ViewLicense_Click(object sender, RoutedEventArgs e)
    {
        // Try to open LICENSE.txt from the application directory
        var exePath = Environment.ProcessPath;
        if (exePath is not null)
        {
            var licenseFile = Path.Combine(Path.GetDirectoryName(exePath)!, "LICENSE.txt");
            if (File.Exists(licenseFile))
            {
                Process.Start(new ProcessStartInfo(licenseFile) { UseShellExecute = true });
            }
        }
    }

    private sealed record LicenseSection(string Title, string Content);
}
