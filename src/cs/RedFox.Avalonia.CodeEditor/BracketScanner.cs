namespace RedFox.Avalonia.CodeEditor;

/// <summary>
/// Finds the brackets of a document and how deeply each is nested, stepping over comments and strings.
/// </summary>
public static class BracketScanner
{
    private const string OpeningBrackets = "([{";
    private const string ClosingBrackets = ")]}";

    /// <summary>
    /// Scans a document.
    /// </summary>
    /// <param name="text">The document text.</param>
    /// <param name="syntax">The comment and string syntax.</param>
    /// <returns>The brackets, ordered by offset.</returns>
    public static List<BracketToken> Scan(ReadOnlySpan<char> text, BracketSyntax syntax)
    {
        ArgumentNullException.ThrowIfNull(syntax);

        List<BracketToken> brackets	= [];
        int depth					= 0;
        int index					= 0;

        while (index < text.Length)
        {
            ReadOnlySpan<char> rest	= text[index..];
            char character			= text[index];

            if (rest.StartsWith(syntax.BlockCommentOpen))
            {
                index = SkipPast(text, index + syntax.BlockCommentOpen.Length, syntax.BlockCommentClose);
            }
            else if (rest.StartsWith(syntax.LineComment))
            {
                index = SkipPast(text, index + syntax.LineComment.Length, "\n");
            }
            else if (syntax.Quotes.Contains(character))
            {
                index = SkipString(text, index);
            }
            else
            {
                if (OpeningBrackets.Contains(character))
                {
                    brackets.Add(new BracketToken(index, depth++, character));
                }
                else if (ClosingBrackets.Contains(character))
                {
                    depth = Math.Max(depth - 1, 0);
                    brackets.Add(new BracketToken(index, depth, character));
                }

                index++;
            }
        }

        return brackets;
    }

    private static int SkipPast(ReadOnlySpan<char> text, int start, string terminator)
    {
        int found = text[start..].IndexOf(terminator);
        return found < 0 ? text.Length : start + found + terminator.Length;
    }

    private static int SkipString(ReadOnlySpan<char> text, int start)
    {
        char quote = text[start];

        for (int index = start + 1; index < text.Length; index++)
        {
            if (text[index] == '\\')
            {
                index++;
            }
            else if (text[index] == quote || text[index] == '\n')
            {
                return index + 1;
            }
        }

        return text.Length;
    }
}
