using System.Diagnostics.CodeAnalysis;

namespace RedFox.GameExtraction;

/// <summary>
/// Carries application settings and operation options to asset handlers.
/// </summary>
public sealed class GameExtractionConfiguration
{
    /// <summary>
    /// Gets all setting values and additional options handlers can interpret as needed.
    /// </summary>
    public Dictionary<string, object?> Options { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Sets a setting value or additional option.
    /// </summary>
    /// <param name="key">The setting or option name.</param>
    /// <param name="value">The value to store.</param>
    public void SetOption(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Options[key] = value;
    }

    /// <summary>
    /// Loads all declared setting values into the configuration.
    /// </summary>
    /// <param name="settings">The persisted settings.</param>
    /// <param name="settingDefinitions">The definitions used to apply defaults and value types.</param>
    public void ApplySettings(GameExtractionSettings settings, IEnumerable<GameExtractionSetting> settingDefinitions)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(settingDefinitions);

        foreach (GameExtractionSetting setting in settingDefinitions.DistinctBy(setting => setting.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (Options.ContainsKey(setting.Name))
            {
                continue;
            }

            string? value = settings.GetSettingValue(setting);
            if (value is null)
            {
                continue;
            }

            object typedValue = setting.Type switch
            {
                GameExtractionSettingType.Boolean when bool.TryParse(value, out bool booleanValue) => booleanValue,
                GameExtractionSettingType.TextArray => value.Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                _ => value,
            };
            SetOption(setting.Name, typedValue);
        }

        foreach ((string name, string? value) in settings.Values)
        {
            if (value is not null && !Options.ContainsKey(name))
            {
                SetOption(name, value);
            }
        }

    }

    /// <summary>
    /// Attempts to get a setting or option by key.
    /// </summary>
    /// <param name="key">The key/name of the setting or option.</param>
    /// <param name="value">The value of the setting or option.</param>
    /// <returns><see langword="true"/> if the setting or option was found; otherwise <see langword="false"/>.</returns>
    public bool TryGetOption(string key, [NotNullWhen(true)] out object? value)
    {
        value = null;
        return Options.TryGetValue(key, out value);
    }

    /// <summary>
    /// Attempts to get a setting or option by key.
    /// </summary>
    /// <typeparam name="T">The type of the setting or option.</typeparam>
    /// <param name="key">The key/name of the setting or option.</param>
    /// <param name="value">The value of the setting or option.</param>
    /// <returns><see langword="true"/> if the setting or option was found; otherwise <see langword="false"/>.</returns>
    public bool TryGetOption<T>(string key, [NotNullWhen(true)] out T? value)
    {
        value = default;
        if (Options.TryGetValue(key, out object? objValue) && objValue is T tValue)
        {
            value = tValue;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets a setting or option by key.
    /// </summary>
    /// <param name="key">The key/name of the setting or option.</param>
    /// <returns>The value of the setting or option.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the setting or option with the specified key is not found.</exception>
    public object GetOption(string key)
    {
        if (Options.TryGetValue(key, out object? value) && value is not null)
        {
            return value;
        }
        throw new KeyNotFoundException($"Setting or option with key '{key}' not found.");
    }

    /// <summary>
    /// Gets a setting or option by key.
    /// </summary>
    /// <typeparam name="T">The type of the setting or option.</typeparam>
    /// <param name="key">The key/name of the setting or option.</param>
    /// <returns>The value of the setting or option.</returns>
    /// <exception cref="InvalidCastException">Thrown when the setting or option with the specified key is not of the expected type.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the setting or option with the specified key is not found.</exception>
    public T GetOption<T>(string key)
    {
        if (Options.TryGetValue(key, out object? value))
        {
            if (value is T tValue)
            {
                return tValue;
            }
            throw new InvalidCastException($"Setting or option with key '{key}' is not of type {typeof(T).FullName}.");
        }
        throw new KeyNotFoundException($"Setting or option with key '{key}' not found.");
    }

    /// <summary>
    /// Gets a setting or option by key.
    /// </summary>
    /// <param name="key">The key/name of the setting or option.</param>
    /// <param name="defaultValue">The default value to return if the setting or option is not found.</param>
    /// <returns>The value of the setting or option.</returns>
    public object GetOption(string key, object defaultValue)
    {
        if (Options.TryGetValue(key, out object? value) && value is not null)
        {
            return value;
        }
        return defaultValue;
    }

    /// <summary>
    /// Gets a setting or option by key.
    /// </summary>
    /// <typeparam name="T">The type of the setting or option.</typeparam>
    /// <param name="key">The key/name of the setting or option.</param>
    /// <param name="defaultValue">The default value to return if the setting or option is not found.</param>
    /// <returns>The value of the setting or option.</returns>
    /// <exception cref="InvalidCastException">Thrown when the setting or option with the specified key is not of the expected type.</exception>
    public T GetOption<T>(string key, T defaultValue)
    {
        if (Options.TryGetValue(key, out object? value))
        {
            if (value is T tValue)
            {
                return tValue;
            }
            throw new InvalidCastException($"Setting or option with key '{key}' is not of type {typeof(T).FullName}.");
        }
        return defaultValue;
    }
}

