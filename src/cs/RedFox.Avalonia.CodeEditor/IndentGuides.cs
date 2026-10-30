namespace RedFox.Avalonia.CodeEditor;

/// <summary>
/// Measures the indentation that indent guides are drawn from.
/// </summary>
public static class IndentGuides
{
    /// <summary>
    /// Measures how many indent levels a line starts with.
    /// </summary>
    /// <param name="line">The line text.</param>
    /// <param name="tabSize">The columns one level occupies.</param>
    /// <returns>The level, or <c>-1</c> when the line is blank.</returns>
    public static int GetLevel(ReadOnlySpan<char> line, int tabSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tabSize);

        int columns = 0;

        foreach (char character in line)
        {
            if (character == '\t')
            {
                columns += tabSize - (columns % tabSize);
            }
            else if (character == ' ')
            {
                columns++;
            }
            else
            {
                return columns / tabSize;
            }
        }

        return -1;
    }
}
