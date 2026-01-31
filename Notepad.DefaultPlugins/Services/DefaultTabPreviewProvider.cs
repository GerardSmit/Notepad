using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Notepad.Abstractions.Models;
using Notepad.Abstractions.Services;

namespace Notepad.DefaultPlugins.Services;

/// <summary>
/// Default implementation of tab preview provider.
/// </summary>
public sealed class DefaultTabPreviewProvider : ITabPreviewProvider
{
    private const int MaxPreviewLines = 5;
    private const int MaxLineLength = 80;

    /// <inheritdoc/>
    public bool Supports(DocumentTab tab)
    {
        return true;
    }

    /// <inheritdoc/>
    public UIElement GetPreview(DocumentTab tab)
    {
        var stackPanel = new StackPanel
        {
            Spacing = 4,
            MaxWidth = 500
        };

        string pathText;
        bool isPathItalic;

        if (!string.IsNullOrEmpty(tab.FilePath))
        {
            pathText = tab.FilePath;
            isPathItalic = false;
        }
        else
        {
            pathText = "Unsaved file";
            isPathItalic = true;
        }

        var pathTextBlock = new TextBlock
        {
            Text = pathText,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontStyle = isPathItalic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal,
            Opacity = isPathItalic ? 0.7 : 1.0
        };
        stackPanel.Children.Add(pathTextBlock);

        var contentBytes = tab.Content.ToArray();
        var (previewText, hasMoreLines) = BuildContentPreview(contentBytes.AsSpan(), MaxPreviewLines, MaxLineLength);

        if (previewText.Length > 0)
        {
            var previewTextBlock = new TextBlock
            {
                Text = hasMoreLines ? previewText + "\n…" : previewText,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Opacity = 0.85,
                TextWrapping = TextWrapping.NoWrap
            };
            stackPanel.Children.Add(previewTextBlock);
        }

        return stackPanel;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
    }

    private static (string preview, bool hasMoreLines) BuildContentPreview(ReadOnlySpan<byte> contentBytes, int maxLines, int maxLineLength)
    {
        if (contentBytes.IsEmpty)
        {
            return (string.Empty, false);
        }

        var content = Encoding.UTF8.GetString(contentBytes);
        return BuildContentPreviewFromString(content.AsSpan(), maxLines, maxLineLength);
    }

    private static (string preview, bool hasMoreLines) BuildContentPreviewFromString(ReadOnlySpan<char> content, int maxLines, int maxLineLength)
    {
        if (content.IsEmpty)
        {
            return (string.Empty, false);
        }

        var sb = new StringBuilder();
        var remaining = content;
        var lineCount = 0;
        var hasMoreLines = false;
        var hasNonWhitespace = false;

        while (!remaining.IsEmpty && lineCount < maxLines)
        {
            var newlineIndex = remaining.IndexOf('\n');
            ReadOnlySpan<char> line;

            if (newlineIndex >= 0)
            {
                line = remaining[..newlineIndex];
                remaining = remaining[(newlineIndex + 1)..];
            }
            else
            {
                line = remaining;
                remaining = [];
            }

            if (line.Length > 0 && line[^1] == '\r')
            {
                line = line[..^1];
            }

            if (lineCount > 0)
            {
                sb.Append('\n');
            }

            if (line.Length > maxLineLength)
            {
                sb.Append(line[..maxLineLength]);
                sb.Append('…');
            }
            else
            {
                sb.Append(line);
            }

            if (!hasNonWhitespace && !line.IsWhiteSpace())
            {
                hasNonWhitespace = true;
            }

            lineCount++;
        }

        // Check if there are more lines
        if (!remaining.IsEmpty)
        {
            hasMoreLines = true;
        }

        return hasNonWhitespace ? (sb.ToString(), hasMoreLines) : (string.Empty, false);
    }
}
