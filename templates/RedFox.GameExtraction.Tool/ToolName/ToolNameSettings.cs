using RedFox.GameExtraction;
using RedFox.Graphics3D.Formats.KaydaraFbx;
using RedFox.Graphics3D.IO;
using RedFox.Imaging.IO;

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
            Description = "Formats models are exported to.",
            Type = GameExtractionSettingType.TextArray,
            DefaultValue = ".semodel,.cast,.fbx",
            OptionsSource = new SceneFormatOptionsSource(SceneTranslatorContent.Model),
        },
        new GameExtractionSetting
        {
            Name = FbxTranslator.WriteAsciiOption,
            Group = GameExtractionSettingGroup.Model,
            Label = "Write FBX as ASCII",
            Type = GameExtractionSettingType.Boolean,
            DefaultValue = false,
        },
        new GameExtractionSetting
        {
            Name = "ExportModelImages",
            Group = GameExtractionSettingGroup.Model,
            Label = "Export model images",
            Type = GameExtractionSettingType.Boolean,
            DefaultValue = true,
        },
        new GameExtractionSetting
        {
            Name = "AnimationFormats",
            Group = GameExtractionSettingGroup.Animation,
            Label = "Animation export formats",
            Description = "Formats animations are exported to.",
            Type = GameExtractionSettingType.TextArray,
            DefaultValue = ".seanim,.cast",
            OptionsSource = new SceneFormatOptionsSource(SceneTranslatorContent.Animation),
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
            Description = "Formats images are exported to.",
            Type = GameExtractionSettingType.TextArray,
            DefaultValue = ".png",
            OptionsSource = new ImageFormatOptionsSource(),
        },
        new GameExtractionSetting
        {
            Name = "ImageQuality",
            Group = GameExtractionSettingGroup.Image,
            Label = "Image quality",
            Description = "Quality used by lossy formats such as JPEG.",
            Type = GameExtractionSettingType.Slider,
            Minimum = 1,
            Maximum = 100,
            DefaultValue = 90,
        },
        new GameExtractionSetting
        {
            Name = "ImageCompression",
            Group = GameExtractionSettingGroup.Image,
            Label = "Image compression",
            Description = "Compression preference for formats that support it, such as PNG, TIFF and EXR.",
            Type = GameExtractionSettingType.Choice,
            DefaultValue = nameof(ImageCompressionPreference.Default),
            Options = Enum.GetNames<ImageCompressionPreference>(),
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
