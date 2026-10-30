namespace RedFox.Avalonia.CodeEditor;

/// <summary>
/// Pairs brackets, so the one touching the caret can show its partner.
/// </summary>
public static class BracketPairs
{
    /// <summary>
    /// Pairs the brackets of a document. A bracket with no partner of its own kind is left out.
    /// </summary>
    /// <param name="brackets">The brackets, ordered by offset.</param>
    /// <returns>The partner offset of each paired bracket, keyed by both offsets.</returns>
    public static Dictionary<int, int> Build(IReadOnlyList<BracketToken> brackets)
    {
        ArgumentNullException.ThrowIfNull(brackets);

        Dictionary<int, int> pairs = [];
        Stack<BracketToken> open = new();

        foreach (BracketToken bracket in brackets)
        {
            if (bracket.Character is '(' or '[' or '{')
            {
                open.Push(bracket);
            }
            else if (open.Count > 0 && open.Peek().Character == GetOpening(bracket.Character))
            {
                BracketToken opening = open.Pop();
                pairs[opening.Offset]	= bracket.Offset;
                pairs[bracket.Offset]	= opening.Offset;
            }
        }

        return pairs;
    }

    /// <summary>
    /// Finds the bracket touching the caret, the one before it first, as VS Code does.
    /// </summary>
    /// <param name="pairs">The pairs from <see cref="Build"/>.</param>
    /// <param name="caretOffset">The caret offset.</param>
    /// <param name="bracket">The bracket touching the caret.</param>
    /// <param name="partner">Its partner.</param>
    /// <returns><see langword="true"/> when the caret touches a paired bracket.</returns>
    public static bool TryFind(IReadOnlyDictionary<int, int> pairs, int caretOffset, out int bracket, out int partner)
    {
        ArgumentNullException.ThrowIfNull(pairs);

        bracket = pairs.ContainsKey(caretOffset - 1) ? caretOffset - 1 : caretOffset;
        return pairs.TryGetValue(bracket, out partner);
    }

    private static char GetOpening(char closing)
    {
        return closing switch
        {
            ')'	=> '(',
            ']'	=> '[',
            _	=> '{',
        };
    }
}
