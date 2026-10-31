using System.IO.Compression;
using System.Text.Json;

namespace RedFox.Avalonia.CodeEditor;

/// <summary>
/// Reads the languages and grammars out of a VS Code extension. The extension's code and language server are not run.
/// </summary>
public sealed class CodeExtension
{
    private const string PackagePrefix = "extension/";

    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas	= true,
        CommentHandling		= JsonCommentHandling.Skip,
    };

    /// <summary>
    /// Gets the languages the extension contributes.
    /// </summary>
    public IReadOnlyList<CodeExtensionLanguage> Languages { get; }

    /// <summary>
    /// Gets the grammars the extension contributes.
    /// </summary>
    public IReadOnlyList<CodeExtensionGrammar> Grammars { get; }

    /// <summary>
    /// Loads an extension from a <c>.vsix</c> package or an installed extension folder.
    /// </summary>
    /// <param name="path">The package or folder path.</param>
    /// <returns>The loaded extension.</returns>
    public static CodeExtension Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        if (Directory.Exists(path))
        {
            string root = Path.GetFullPath(path);
            return Read(relativePath => File.ReadAllText(ResolveInFolder(root, relativePath)));
        }

        using ZipArchive archive = ZipFile.OpenRead(path);
        return Read(relativePath => ReadEntry(archive, relativePath));
    }

    private CodeExtension(IReadOnlyList<CodeExtensionLanguage> languages, IReadOnlyList<CodeExtensionGrammar> grammars)
    {
        Languages	= languages;
        Grammars	= grammars;
    }

    private static string ResolveInFolder(string root, string relativePath)
    {
        string resolved = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!resolved.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Extension file '{relativePath}' is outside the extension.");
        }

        return resolved;
    }

    private static string ReadEntry(ZipArchive archive, string relativePath)
    {
        string name				= PackagePrefix + Path.GetRelativePath(".", relativePath).Replace('\\', '/');
        ZipArchiveEntry entry	= archive.GetEntry(name) ?? throw new InvalidDataException($"The package has no file '{name}'.");

        using StreamReader reader = new(entry.Open());
        return reader.ReadToEnd();
    }

    private static CodeExtension Read(Func<string, string> readFile)
    {
        using JsonDocument manifest = JsonDocument.Parse(readFile("package.json"), JsonOptions);

        if (!manifest.RootElement.TryGetProperty("contributes", out JsonElement contributes))
        {
            return new CodeExtension([], []);
        }

        return new CodeExtension(ReadLanguages(contributes), ReadGrammars(contributes, readFile));
    }

    private static List<CodeExtensionLanguage> ReadLanguages(JsonElement contributes)
    {
        List<CodeExtensionLanguage> languages = [];

        if (contributes.TryGetProperty("languages", out JsonElement items))
        {
            foreach (JsonElement item in items.EnumerateArray())
            {
                string[] extensions = item.TryGetProperty("extensions", out JsonElement values) ? [.. values.EnumerateArray().Select(value => value.GetString() ?? string.Empty)] : [];
                languages.Add(new CodeExtensionLanguage(GetRequiredString(item, "id"), extensions));
            }
        }

        return languages;
    }

    private static List<CodeExtensionGrammar> ReadGrammars(JsonElement contributes, Func<string, string> readFile)
    {
        List<CodeExtensionGrammar> grammars = [];

        if (contributes.TryGetProperty("grammars", out JsonElement items))
        {
            foreach (JsonElement item in items.EnumerateArray())
            {
                if (item.TryGetProperty("language", out JsonElement language))
                {
                    string path = GetRequiredString(item, "path");
                    if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException($"Grammar '{path}' is not JSON, the only form that can be read.");
                    }

                    grammars.Add(new CodeExtensionGrammar(language.GetString() ?? string.Empty, GetRequiredString(item, "scopeName"), readFile(path)));
                }
            }
        }

        return grammars;
    }

    private static string GetRequiredString(JsonElement element, string name)
    {
        return element.GetProperty(name).GetString() ?? throw new InvalidDataException($"The manifest's '{name}' is empty.");
    }
}
