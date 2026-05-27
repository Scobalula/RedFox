// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using RedFox.Graphics3D;
using RedFox.Graphics3D.IO;
using RedFox.Plugins;
using RedFox.Plugins.Python;

namespace RedFox.Samples.Examples;

/// <summary>
/// Demonstrates loading a Python plugin that registers a custom <see cref="SceneTranslator"/> via the
/// shared <see cref="SceneTranslatorManager"/>.
/// </summary>
internal sealed class PythonPluginSample : ISample
{
    /// <inheritdoc />
    public string Name => "python-plugin";

    /// <inheritdoc />
    public string Description => "Loads a Python plugin that registers a custom Graphics3D scene translator.";

    /// <inheritdoc />
    public int Run(string[] arguments) => Run(arguments, scriptPath: null);

    private static int Run(string[] arguments, string? scriptPath)
    {
        string outputDirectory = arguments.Length > 0
            ? Path.GetFullPath(arguments[0])
            : Path.Combine(Environment.CurrentDirectory, "artifacts", "python-plugin");

        Directory.CreateDirectory(outputDirectory);

        scriptPath ??= Path.Combine(AppContext.BaseDirectory, "Examples", "Plugins", "sample_translator.py");
        if (!File.Exists(scriptPath))
        {
            Console.Error.WriteLine($"Plugin script not found at '{scriptPath}'.");
            return 1;
        }

        SceneTranslatorManager translators = new();

        using PluginManager plugins = new();
        plugins.Services["scene-translators"] = translators;

        Console.WriteLine($"Loading Python plugin: {scriptPath}");
        Plugin plugin;
        try
        {
            plugins.RegisterHost(new PythonPluginHost());
            plugin = plugins.Load(scriptPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Plugin load failed: {ex.InnerException?.Message ?? ex.Message}");
            Console.Error.WriteLine("Ensure Python 3 is installed and the PYTHONNET_PYDLL environment variable points at python3X.dll (e.g. C:\\Python312\\python312.dll).");
            return 1;
        }

        Scene scene = new("PluginSampleScene");
        scene.RootNode.AddNode<Group>("Hips");
        scene.RootNode.AddNode<Group>("Spine");
        scene.RootNode.AddNode<Group>("Head");

        string outputPath = Path.Combine(outputDirectory, "scene.pytxt");
        translators.Write(outputPath, scene, new SceneTranslatorOptions(), CancellationToken.None);
        Console.WriteLine($"Wrote scene via Python translator -> {outputPath} ({new FileInfo(outputPath).Length} bytes)");

        Console.WriteLine("Reloading plugin...");
        try
        {
            plugins.Reload(plugin.Name);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Reload failed: {ex.InnerException?.Message ?? ex.Message}");
            return 1;
        }
        translators.Write(outputPath, scene, new SceneTranslatorOptions(), CancellationToken.None);
        Console.WriteLine("Reload OK, wrote scene again.");

        Console.WriteLine("Unloading plugin...");
        try
        {
            plugins.Unload(plugin.Name);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unload failed: {ex.InnerException?.Message ?? ex.Message}");
            return 1;
        }
        Console.WriteLine($"Translators after unload: {translators.Translators.Count}");

        return 0;
    }
}
