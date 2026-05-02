using System.Text.Json;
using RedFox.GameExtraction;

namespace RedFox.GameExtraction.UI;

/// <summary>
/// Stores export settings for the GameExtraction UI shell.
/// </summary>
public sealed class GameExtractionSettings
{
    private const string VendorDirectoryName = "RedFox";
    private const string SettingsFileName = "settings.json";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Gets or sets the root directory used for exported assets.
    /// </summary>
    public string OutputDirectory { get; set; } = GetDefaultOutputDirectory();

    /// <summary>
    /// Gets or sets a value indicating whether existing export files should be overwritten.
    /// </summary>
    public bool Overwrite { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether referenced assets should be exported recursively.
    /// </summary>
    public bool ExportReferences { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether virtual asset directories should be preserved under the output root.
    /// </summary>
    public bool PreserveDirectoryStructure { get; set; } = true;

    /// <summary>
    /// Loads persisted settings into this instance when the file exists.
    /// </summary>
    /// <param name="path">The settings file path.</param>
    public void LoadFrom(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            return;
        }

        using FileStream stream = File.OpenRead(path);
        GameExtractionSettings? savedSettings = JsonSerializer.Deserialize<GameExtractionSettings>(stream, SerializerOptions);
        if (savedSettings is null)
        {
            return;
        }

        OutputDirectory = string.IsNullOrWhiteSpace(savedSettings.OutputDirectory)
            ? GetDefaultOutputDirectory()
            : savedSettings.OutputDirectory;
        Overwrite = savedSettings.Overwrite;
        ExportReferences = savedSettings.ExportReferences;
        PreserveDirectoryStructure = savedSettings.PreserveDirectoryStructure;
    }

    /// <summary>
    /// Saves this settings instance to disk.
    /// </summary>
    /// <param name="path">The settings file path.</param>
    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using FileStream stream = File.Create(path);
        JsonSerializer.Serialize(stream, this, SerializerOptions);
    }

    /// <summary>
    /// Creates an export configuration for the core asset manager.
    /// </summary>
    /// <returns>The export configuration represented by these settings.</returns>
    public ExportConfiguration ToExportConfiguration()
    {
        return new ExportConfiguration
        {
            OutputDirectory = string.IsNullOrWhiteSpace(OutputDirectory)
                ? GetDefaultOutputDirectory()
                : OutputDirectory,
            Overwrite = Overwrite,
            ExportReferences = ExportReferences,
            PreserveDirectoryStructure = PreserveDirectoryStructure,
        };
    }

    /// <summary>
    /// Resolves the default settings path for an application name.
    /// </summary>
    /// <param name="appName">The application name.</param>
    /// <returns>The default settings path.</returns>
    public static string GetDefaultSettingsPath(string appName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);

        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, VendorDirectoryName, appName, SettingsFileName);
    }

    /// <summary>
    /// Resolves the default export directory.
    /// </summary>
    /// <returns>The default export directory.</returns>
    public static string GetDefaultOutputDirectory()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documents, VendorDirectoryName, "Exports");
    }
}
