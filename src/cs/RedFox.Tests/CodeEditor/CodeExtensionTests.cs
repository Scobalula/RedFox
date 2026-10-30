using System.IO.Compression;
using RedFox.Avalonia.CodeEditor;

namespace RedFox.Tests.CodeEditor;

public sealed class CodeExtensionTests : IDisposable
{
    private const string Manifest = """
        {
          // Manifests can carry comments.
          "contributes": {
            "languages": [ { "id": "script", "extensions": [ ".scr" ] } ],
            "grammars": [
              { "language": "script", "scopeName": "source.script", "path": "./syntaxes/script.json" },
              { "scopeName": "source.injected", "path": "./syntaxes/injected.json", "injectTo": [ "source.script" ] }
            ],
          }
        }
        """;

    private const string Grammar = """{ "scopeName": "source.script", "patterns": [ { "name": "keyword.control.script", "match": "\\bgo\\b" } ] }""";

    private readonly string _root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public CodeExtensionTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "syntaxes"));
        File.WriteAllText(Path.Combine(_root, "package.json"), Manifest);
        File.WriteAllText(Path.Combine(_root, "syntaxes", "script.json"), Grammar);
    }

    public void Dispose()
    {
        Directory.Delete(_root, true);
    }

    [Fact]
    public void LoadsAnExtensionFolder()
    {
        AssertContributions(CodeExtension.Load(_root));
    }

    [Fact]
    public void LoadsAVsixPackage()
    {
        string package = Path.Combine(_root, "script.vsix");
        using (ZipArchive archive = ZipFile.Open(package, ZipArchiveMode.Create))
        {
            archive.CreateEntryFromFile(Path.Combine(_root, "package.json"), "extension/package.json");
            archive.CreateEntryFromFile(Path.Combine(_root, "syntaxes", "script.json"), "extension/syntaxes/script.json");
        }

        AssertContributions(CodeExtension.Load(package));
    }

    [Fact]
    public void ARegisteredExtensionHandlesItsFiles()
    {
        CodeLanguageRegistry registry = new();
        registry.Add(CodeExtension.Load(_root));

        Assert.Equal("source.script", registry.FindScope("a/b.SCR"));
        Assert.Equal("source.script", registry.GetGrammar("source.script").GetScopeName());
    }

    [Fact]
    public void AGrammarOutsideTheExtensionIsRefused()
    {
        File.WriteAllText(Path.Combine(_root, "package.json"), Manifest.Replace("./syntaxes/script.json", "../outside.json"));

        Assert.Throws<InvalidDataException>(() => CodeExtension.Load(_root));
    }

    [Fact]
    public void APlistGrammarIsRefused()
    {
        File.WriteAllText(Path.Combine(_root, "package.json"), Manifest.Replace("./syntaxes/script.json", "./syntaxes/script.tmLanguage"));

        Assert.Throws<InvalidDataException>(() => CodeExtension.Load(_root));
    }

    private static void AssertContributions(CodeExtension extension)
    {
        CodeExtensionLanguage language = Assert.Single(extension.Languages);
        CodeExtensionGrammar grammar = Assert.Single(extension.Grammars);

        Assert.Equal("script", language.Id);
        Assert.Equal([".scr"], language.FileExtensions);
        Assert.Equal("source.script", grammar.ScopeName);
        Assert.Equal(Grammar, grammar.Content);
    }
}
