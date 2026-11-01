using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace RedFox.GameExtraction;

/// <summary>
/// Stores persisted setting values shared by GameExtraction frontends.
/// </summary>
public sealed class GameExtractionSettings
{
    private const string SettingsFileName		= "settings.json";
    private const string DataFolderFileName		= "datafolder.txt";
    private const string ProfileFileName		= "profile.txt";
    private const string ProfilesDirectoryName	= "profiles";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Gets the name of the built-in profile stored in the data directory's settings file.
    /// </summary>
    public const string DefaultProfileName = "Default";

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

    /// <summary>
    /// Gets the persisted value for a setting, falling back to its default value.
    /// </summary>
    /// <param name="setting">The setting definition.</param>
    /// <returns>The persisted or default value.</returns>
    public string? GetSettingValue(GameExtractionSetting setting)
    {
        ArgumentNullException.ThrowIfNull(setting);

        return Values.TryGetValue(setting.Name, out string? value)
            ? value
            : setting.DefaultValue?.ToString();
    }

    /// <summary>
    /// Sets the persisted value for a setting, removing it when the value is <see langword="null"/>.
    /// </summary>
    /// <param name="setting">The setting definition.</param>
    /// <param name="value">The value to persist.</param>
    public void SetSettingValue(GameExtractionSetting setting, string? value)
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

        return GetProfileSettingsPath(appName, GetActiveProfile(appName));
    }

    /// <summary>
    /// Resolves the default data directory for an application name.
    /// </summary>
    /// <param name="appName">The application name.</param>
    /// <returns>The default data directory.</returns>
    public static string GetDefaultDataDirectory(string appName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName);
    }

    /// <summary>
    /// Resolves the data directory for an application name, honouring a folder path written to datafolder.txt next to the executable or in the default data directory. Relative paths resolve from the executable folder.
    /// </summary>
    /// <param name="appName">The application name.</param>
    /// <returns>The data directory.</returns>
    public static string GetDataDirectory(string appName)
    {
        string defaultDirectory	= GetDefaultDataDirectory(appName);
        string redirectPath		= Path.Combine(AppContext.BaseDirectory, DataFolderFileName);

        if (!File.Exists(redirectPath))
        {
            redirectPath = Path.Combine(defaultDirectory, DataFolderFileName);
        }

        if (!File.Exists(redirectPath))
        {
            return defaultDirectory;
        }

        string directory = File.ReadAllText(redirectPath).Trim();
        return string.IsNullOrWhiteSpace(directory) ? defaultDirectory : Path.GetFullPath(directory, AppContext.BaseDirectory);
    }

    /// <summary>
    /// Resolves the settings path of a profile.
    /// </summary>
    /// <param name="appName">The application name.</param>
    /// <param name="profile">The profile name.</param>
    /// <returns>The profile settings path.</returns>
    public static string GetProfileSettingsPath(string appName, string profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profile);

        if (profile.Equals(DefaultProfileName, StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(GetDataDirectory(appName), SettingsFileName);
        }

        return Path.Combine(GetDataDirectory(appName), ProfilesDirectoryName, profile + ".json");
    }

    /// <summary>
    /// Gets the names of the profiles of an application name, starting with <see cref="DefaultProfileName"/>.
    /// </summary>
    /// <param name="appName">The application name.</param>
    /// <returns>The profile names.</returns>
    public static IReadOnlyList<string> GetProfileNames(string appName)
    {
        string directory = Path.Combine(GetDataDirectory(appName), ProfilesDirectoryName);

        if (!Directory.Exists(directory))
        {
            return [DefaultProfileName];
        }

        return [DefaultProfileName, .. Directory.GetFiles(directory, "*.json").Select(Path.GetFileNameWithoutExtension).OfType<string>().Order(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// Gets the name of the active profile of an application name.
    /// </summary>
    /// <param name="appName">The application name.</param>
    /// <returns>The active profile name, or <see cref="DefaultProfileName"/> when none is set or it no longer exists.</returns>
    public static string GetActiveProfile(string appName)
    {
        string path = Path.Combine(GetDataDirectory(appName), ProfileFileName);

        if (!File.Exists(path))
        {
            return DefaultProfileName;
        }

        string profile = File.ReadAllText(path).Trim();

        if (!IsValidProfileName(profile) || !File.Exists(GetProfileSettingsPath(appName, profile)))
        {
            return DefaultProfileName;
        }

        return profile;
    }

    /// <summary>
    /// Sets the active profile of an application name.
    /// </summary>
    /// <param name="appName">The application name.</param>
    /// <param name="profile">The profile name.</param>
    public static void SetActiveProfile(string appName, string profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profile);

        string directory = GetDataDirectory(appName);

        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, ProfileFileName), profile);
    }

    /// <summary>
    /// Deletes a profile of an application name. The <see cref="DefaultProfileName"/> profile cannot be deleted.
    /// </summary>
    /// <param name="appName">The application name.</param>
    /// <param name="profile">The profile name.</param>
    public static void DeleteProfile(string appName, string profile)
    {
        if (profile.Equals(DefaultProfileName, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The default profile cannot be deleted.", nameof(profile));
        }

        File.Delete(GetProfileSettingsPath(appName, profile));
    }

    /// <summary>
    /// Determines whether a profile name can be used as a file name.
    /// </summary>
    /// <param name="profile">The profile name.</param>
    /// <returns><see langword="true"/> when the name is usable; otherwise <see langword="false"/>.</returns>
    public static bool IsValidProfileName(string profile)
    {
        return !string.IsNullOrWhiteSpace(profile) && profile == profile.Trim() && profile.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && profile != "." && profile != "..";
    }

    /// <summary>
    /// Resolves the default export directory.
    /// </summary>
    /// <returns>The default export directory.</returns>
    public static string GetDefaultOutputDirectory() => "Exports";

    /// <summary>
    /// Resolves the default export directory.
    /// </summary>
    /// <returns>The default export directory.</returns>
    public static string GetDefaultOutputDirectory(string appName)
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documents, appName, "Exports");
    }
}
