using System.ComponentModel;
using System.Reflection;
using System.Text.Json;

namespace RedFox.GameExtraction;

[AttributeUsage(AttributeTargets.Property)]
public class SettingCategoryAttribute(string category) : Attribute
{
    public string Category { get; } = category;
}

[AttributeUsage(AttributeTargets.Property)]
public class SettingDescriptionAttribute(string description) : Attribute
{
    public string Description { get; } = description;
}

[AttributeUsage(AttributeTargets.Property)]
public class SettingDisplayNameAttribute(string displayName) : Attribute
{
    public string DisplayName { get; } = displayName;
}

/// <summary>
/// Base class for extraction application settings.
/// </summary>
public abstract class SettingsBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    [SettingCategory("General")]
    [SettingDisplayName("Output Directory")]
    [SettingDescription("The default directory where exported files will be saved.")]
    public string OutputDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

    public void Save(string filePath)
    {
        var json = JsonSerializer.Serialize(this, GetType(), new JsonSerializerOptions
        {
            WriteIndented = true
        });
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(filePath, json);
    }

    public static T? Load<T>(string filePath) where T : SettingsBase
    {
        if (!File.Exists(filePath))
            return null;

        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<T>(json);
    }

    public void LoadFrom(string filePath)
    {
        if (!File.Exists(filePath))
            return;

        var json = File.ReadAllText(filePath);
        if (JsonSerializer.Deserialize(json, GetType()) is not SettingsBase loaded)
            return;

        foreach (var prop in GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.CanRead && prop.CanWrite)
                prop.SetValue(this, prop.GetValue(loaded));
        }
    }

    public static string GetDefaultSettingsPath(string appName)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "RedFox", appName, "settings.json");
    }
}