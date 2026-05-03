using System.Text.Json;

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
    /// Gets or sets persisted setting values keyed by <see cref="GameExtractionSetting.Name"/>.
    /// </summary>
    public Dictionary<string, string?> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);

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

        Values = savedSettings.Values is null
            ? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string?>(savedSettings.Values, StringComparer.OrdinalIgnoreCase);
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

    internal string? GetSettingValue(GameExtractionSetting setting)
    {
        ArgumentNullException.ThrowIfNull(setting);

        return Values.TryGetValue(setting.Name, out string? value)
            ? value
            : setting.DefaultValue?.ToString();
    }

    internal void SetSettingValue(GameExtractionSetting setting, string? value)
    {
        ArgumentNullException.ThrowIfNull(setting);

        if (value is null)
        {
            Values.Remove(setting.Name);
            return;
        }

        Values[setting.Name] = value;
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
