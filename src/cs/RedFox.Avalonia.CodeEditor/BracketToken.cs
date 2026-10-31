namespace RedFox.Avalonia.CodeEditor;

/// <summary>
/// Describes one bracket found in a document.
/// </summary>
/// <param name="Offset">The offset of the bracket.</param>
/// <param name="Depth">How many brackets enclose it.</param>
/// <param name="Character">The bracket character.</param>
public readonly record struct BracketToken(int Offset, int Depth, char Character);
