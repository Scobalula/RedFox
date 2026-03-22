using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedFox.GameExtraction;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// A single setting item discovered by reflection for rendering in the settings UI.
/// </summary>
public class SettingItem : ObservableObject
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public required string Category { get; init; }
    public string Description { get; init; } = string.Empty;
    public required Type PropertyType { get; init; }
    public required PropertyInfo Property { get; init; }
    public required SettingsBase SettingsInstance { get; init; }

    /// <summary>Whether this is a boolean property (renders as CheckBox).</summary>
    public bool IsBool => PropertyType == typeof(bool);

    /// <summary>Whether this is an enum property (renders as ComboBox).</summary>
    public bool IsEnum => PropertyType.IsEnum;

    /// <summary>Whether this is a text/numeric property (renders as TextBox).</summary>
    public bool IsText => !IsBool && !IsEnum;

    /// <summary>For bool settings — wraps the underlying value.</summary>
    public bool BoolValue
    {
        get => Property.GetValue(SettingsInstance) is true;
        set
        {
            Property.SetValue(SettingsInstance, value);
            OnPropertyChanged();
        }
    }

    /// <summary>For text/numeric settings — wraps the underlying value as string.</summary>
    public string TextValue
    {
        get => Property.GetValue(SettingsInstance)?.ToString() ?? string.Empty;
        set
        {
            var converted = ConvertValue(value, PropertyType);
            if (converted is not null)
                Property.SetValue(SettingsInstance, converted);
            OnPropertyChanged();
        }
    }

    /// <summary>For enum settings — the selected value.</summary>
    public object? EnumSelectedValue
    {
        get => Property.GetValue(SettingsInstance);
        set
        {
            if (value is not null)
                Property.SetValue(SettingsInstance, value);
            OnPropertyChanged();
        }
    }

    /// <summary>For enum types, the possible values.</summary>
    public Array? EnumValues => PropertyType.IsEnum ? Enum.GetValues(PropertyType) : null;

    private static object? ConvertValue(string text, Type target)
    {
        try
        {
            if (target == typeof(string)) return text;
            if (target == typeof(int) && int.TryParse(text, out var i)) return i;
            if (target == typeof(long) && long.TryParse(text, out var l)) return l;
            if (target == typeof(double) && double.TryParse(text, out var d)) return d;
            if (target == typeof(float) && float.TryParse(text, out var f)) return f;
            return Convert.ChangeType(text, target);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// A group of settings under a category heading.
/// </summary>
public class SettingGroup
{
    public required string Category { get; init; }
    public ObservableCollection<SettingItem> Items { get; init; } = [];
}

public partial class SettingsWindowViewModel : ObservableObject
{
    private readonly SettingsBase _settings;
    private readonly string _appName;

    public SettingsWindowViewModel(SettingsBase settings, string appName)
    {
        _settings = settings;
        _appName = appName;
        DiscoverSettings();
    }

    public ObservableCollection<SettingGroup> Groups { get; } = [];

    [RelayCommand]
    private void Save()
    {
        var path = SettingsBase.GetDefaultSettingsPath(_appName);
        _settings.Save(path);
        SaveRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        CancelRequested?.Invoke();
    }

    public event Action? SaveRequested;
    public event Action? CancelRequested;

    private void DiscoverSettings()
    {
        var type = _settings.GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite);

        var groups = new Dictionary<string, SettingGroup>();

        foreach (var prop in properties)
        {
            // Skip inherited properties from SettingsBase itself
            if (prop.DeclaringType == typeof(SettingsBase)) continue;

            var category = prop.GetCustomAttribute<SettingCategoryAttribute>()?.Category ?? "General";
            var displayName = prop.GetCustomAttribute<SettingDisplayNameAttribute>()?.DisplayName
                ?? AddSpacesToPascalCase(prop.Name);
            var description = prop.GetCustomAttribute<SettingDescriptionAttribute>()?.Description ?? string.Empty;

            if (!groups.ContainsKey(category))
            {
                groups[category] = new SettingGroup { Category = category };
            }

            groups[category].Items.Add(new SettingItem
            {
                Name = prop.Name,
                DisplayName = displayName,
                Category = category,
                Description = description,
                PropertyType = prop.PropertyType,
                Property = prop,
                SettingsInstance = _settings
            });
        }

        foreach (var group in groups.Values.OrderBy(g => g.Category))
            Groups.Add(group);
    }

    private static string AddSpacesToPascalCase(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var result = new System.Text.StringBuilder();
        result.Append(text[0]);
        for (int i = 1; i < text.Length; i++)
        {
            if (char.IsUpper(text[i]) && !char.IsUpper(text[i - 1]))
                result.Append(' ');
            result.Append(text[i]);
        }
        return result.ToString();
    }
}
