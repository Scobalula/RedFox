using CommunityToolkit.Mvvm.ComponentModel;
using RedFox.GameExtraction;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Rendering;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Preview preferences shared across assets and persisted with the application's settings.
/// </summary>
public sealed partial class PreviewSettings : ObservableObject
{
    private static readonly GameExtractionSetting UpAxisSetting = new()
    {
        Name = "PreviewUpAxis",
        Group = GameExtractionSettingGroup.Preview,
        Label = "Up axis",
        Type = GameExtractionSettingType.Choice,
        Options = [.. Enum.GetNames<SceneUpAxis>()],
        DefaultValue = SceneUpAxis.Y,
    };

    private static readonly GameExtractionSetting SkinningModeSetting = new()
    {
        Name = "PreviewSkinningMode",
        Group = GameExtractionSettingGroup.Preview,
        Label = "Skinning mode",
        Type = GameExtractionSettingType.Choice,
        Options = [.. Enum.GetNames<SkinningMode>()],
        DefaultValue = SkinningMode.Linear,
    };

    private static readonly GameExtractionSetting LightingModeSetting = new()
    {
        Name = "PreviewLightingMode",
        Group = GameExtractionSettingGroup.Preview,
        Label = "Lighting mode",
        Type = GameExtractionSettingType.Choice,
        Options = [.. Enum.GetNames<ScenePreviewLightingMode>()],
        DefaultValue = ScenePreviewLightingMode.Scene,
    };

    private static readonly GameExtractionSetting AutoFitSetting = new()
    {
        Name = "PreviewAutoFitScene",
        Group = GameExtractionSettingGroup.Preview,
        Label = "Auto fit scenes",
        Type = GameExtractionSettingType.Boolean,
        DefaultValue = true,
    };

    private static readonly GameExtractionSetting AutoPlayAudioSetting = new()
    {
        Name = "PreviewAutoPlayAudio",
        Group = GameExtractionSettingGroup.Preview,
        Label = "Auto-play audio",
        Type = GameExtractionSettingType.Boolean,
        DefaultValue = false,
    };

    /// <summary>
    /// Gets the optional setting definitions for the built-in preview preferences.
    /// Add these to an application's setting definitions to expose them in its settings UI.
    /// </summary>
    public static IReadOnlyList<GameExtractionSetting> SettingDefinitions { get; } =
    [
        UpAxisSetting,
        SkinningModeSetting,
        LightingModeSetting,
        AutoFitSetting,
        AutoPlayAudioSetting,
    ];

    internal ScenePreviewCameraState? CameraState { get; set; }

    /// <summary>
    /// Gets or sets the scene up axis.
    /// </summary>
    [ObservableProperty]
    public partial SceneUpAxis UpAxis { get; set; } = SceneUpAxis.Y;

    /// <summary>
    /// Gets or sets the renderer skinning mode.
    /// </summary>
    [ObservableProperty]
    public partial SkinningMode SkinningMode { get; set; } = SkinningMode.Linear;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UseViewBasedLighting))]
    internal partial ScenePreviewLightingMode LightingMode { get; set; }

    /// <summary>
    /// Gets a value indicating whether view-based lighting is enabled.
    /// </summary>
    public bool UseViewBasedLighting => LightingMode == ScenePreviewLightingMode.ViewBased;

    /// <summary>
    /// Gets or sets a value indicating whether animation playback is paused.
    /// </summary>
    [ObservableProperty]
    public partial bool IsAnimationPaused { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the ground grid is drawn.
    /// </summary>
    [ObservableProperty]
    public partial bool ShowGrid { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether skeleton bones are drawn in scene previews.
    /// </summary>
    [ObservableProperty]
    public partial bool ShowBones { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether scene changes automatically refit the preview camera.
    /// </summary>
    [ObservableProperty]
    public partial bool AutoFitScene { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether audio previews start playing as soon as they are shown.
    /// </summary>
    [ObservableProperty]
    public partial bool AutoPlayAudio { get; set; }

    internal void LoadFrom(GameExtractionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        UpAxis = GetEnumValue(settings, UpAxisSetting, SceneUpAxis.Y);
        SkinningMode = GetEnumValue(settings, SkinningModeSetting, SkinningMode.Linear);
        LightingMode = GetEnumValue(settings, LightingModeSetting, ScenePreviewLightingMode.Scene);
        AutoFitScene = bool.TryParse(settings.GetSettingValue(AutoFitSetting), out bool autoFit) ? autoFit : true;
        AutoPlayAudio = bool.TryParse(settings.GetSettingValue(AutoPlayAudioSetting), out bool autoPlay) && autoPlay;
    }

    private static TEnum GetEnumValue<TEnum>(GameExtractionSettings settings, GameExtractionSetting setting, TEnum fallback)
        where TEnum : struct, Enum
    {
        string? value = settings.GetSettingValue(setting);
        return Enum.TryParse(value, ignoreCase: true, out TEnum parsed) && Enum.IsDefined(parsed) ? parsed : fallback;
    }
}
