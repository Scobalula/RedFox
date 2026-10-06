using RedFox.GameExtraction;

namespace ToolName;

/// <summary>
/// Provides the setting definitions, defaults, and shared configuration mapping for the ToolName frontends.
/// </summary>
public static class ToolNameSettings
{
    /// <summary>
    /// Gets the settings exposed by the ToolName frontends.
    /// </summary>
    public static IReadOnlyList<GameExtractionSetting> Definitions { get; } =
    [
        new GameExtractionSetting
        {
            Name = "OutputDirectory",
            Group = GameExtractionSettingGroup.Export,
            Label = "Output directory",
            Type = GameExtractionSettingType.DirectoryPath,
            DefaultValue = GameExtractionSettings.GetDefaultOutputDirectory("ToolName"),
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
            Name = "ReadRawAssets",
            Group = GameExtractionSettingGroup.Export,
            Label = "Read and export raw assets",
            Type = GameExtractionSettingType.Boolean,
            Description = "Read assets as-is from the source. This setting also affects the previewer.",
            DefaultValue = true,
        },
        GameExtractionLogging.VerboseSetting,
#if MODEL_SUPPORT
        new GameExtractionSetting
        {
            Name = "ModelSupport",
            Group = GameExtractionSettingGroup.Model,
            Label = "Enable model support",
            Type = GameExtractionSettingType.Boolean,
            DefaultValue = true,
        },
        new GameExtractionSetting
        {
            Name = "ModelFormats",
            Group = GameExtractionSettingGroup.Model,
            Label = "Model export formats",
            Description = "Comma-separated list of model extensions.",
            Type = GameExtractionSettingType.TextArray,
            DefaultValue = ".semodel,.cast,.fbx",
        },
        new GameExtractionSetting
        {
            Name = "ExportModelImages",
            Group = GameExtractionSettingGroup.Model,
            Label = "Export model images",
            Type = GameExtractionSettingType.Boolean,
            DefaultValue = true,
        },
#endif
#if IMAGE_SUPPORT
        new GameExtractionSetting
        {
            Name = "ImageSupport",
            Group = GameExtractionSettingGroup.Image,
            Label = "Enable image support",
            Type = GameExtractionSettingType.Boolean,
            DefaultValue = true,
        },
        new GameExtractionSetting
        {
            Name = "ImageFormats",
            Group = GameExtractionSettingGroup.Image,
            Label = "Image export formats",
            Description = "Comma-separated list of image extensions.",
            Type = GameExtractionSettingType.TextArray,
            DefaultValue = ".png",
        },
        new GameExtractionSetting
        {
            Name = "SkipExistingImages",
            Group = GameExtractionSettingGroup.Image,
            Label = "Skip images that already exist",
            Type = GameExtractionSettingType.Boolean,
            DefaultValue = true,
        },
#endif
#if AUDIO_SUPPORT
        new GameExtractionSetting
        {
            Name = "AudioSupport",
            Group = GameExtractionSettingGroup.Sound,
            Label = "Enable audio support",
            Type = GameExtractionSettingType.Boolean,
            DefaultValue = true,
        },
        new GameExtractionSetting
        {
            Name = "AudioFormats",
            Group = GameExtractionSettingGroup.Sound,
            Label = "Audio export formats",
            Description = "Comma-separated list of audio extensions.",
            Type = GameExtractionSettingType.TextArray,
            DefaultValue = ".wav",
        },
        new GameExtractionSetting
        {
            Name = "SkipExistingAudio",
            Group = GameExtractionSettingGroup.Sound,
            Label = "Skip audio that already exists",
            Type = GameExtractionSettingType.Boolean,
            DefaultValue = true,
        },
#endif
    ];

    /// <summary>
    /// Creates the default persisted setting values from the setting definitions.
    /// </summary>
    /// <returns>The default settings.</returns>
    public static GameExtractionSettings CreateDefaults()
    {
        GameExtractionSettings settings = new();

        foreach (GameExtractionSetting definition in Definitions)
        {
            settings.SetSettingValue(definition, definition.DefaultValue?.ToString());
        }

        return settings;
    }
}
