using System.Text;
using System.Text.RegularExpressions;

namespace Notepad.DefaultPlugins.Services;

/// <summary>
/// Options for the find operation.
/// </summary>
public record FindOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the search is case-sensitive.
    /// </summary>
    public bool MatchCase { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to match whole words only.
    /// </summary>
    public bool MatchWholeWord { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to use regex for searching.
    /// </summary>
    public bool UseRegex { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to search only within the selection.
    /// </summary>
    public bool InSelection { get; init; }

    /// <summary>
    /// Gets or sets the selection start position for "in selection" search.
    /// </summary>
    public int SelectionStart { get; init; }

    /// <summary>
    /// Gets or sets the selection end position for "in selection" search.
    /// </summary>
    public int SelectionEnd { get; init; }
}

/// <summary>
/// Represents a search match result.
/// </summary>
public record FindMatch(int Start, int End)
{
    /// <summary>
    /// Gets the length of the match.
    /// </summary>
    public int Length => End - Start;
}

/// <summary>
/// Result of a find operation containing matches and metadata.
/// </summary>
public record FindResult(List<FindMatch> Matches, bool HasMoreMatches)
{
    /// <summary>
    /// The default maximum number of matches to return.
    /// </summary>
    public const int DefaultMaxMatches = 10000;
}

/// <summary>
/// Service for finding and replacing text in the editor.
/// </summary>
public static class FindReplaceService
{
    /// <summary>
    /// Finds all matches of the search text in the content.
    /// </summary>
    /// <param name="content">The content to search in.</param>
    /// <param name="searchText">The text to search for.</param>
    /// <param name="options">The search options.</param>
    /// <param name="maxMatches">Maximum number of matches to return. Use -1 for unlimited.</param>
    /// <returns>A FindResult containing matches and whether more matches exist.</returns>
    public static FindResult FindAll(string content, string searchText, FindOptions options, int maxMatches = FindResult.DefaultMaxMatches)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(searchText);

        if (string.IsNullOrEmpty(searchText))
        {
            return new FindResult([], false);
        }

        var matches = new List<FindMatch>();
        var searchContent = content;
        var searchStart = 0;
        int searchEnd;
        var hasMoreMatches = false;
        var effectiveMaxMatches = maxMatches < 0 ? int.MaxValue : maxMatches;

        if (options.InSelection && options.SelectionEnd > options.SelectionStart)
        {
            searchStart = Math.Clamp(options.SelectionStart, 0, content.Length);
            searchEnd = Math.Clamp(options.SelectionEnd, 0, content.Length);
            searchContent = content[searchStart..searchEnd];
        }

        try
        {
            if (options.UseRegex)
            {
                var regexOptions = RegexOptions.Multiline;
                if (!options.MatchCase)
                {
                    regexOptions |= RegexOptions.IgnoreCase;
                }

                var pattern = searchText;
                if (options.MatchWholeWord)
                {
                    pattern = $@"\b{pattern}\b";
                }

                var regex = new Regex(pattern, regexOptions, TimeSpan.FromSeconds(1));
                var regexMatches = regex.Matches(searchContent);

                foreach (Match match in regexMatches)
                {
                    if (matches.Count >= effectiveMaxMatches)
                    {
                        hasMoreMatches = true;
                        break;
                    }
                    matches.Add(new FindMatch(searchStart + match.Index, searchStart + match.Index + match.Length));
                }
            }
            else
            {
                var comparison = options.MatchCase
                    ? StringComparison.Ordinal
                    : StringComparison.OrdinalIgnoreCase;

                var index = 0;
                while (index < searchContent.Length)
                {
                    var foundIndex = searchContent.IndexOf(searchText, index, comparison);
                    if (foundIndex < 0)
                    {
                        break;
                    }

                    var absoluteStart = searchStart + foundIndex;
                    var absoluteEnd = absoluteStart + searchText.Length;

                    if (options.MatchWholeWord)
                    {
                        if (IsWholeWord(content, absoluteStart, absoluteEnd))
                        {
                            if (matches.Count >= effectiveMaxMatches)
                            {
                                hasMoreMatches = true;
                                break;
                            }
                            matches.Add(new FindMatch(absoluteStart, absoluteEnd));
                        }
                    }
                    else
                    {
                        if (matches.Count >= effectiveMaxMatches)
                        {
                            hasMoreMatches = true;
                            break;
                        }
                        matches.Add(new FindMatch(absoluteStart, absoluteEnd));
                    }

                    index = foundIndex + 1;
                }
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return new FindResult([], false);
        }
        catch (ArgumentException)
        {
            return new FindResult([], false);
        }

        return new FindResult(matches, hasMoreMatches);
    }

    /// <summary>
    /// Finds all matches asynchronously, returning byte positions for Scintilla.
    /// </summary>
    /// <param name="content">The content to search in (raw bytes from Scintilla).</param>
    /// <param name="searchText">The text to search for.</param>
    /// <param name="options">The search options.</param>
    /// <param name="maxMatches">Maximum number of matches to return. Use -1 for unlimited.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A FindResult containing matches with byte positions.</returns>
    public static Task<FindResult> FindAllAsync(
        byte[] content,
        string searchText,
        FindOptions options,
        int maxMatches = FindResult.DefaultMaxMatches,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => FindAllBytes(content, searchText, options, maxMatches, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Finds all matches in byte content, returning byte positions for Scintilla.
    /// </summary>
    private static FindResult FindAllBytes(
        byte[] content,
        string searchText,
        FindOptions options,
        int maxMatches,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(searchText);

        if (string.IsNullOrEmpty(searchText) || content.Length == 0)
        {
            return new FindResult([], false);
        }

        var matches = new List<FindMatch>();
        var hasMoreMatches = false;
        var effectiveMaxMatches = maxMatches < 0 ? int.MaxValue : maxMatches;

        // Convert content to string for searching, but track byte positions
        var text = Encoding.UTF8.GetString(content);
        var searchStart = 0;
        var searchEnd = text.Length;

        if (options.InSelection && options.SelectionEnd > options.SelectionStart)
        {
            // Selection positions are in bytes, convert to char positions
            searchStart = ByteToCharPosition(content, Math.Clamp(options.SelectionStart, 0, content.Length));
            searchEnd = ByteToCharPosition(content, Math.Clamp(options.SelectionEnd, 0, content.Length));
        }

        var searchContent = text[searchStart..searchEnd];

        try
        {
            List<(int charStart, int charEnd)> charMatches = [];

            if (options.UseRegex)
            {
                var regexOptions = RegexOptions.Multiline;
                if (!options.MatchCase)
                {
                    regexOptions |= RegexOptions.IgnoreCase;
                }

                var pattern = searchText;
                if (options.MatchWholeWord)
                {
                    pattern = $@"\b{pattern}\b";
                }

                var regex = new Regex(pattern, regexOptions, TimeSpan.FromSeconds(1));
                var regexMatches = regex.Matches(searchContent);

                foreach (Match match in regexMatches)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (charMatches.Count >= effectiveMaxMatches)
                    {
                        hasMoreMatches = true;
                        break;
                    }
                    charMatches.Add((searchStart + match.Index, searchStart + match.Index + match.Length));
                }
            }
            else
            {
                var comparison = options.MatchCase
                    ? StringComparison.Ordinal
                    : StringComparison.OrdinalIgnoreCase;

                var index = 0;
                while (index < searchContent.Length)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var foundIndex = searchContent.IndexOf(searchText, index, comparison);
                    if (foundIndex < 0)
                    {
                        break;
                    }

                    var absoluteStart = searchStart + foundIndex;
                    var absoluteEnd = absoluteStart + searchText.Length;

                    if (options.MatchWholeWord)
                    {
                        if (IsWholeWord(text, absoluteStart, absoluteEnd))
                        {
                            if (charMatches.Count >= effectiveMaxMatches)
                            {
                                hasMoreMatches = true;
                                break;
                            }
                            charMatches.Add((absoluteStart, absoluteEnd));
                        }
                    }
                    else
                    {
                        if (charMatches.Count >= effectiveMaxMatches)
                        {
                            hasMoreMatches = true;
                            break;
                        }
                        charMatches.Add((absoluteStart, absoluteEnd));
                    }

                    index = foundIndex + 1;
                }
            }

            // Convert char positions to byte positions
            foreach (var (charStart, charEnd) in charMatches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var byteStart = CharToBytePosition(text, charStart);
                var byteEnd = CharToBytePosition(text, charEnd);
                matches.Add(new FindMatch(byteStart, byteEnd));
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return new FindResult([], false);
        }
        catch (ArgumentException)
        {
            return new FindResult([], false);
        }

        return new FindResult(matches, hasMoreMatches);
    }

    /// <summary>
    /// Converts a byte position to a character position in UTF-8 text.
    /// </summary>
    private static int ByteToCharPosition(byte[] bytes, int bytePos)
    {
        if (bytePos <= 0) return 0;
        if (bytePos >= bytes.Length) return Encoding.UTF8.GetString(bytes).Length;
        return Encoding.UTF8.GetString(bytes, 0, bytePos).Length;
    }

    /// <summary>
    /// Converts a character position to a byte position in UTF-8 text.
    /// </summary>
    private static int CharToBytePosition(string text, int charPos)
    {
        if (charPos <= 0) return 0;
        if (charPos >= text.Length) return Encoding.UTF8.GetByteCount(text);
        return Encoding.UTF8.GetByteCount(text[..charPos]);
    }

    /// <summary>
    /// Gets the replacement text, handling regex group references if applicable.
    /// </summary>
    public static string GetReplacementText(string content, string searchText, string replaceText, FindMatch match, FindOptions options)
    {
        if (!options.UseRegex)
        {
            return replaceText;
        }

        try
        {
            var regexOptions = RegexOptions.Multiline;
            if (!options.MatchCase)
            {
                regexOptions |= RegexOptions.IgnoreCase;
            }

            var pattern = searchText;
            if (options.MatchWholeWord)
            {
                pattern = $@"\b{pattern}\b";
            }

            var regex = new Regex(pattern, regexOptions, TimeSpan.FromSeconds(1));
            var matchText = content.Substring(match.Start, match.Length);
            
            return regex.Replace(matchText, replaceText);
        }
        catch
        {
            return replaceText;
        }
    }

    private static bool IsWholeWord(string content, int start, int end)
    {
        if (start > 0 && char.IsLetterOrDigit(content[start - 1]))
        {
            return false;
        }

        if (end < content.Length && char.IsLetterOrDigit(content[end]))
        {
            return false;
        }

        return true;
    }
}
