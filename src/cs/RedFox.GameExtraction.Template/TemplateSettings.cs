namespace RedFox.GameExtraction.Template;

/// <summary>
/// Provides the setting definitions, defaults, and shared configuration mapping for the template frontends.
/// </summary>
public static class TemplateSettings
{
    /// <summary>
    /// Gets the settings exposed by the template frontends.
    /// </summary>
    public static IReadOnlyList<GameExtractionSetting> Definitions { get; } =
    [
        new GameExtractionSetting
        {
            Name = "OutputDirectory",
            Group = GameExtractionSettingGroup.Export,
            Label = "Output directory",
            Type = GameExtractionSettingType.DirectoryPath,
            DefaultValue = GameExtractionSettings.GetDefaultOutputDirectory(),
        },
        new GameExtractionSetting
        {
            Name = "Overwrite",
            Group = GameExtractionSettingGroup.Export,
            Label = "Overwrite existing files",
            Type = GameExtractionSettingType.Boolean,
            DefaultValue = false,
        },
        new GameExtractionSetting
        {
            Name = "PreserveDirectoryStructure",
            Group = GameExtractionSettingGroup.Export,
            Label = "Preserve directory structure",
            Type = GameExtractionSettingType.Boolean,
            DefaultValue = true,
        },
        new GameExtractionSetting
        {
            Name = "ExportReferences",
            Group = GameExtractionSettingGroup.Export,
            Label = "Export referenced assets",
            Type = GameExtractionSettingType.Boolean,
            DefaultValue = false,
        },
        new GameExtractionSetting
        {
            Name = "ExportReferences",
            Group = GameExtractionSettingGroup.Model,
            Label = "Well boss man how she cuttin",
            Type = GameExtractionSettingType.Text,
            DefaultValue = false,
        },
    ];

    /// <summary>
    /// Creates the default persisted setting values.
    /// </summary>
    /// <returns>The default settings.</returns>
    public static GameExtractionSettings CreateDefaults() => new()
    {
        Values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["OutputDirectory"] = GameExtractionSettings.GetDefaultOutputDirectory(),
            ["Overwrite"] = bool.FalseString,
            ["ExportReferences"] = bool.FalseString,
            ["PreserveDirectoryStructure"] = bool.TrueString,
        },
    };

    /// <summary>
    /// Builds the shared handler configuration from persisted setting values.
    /// </summary>
    /// <param name="settings">The persisted settings.</param>
    /// <returns>The shared handler configuration.</returns>
    public static GameExtractionConfiguration CreateConfiguration(GameExtractionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        GameExtractionConfiguration configuration = new();
        configuration.ApplySettings(settings, Definitions);
        return configuration;
    }
}
