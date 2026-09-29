namespace RedFox.GameExtraction.Template;

/// <summary>
/// Provides the setting definitions, defaults, and export configuration mapping shared by the template frontends.
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
            Group = "Export",
            Label = "Output directory",
            Type = GameExtractionSettingType.DirectoryPath,
            DefaultValue = GameExtractionSettings.GetDefaultOutputDirectory(),
        },
        new GameExtractionSetting
        {
            Name = "Overwrite",
            Group = "Export",
            Label = "Overwrite existing files",
            Type = GameExtractionSettingType.Boolean,
            DefaultValue = false,
        },
        new GameExtractionSetting
        {
            Name = "PreserveDirectoryStructure",
            Group = "Export",
            Label = "Preserve directory structure",
            Type = GameExtractionSettingType.Boolean,
            DefaultValue = true,
        },
        new GameExtractionSetting
        {
            Name = "ExportReferences",
            Group = "Export",
            Label = "Export referenced assets",
            Type = GameExtractionSettingType.Boolean,
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
    /// Builds an export configuration from persisted setting values.
    /// </summary>
    /// <param name="settings">The persisted settings.</param>
    /// <returns>The export configuration.</returns>
    public static ExportConfiguration CreateExportConfiguration(GameExtractionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string? outputDirectory = settings.Values.TryGetValue("OutputDirectory", out string? configuredOutputDirectory) ? configuredOutputDirectory : null;

        return new ExportConfiguration
        {
            OutputDirectory = string.IsNullOrWhiteSpace(outputDirectory) ? GameExtractionSettings.GetDefaultOutputDirectory() : outputDirectory,
            Overwrite = settings.Values.TryGetValue("Overwrite", out string? overwriteValue) && bool.TryParse(overwriteValue, out bool overwrite) && overwrite,
            ExportReferences = settings.Values.TryGetValue("ExportReferences", out string? exportReferencesValue) && bool.TryParse(exportReferencesValue, out bool exportReferences) && exportReferences,
            PreserveDirectoryStructure = !settings.Values.TryGetValue("PreserveDirectoryStructure", out string? preserveDirectoryStructureValue) || (bool.TryParse(preserveDirectoryStructureValue, out bool preserveDirectoryStructure) && preserveDirectoryStructure),
        };
    }
}
