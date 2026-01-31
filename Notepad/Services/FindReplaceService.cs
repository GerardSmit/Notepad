using System.Text.RegularExpressions;

namespace Notepad.Services;

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
/// Service for finding and replacing text in the editor.
/// </summary>
public static class FindReplaceService
{
    /// <summary>
    /// Finds all matches of the search text in the content.
    /// </summary>
    /// <param name="content">The text content to search in.</param>
    /// <param name="searchText">The text to search for.</param>
    /// <param name="options">The search options.</param>
    /// <returns>A list of all matches.</returns>
    public static List<FindMatch> FindAll(string content, string searchText, FindOptions options)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(searchText);

        if (string.IsNullOrEmpty(searchText))
        {
            return [];
        }

        var matches = new List<FindMatch>();
        var searchContent = content;
        var searchStart = 0;

        if (options.InSelection && options.SelectionEnd > options.SelectionStart)
        {
            searchStart = Math.Clamp(options.SelectionStart, 0, content.Length);
            var searchEnd = Math.Clamp(options.SelectionEnd, 0, content.Length);
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
                            matches.Add(new FindMatch(absoluteStart, absoluteEnd));
                        }
                    }
                    else
                    {
                        matches.Add(new FindMatch(absoluteStart, absoluteEnd));
                    }

                    index = foundIndex + 1;
                }
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // Regex took too long, return empty results
            return [];
        }
        catch (ArgumentException)
        {
            // Invalid regex pattern, return empty results
            return [];
        }

        return matches;
    }

    /// <summary>
    /// Finds the next match starting from the given position.
    /// </summary>
    /// <param name="content">The text content to search in.</param>
    /// <param name="searchText">The text to search for.</param>
    /// <param name="startPosition">The position to start searching from.</param>
    /// <param name="options">The search options.</param>
    /// <returns>The next match, or null if not found.</returns>
    public static FindMatch? FindNext(string content, string searchText, int startPosition, FindOptions options)
    {
        var allMatches = FindAll(content, searchText, options);
        
        // Find the first match that starts at or after the start position
        var nextMatch = allMatches.FirstOrDefault(m => m.Start >= startPosition);
        
        // If no match found after the position, wrap around to the first match
        return nextMatch ?? allMatches.FirstOrDefault();
    }

    /// <summary>
    /// Finds the previous match before the given position.
    /// </summary>
    /// <param name="content">The text content to search in.</param>
    /// <param name="searchText">The text to search for.</param>
    /// <param name="startPosition">The position to start searching from.</param>
    /// <param name="options">The search options.</param>
    /// <returns>The previous match, or null if not found.</returns>
    public static FindMatch? FindPrevious(string content, string searchText, int startPosition, FindOptions options)
    {
        var allMatches = FindAll(content, searchText, options);
        
        // Find the last match that starts before the start position
        var previousMatch = allMatches.LastOrDefault(m => m.Start < startPosition);
        
        // If no match found before the position, wrap around to the last match
        return previousMatch ?? allMatches.LastOrDefault();
    }

    /// <summary>
    /// Gets the replacement text, handling regex group references if applicable.
    /// </summary>
    /// <param name="content">The original content.</param>
    /// <param name="searchText">The search pattern.</param>
    /// <param name="replaceText">The replacement text.</param>
    /// <param name="match">The match to replace.</param>
    /// <param name="options">The find options.</param>
    /// <returns>The replacement text with group references resolved.</returns>
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
        // Check character before the match
        if (start > 0 && char.IsLetterOrDigit(content[start - 1]))
        {
            return false;
        }

        // Check character after the match
        if (end < content.Length && char.IsLetterOrDigit(content[end]))
        {
            return false;
        }

        return true;
    }
}
