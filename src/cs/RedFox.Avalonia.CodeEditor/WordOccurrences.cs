namespace RedFox.Avalonia.CodeEditor;

/// <summary>
/// Finds the word under the caret and its other occurrences in a line.
/// </summary>
public static class WordOccurrences
{
    private const int WordKind = 0;
    private const int WhitespaceKind = 1;
    private const int SymbolKind = 2;

    /// <summary>
    /// Finds the word touching a column.
    /// </summary>
    /// <param name="line">The line text.</param>
    /// <param name="column">The zero-based caret column.</param>
    /// <returns>The word, or an empty string when the caret is not on one.</returns>
    public static string FindWord(string line, int column)
    {
        ArgumentNullException.ThrowIfNull(line);

        int start	= Math.Min(column, line.Length);
        int end		= start;

        while (start > 0 && IsWordCharacter(line[start - 1]))
        {
            start--;
        }

        while (end < line.Length && IsWordCharacter(line[end]))
        {
            end++;
        }

        return line[start..end];
    }

    /// <summary>
    /// Finds the run of the same kind of character around a column, as a double click selects it: word characters,
    /// whitespace or symbols. A column past the end of the line uses the line's last character.
    /// </summary>
    /// <param name="line">The line text.</param>
    /// <param name="column">The zero-based column of the character.</param>
    /// <returns>The first column of the run and its length, which is zero on an empty line.</returns>
    public static (int Start, int Length) FindRun(string line, int column)
    {
        ArgumentNullException.ThrowIfNull(line);

        if (line.Length == 0)
        {
            return (0, 0);
        }

        int anchor	= Math.Clamp(column, 0, line.Length - 1);
        int kind	= GetKind(line[anchor]);
        int start	= anchor;
        int end		= anchor + 1;

        while (start > 0 && GetKind(line[start - 1]) == kind)
        {
            start--;
        }

        while (end < line.Length && GetKind(line[end]) == kind)
        {
            end++;
        }

        return (start, end - start);
    }

    /// <summary>
    /// Finds the columns a term occurs at in a line.
    /// </summary>
    /// <param name="line">The line text.</param>
    /// <param name="term">The term.</param>
    /// <param name="wholeWord">Whether a match must not sit inside a longer word.</param>
    /// <returns>The start column of each occurrence.</returns>
    public static List<int> Find(string line, string term, bool wholeWord)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentException.ThrowIfNullOrEmpty(term);

        List<int> columns = [];

        for (int index = line.IndexOf(term, StringComparison.Ordinal); index >= 0; index = line.IndexOf(term, index + term.Length, StringComparison.Ordinal))
        {
            if (!wholeWord || IsWholeWord(line, index, term.Length))
            {
                columns.Add(index);
            }
        }

        return columns;
    }

    private static bool IsWholeWord(string line, int start, int length)
    {
        int end = start + length;
        return (start == 0 || !IsWordCharacter(line[start - 1])) && (end == line.Length || !IsWordCharacter(line[end]));
    }

    private static int GetKind(char character)
    {
        if (IsWordCharacter(character))
        {
            return WordKind;
        }

        return character is ' ' or '\t' ? WhitespaceKind : SymbolKind;
    }

    private static bool IsWordCharacter(char character)
    {
        return char.IsLetterOrDigit(character) || character == '_';
    }
}
