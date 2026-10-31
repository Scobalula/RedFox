namespace RedFox.Avalonia.CodeEditor;

/// <summary>
/// Describes a language a VS Code extension contributes.
/// </summary>
/// <param name="Id">The language id.</param>
/// <param name="FileExtensions">The file extensions, each with its leading dot.</param>
public sealed record CodeExtensionLanguage(string Id, IReadOnlyList<string> FileExtensions);
