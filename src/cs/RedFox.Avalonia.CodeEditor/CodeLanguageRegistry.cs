using System.Text;
using TextMateSharp.Grammars;
using TextMateSharp.Internal.Grammars.Reader;
using TextMateSharp.Internal.Types;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace RedFox.Avalonia.CodeEditor;

/// <summary>
/// Resolves files to TextMate grammars. Later extensions replace the built-in grammar of a file extension.
/// </summary>
public sealed class CodeLanguageRegistry : IRegistryOptions
{
    private const string GscScope = "source.gsc";
    private const string CsvScope = "text.csv";

    private readonly RegistryOptions _bundled = new(ThemeName.DarkPlus);
    private readonly Dictionary<string, string> _scopesByExtension = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Func<string>> _grammars = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the registry every editor uses unless it is given its own.
    /// </summary>
    public static CodeLanguageRegistry Default { get; } = new();

    /// <summary>
    /// Initializes a registry with the GSC and CSV grammars.
    /// </summary>
    public CodeLanguageRegistry()
    {
        AddBuiltIn(GscScope, "gsc.tmLanguage.json", ".gsc", ".csc", ".gsh");
        AddBuiltIn(CsvScope, "csv.tmLanguage.json", ".csv", ".tsv");
    }

    /// <summary>
    /// Adds the grammars of a VS Code extension.
    /// </summary>
    /// <param name="extension">The extension.</param>
    public void Add(CodeExtension extension)
    {
        ArgumentNullException.ThrowIfNull(extension);

        foreach (CodeExtensionGrammar grammar in extension.Grammars)
        {
            _grammars[grammar.ScopeName] = () => grammar.Content;

            foreach (CodeExtensionLanguage language in extension.Languages.Where(language => language.Id == grammar.Language))
            {
                foreach (string fileExtension in language.FileExtensions)
                {
                    _scopesByExtension[fileExtension] = grammar.ScopeName;
                }
            }
        }
    }

    /// <summary>
    /// Finds the grammar scope for a file name.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>The scope, or <see langword="null"/> when no grammar handles the file.</returns>
    public string? FindScope(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        string extension = Path.GetExtension(fileName);
        if (extension.Length == 0)
        {
            return null;
        }

        return _scopesByExtension.TryGetValue(extension, out string? scope) ? scope : _bundled.GetScopeByExtension(extension);
    }

    /// <inheritdoc/>
    public IRawGrammar GetGrammar(string scopeName)
    {
        if (!_grammars.TryGetValue(scopeName, out Func<string>? content))
        {
            return _bundled.GetGrammar(scopeName);
        }

        using StreamReader reader = new(new MemoryStream(Encoding.UTF8.GetBytes(content())));
        return GrammarReader.ReadGrammarSync(reader);
    }

    /// <inheritdoc/>
    public ICollection<string> GetInjections(string scopeName) => _bundled.GetInjections(scopeName);

    /// <inheritdoc/>
    public IRawTheme GetTheme(string scopeName) => _bundled.GetTheme(scopeName);

    /// <inheritdoc/>
    public IRawTheme GetDefaultTheme() => _bundled.GetDefaultTheme();

    private void AddBuiltIn(string scope, string resource, params string[] extensions)
    {
        _grammars[scope] = () => ReadResource(resource);

        foreach (string extension in extensions)
        {
            _scopesByExtension[extension] = scope;
        }
    }

    private static string ReadResource(string name)
    {
        string resource = $"RedFox.Avalonia.CodeEditor.Grammars.{name}";
        using Stream stream = typeof(CodeLanguageRegistry).Assembly.GetManifestResourceStream(resource) ?? throw new InvalidOperationException($"Missing resource '{resource}'.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
