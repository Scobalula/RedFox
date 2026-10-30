namespace RedFox.Avalonia.CodeEditor;

/// <summary>
/// Describes the comments and strings a bracket scan has to step over.
/// </summary>
/// <param name="LineComment">The text that starts a line comment.</param>
/// <param name="BlockCommentOpen">The text that starts a block comment.</param>
/// <param name="BlockCommentClose">The text that ends a block comment.</param>
/// <param name="Quotes">The characters that delimit a string.</param>
public sealed record BracketSyntax(string LineComment, string BlockCommentOpen, string BlockCommentClose, string Quotes)
{
    private const string CStyleBlockOpen = "/*";
    private const string CStyleBlockClose = "*/";

    /// <summary>
    /// Gets the syntax shared by GSC, CSC and JSON.
    /// </summary>
    public static BracketSyntax CStyle { get; } = new("//", CStyleBlockOpen, CStyleBlockClose, "\"");

    /// <summary>
    /// Gets the Lua syntax.
    /// </summary>
    public static BracketSyntax Lua { get; } = new("--", "--[[", "]]", "\"'");

    /// <summary>
    /// Selects the syntax for a file name, or <see langword="null"/> when brackets mean nothing in that format.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>The syntax to scan with.</returns>
    public static BracketSyntax? ForFile(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".lua"								=> Lua,
            ".csv" or ".tsv" or ".txt" or ""	=> null,
            _									=> CStyle,
        };
    }
}
