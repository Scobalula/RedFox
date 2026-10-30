namespace RedFox.Avalonia.CodeEditor;

/// <summary>
/// Describes a TextMate grammar a VS Code extension contributes.
/// </summary>
/// <param name="Language">The language id the grammar colours.</param>
/// <param name="ScopeName">The grammar's root scope.</param>
/// <param name="Content">The grammar's JSON.</param>
public sealed record CodeExtensionGrammar(string Language, string ScopeName, string Content);
