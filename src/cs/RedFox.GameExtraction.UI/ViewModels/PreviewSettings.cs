using System.Globalization;
using System.Numerics;
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
        Section = "Scene",
        Label = "Up axis",
        Type = GameExtractionSettingType.Choice,
        Options = [.. Enum.GetNames<SceneUpAxis>()],
        DefaultValue = SceneUpAxis.Y,
    };

    private static readonly GameExtractionSetting ModelFrontSetting = new()
    {
        Name = "PreviewModelFront",
        Group = GameExtractionSettingGroup.Preview,
        Section = "Scene",
        Label = "Model front",
        Description = "The axis models face, so previews and thumbnails show them from the front.",
        Type = GameExtractionSettingType.Choice,
        Options = ["Positive X", "Negative X", "Positive Y", "Negative Y", "Positive Z", "Negative Z"],
        DefaultValue = "Positive Z",
    };

    private static readonly GameExtractionSetting SkinningModeSetting = new()
    {
        Name = "PreviewSkinningMode",
        Group = GameExtractionSettingGroup.Preview,
        Section = "Scene",
        Label = "Skinning mode",
        Type = GameExtractionSettingType.Choice,
        Options = [.. Enum.GetNames<SkinningMode>()],
        DefaultValue = SkinningMode.Linear,
    };

    private static readonly GameExtractionSetting LightingModeSetting = new()
    {
        Name = "PreviewLightingMode",
        Group = GameExtractionSettingGroup.Preview,
        Section = "Rendering",
        Label = "Lighting mode",
        Type = GameExtractionSettingType.Choice,
        Options = [.. Enum.GetNames<ScenePreviewLightingMode>()],
        DefaultValue = ScenePreviewLightingMode.Scene,
    };

    private static readonly GameExtractionSetting AntiAliasingSetting = new()
    {
        Name = "PreviewAntiAliasing",
        Group = GameExtractionSettingGroup.Preview,
        Section = "Rendering",
        Label = "Anti-aliasing",
        Type = GameExtractionSettingType.Choice,
        Options = ["Off", "2x", "4x", "8x"],
        DefaultValue = "8x",
    };

    private static readonly GameExtractionSetting ShadowQualitySetting = new()
    {
        Name = "PreviewShadowQuality",
        Group = GameExtractionSettingGroup.Preview,
        Section = "Shadows",
        Label = "Shadow quality",
        Type = GameExtractionSettingType.Choice,
        Options = ["Off", "Ultra Low", "Low", "Medium", "High", "Ultra High"],
        DefaultValue = ShadowQuality.High,
    };

    private static readonly GameExtractionSetting ShadowSoftnessSetting = new()
    {
        Name = "PreviewShadowSoftness",
        Group = GameExtractionSettingGroup.Preview,
        Section = "Shadows",
        Label = "Shadow softness",
        Type = GameExtractionSettingType.Slider,
        DefaultValue = 1.0,
        Minimum = 0.0,
        Maximum = 3.0,
    };

    private static readonly GameExtractionSetting ShadowStrengthSetting = new()
    {
        Name = "PreviewShadowStrength",
        Group = GameExtractionSettingGroup.Preview,
        Section = "Shadows",
        Label = "Shadow strength",
        Type = GameExtractionSettingType.Slider,
        DefaultValue = 1.0,
        Minimum = 0.0,
        Maximum = 1.0,
    };

    private static readonly GameExtractionSetting AmbientOcclusionQualitySetting = new()
    {
        Name = "PreviewAmbientOcclusionQuality",
        Group = GameExtractionSettingGroup.Preview,
        Section = "Ambient occlusion",
        Label = "Ambient occlusion quality",
        Type = GameExtractionSettingType.Choice,
        Options = [.. Enum.GetNames<AmbientOcclusionQuality>()],
        DefaultValue = AmbientOcclusionQuality.High,
    };

    private static readonly GameExtractionSetting AmbientOcclusionRadiusSetting = new()
    {
        Name = "PreviewAmbientOcclusionRadius",
        Group = GameExtractionSettingGroup.Preview,
        Section = "Ambient occlusion",
        Label = "Ambient occlusion radius",
        Type = GameExtractionSettingType.Slider,
        DefaultValue = 0.08,
        Minimum = 0.01,
        Maximum = 0.5,
    };

    private static readonly GameExtractionSetting AmbientOcclusionIntensitySetting = new()
    {
        Name = "PreviewAmbientOcclusionIntensity",
        Group = GameExtractionSettingGroup.Preview,
        Section = "Ambient occlusion",
        Label = "Ambient occlusion intensity",
        Type = GameExtractionSettingType.Slider,
        DefaultValue = 1.5,
        Minimum = 0.0,
        Maximum = 4.0,
    };

    private static readonly GameExtractionSetting AutoFitSetting = new()
    {
        Name = "PreviewAutoFitScene",
        Group = GameExtractionSettingGroup.Preview,
        Section = "Interface",
        Label = "Auto fit scenes",
        Type = GameExtractionSettingType.Boolean,
        DefaultValue = true,
    };

    private static readonly GameExtractionSetting AutoPlayAudioSetting = new()
    {
        Name = "PreviewAutoPlayAudio",
        Group = GameExtractionSettingGroup.Preview,
        Section = "Interface",
        Label = "Auto-play audio",
        Type = GameExtractionSettingType.Boolean,
        DefaultValue = false,
    };

    private static readonly GameExtractionSetting AssetGridSetting = new()
    {
        Name = "PreviewAssetGrid",
        Group = GameExtractionSettingGroup.Preview,
        Section = "Interface",
        Label = "Asset grid view",
        Type = GameExtractionSettingType.Boolean,
        DefaultValue = false,
    };

    private static readonly GameExtractionSetting SkyboxSetting = new()
    {
        Name = "PreviewSkybox",
        Group = GameExtractionSettingGroup.Preview,
        Section = "Skybox",
        Label = "Show skybox background",
        Type = GameExtractionSettingType.Boolean,
        DefaultValue = true,
    };

    private static readonly GameExtractionSetting SkyboxBlurSetting = new()
    {
        Name = "PreviewSkyboxBlur",
        Group = GameExtractionSettingGroup.Preview,
        Section = "Skybox",
        DependsOn = "PreviewSkybox",
        Label = "Skybox background blur",
        Type = GameExtractionSettingType.Slider,
        DefaultValue = 0.0,
        Minimum = 0.0,
        Maximum = 1.0,
    };

    private static readonly GameExtractionSetting SkyboxRotationSetting = new()
    {
        Name = "PreviewSkyboxRotation",
        Group = GameExtractionSettingGroup.Preview,
        Section = "Skybox",
        Label = "Skybox rotation",
        Description = "Hold Shift and drag in the 3D preview to rotate the skybox.",
        Type = GameExtractionSettingType.Slider,
        DefaultValue = 0.0,
        Minimum = 0.0,
        Maximum = 360.0,
    };

    private static readonly GameExtractionSetting PreviewPanelSetting = new()
    {
        Name = "PreviewPanelVisible",
        Group = GameExtractionSettingGroup.Preview,
        Section = "Interface",
        Label = "Show preview panel",
        Type = GameExtractionSettingType.Boolean,
        DefaultValue = true,
    };

    private static readonly GameExtractionSetting PreviewPanelRatioSetting = new()
    {
        Name = "PreviewPanelRatio",
        Group = GameExtractionSettingGroup.Preview,
        Section = "Interface",
        Label = "Preview panel width",
        Type = GameExtractionSettingType.Text,
        DefaultValue = 0.4,
    };

    /// <summary>
    /// Gets the optional setting definitions for the built-in preview preferences.
    /// Add these to an application's setting definitions to expose them in its settings UI.
    /// </summary>
    public static IReadOnlyList<GameExtractionSetting> SettingDefinitions { get; } =
    [
        UpAxisSetting,
        ModelFrontSetting,
        SkinningModeSetting,
        LightingModeSetting,
        AntiAliasingSetting,
        ShadowQualitySetting,
        ShadowSoftnessSetting,
        ShadowStrengthSetting,
        AmbientOcclusionQualitySetting,
        AmbientOcclusionRadiusSetting,
        AmbientOcclusionIntensitySetting,
        AutoFitSetting,
        AutoPlayAudioSetting,
        AssetGridSetting,
        SkyboxSetting,
        SkyboxBlurSetting,
        SkyboxRotationSetting,
    ];

    internal event Action? SaveRequested;

    internal ScenePreviewCameraState? CameraState { get; set; }

    /// <summary>
    /// Gets or sets the scene up axis.
    /// </summary>
    [ObservableProperty]
    public partial SceneUpAxis UpAxis { get; set; } = SceneUpAxis.Y;

    [ObservableProperty]
    internal partial ScenePreviewFrontAxis ModelFront { get; set; } = ScenePreviewFrontAxis.PositiveZ;

    /// <summary>
    /// Gets or sets the renderer skinning mode.
    /// </summary>
    [ObservableProperty]
    public partial SkinningMode SkinningMode { get; set; } = SkinningMode.Linear;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UseViewBasedLighting))]
    [NotifyPropertyChangedFor(nameof(UseFlatShading))]
    internal partial ScenePreviewLightingMode LightingMode { get; set; }

    /// <summary>
    /// Gets a value indicating whether view-based lighting is enabled.
    /// </summary>
    public bool UseViewBasedLighting => LightingMode == ScenePreviewLightingMode.ViewBased;

    /// <summary>
    /// Gets a value indicating whether materials are drawn with their base color only, without lighting.
    /// </summary>
    public bool UseFlatShading => LightingMode == ScenePreviewLightingMode.Flat;

    [ObservableProperty]
    public partial int AntiAliasingSamples { get; set; } = 8;

    [ObservableProperty]
    public partial ShadowQuality ShadowQuality { get; set; } = ShadowQuality.High;

    [ObservableProperty]
    public partial float ShadowSoftness { get; set; } = 1.0f;

    [ObservableProperty]
    public partial float ShadowStrength { get; set; } = 1.0f;

    [ObservableProperty]
    public partial AmbientOcclusionQuality AmbientOcclusionQuality { get; set; } = AmbientOcclusionQuality.High;

    [ObservableProperty]
    public partial float AmbientOcclusionRadius { get; set; } = 0.08f;

    [ObservableProperty]
    public partial float AmbientOcclusionIntensity { get; set; } = 1.5f;

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
    /// Gets or sets how mesh wireframes are drawn in scene previews.
    /// </summary>
    [ObservableProperty]
    public partial WireframeMode WireframeMode { get; set; }

    // The skybox always lights and reflects on the model; these only control the visible background.
    [ObservableProperty]
    public partial bool ShowSkybox { get; set; } = true;

    [ObservableProperty]
    public partial float SkyboxBlur { get; set; }

    [ObservableProperty]
    public partial float SkyboxRotation { get; set; }

    internal bool HasSkybox => SkyboxTexture is not null;

    internal Texture? SkyboxTexture { get; set; }

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

    [ObservableProperty]
    public partial bool ShowAssetGrid { get; set; }

    [ObservableProperty]
    public partial bool ShowPreviewPanel { get; set; } = true;

    internal double PreviewPanelRatio { get; set; } = 0.4;

    internal void LoadFrom(GameExtractionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        UpAxis = GetEnumValue(settings, UpAxisSetting, SceneUpAxis.Y);
        ModelFront = GetEnumValue(settings, ModelFrontSetting, ScenePreviewFrontAxis.PositiveZ);
        SkinningMode = GetEnumValue(settings, SkinningModeSetting, SkinningMode.Linear);
        LightingMode = GetEnumValue(settings, LightingModeSetting, ScenePreviewLightingMode.Scene);
        AntiAliasingSamples = settings.GetSettingValue(AntiAliasingSetting) switch
        {
            "Off"	=> 1,
            "2x"	=> 2,
            "4x"	=> 4,
            _		=> 8,
        };
        ShadowQuality = GetEnumValue(settings, ShadowQualitySetting, ShadowQuality.High);
        ShadowSoftness = float.TryParse(settings.GetSettingValue(ShadowSoftnessSetting), NumberStyles.Float, CultureInfo.InvariantCulture, out float shadowSoftness) ? Math.Clamp(shadowSoftness, 0.0f, 3.0f) : 1.0f;
        ShadowStrength = float.TryParse(settings.GetSettingValue(ShadowStrengthSetting), NumberStyles.Float, CultureInfo.InvariantCulture, out float shadowStrength) ? Math.Clamp(shadowStrength, 0.0f, 1.0f) : 1.0f;
        AmbientOcclusionQuality = GetEnumValue(settings, AmbientOcclusionQualitySetting, AmbientOcclusionQuality.High);
        AmbientOcclusionRadius = float.TryParse(settings.GetSettingValue(AmbientOcclusionRadiusSetting), NumberStyles.Float, CultureInfo.InvariantCulture, out float ambientOcclusionRadius) ? Math.Clamp(ambientOcclusionRadius, 0.01f, 0.5f) : 0.08f;
        AmbientOcclusionIntensity = float.TryParse(settings.GetSettingValue(AmbientOcclusionIntensitySetting), NumberStyles.Float, CultureInfo.InvariantCulture, out float ambientOcclusionIntensity) ? Math.Clamp(ambientOcclusionIntensity, 0.0f, 4.0f) : 1.5f;
        AutoFitScene = bool.TryParse(settings.GetSettingValue(AutoFitSetting), out bool autoFit) ? autoFit : true;
        AutoPlayAudio = bool.TryParse(settings.GetSettingValue(AutoPlayAudioSetting), out bool autoPlay) && autoPlay;
        ShowAssetGrid = bool.TryParse(settings.GetSettingValue(AssetGridSetting), out bool showAssetGrid) && showAssetGrid;
        ShowSkybox = bool.TryParse(settings.GetSettingValue(SkyboxSetting), out bool showSkybox) ? showSkybox : true;
        SkyboxBlur = float.TryParse(settings.GetSettingValue(SkyboxBlurSetting), NumberStyles.Float, CultureInfo.InvariantCulture, out float skyboxBlur) ? Math.Clamp(skyboxBlur, 0.0f, 1.0f) : 0.0f;
        SkyboxRotation = float.TryParse(settings.GetSettingValue(SkyboxRotationSetting), NumberStyles.Float, CultureInfo.InvariantCulture, out float skyboxRotation) ? Math.Clamp(skyboxRotation, 0.0f, 360.0f) : 0.0f;
        ShowPreviewPanel = bool.TryParse(settings.GetSettingValue(PreviewPanelSetting), out bool showPreviewPanel) ? showPreviewPanel : true;
        PreviewPanelRatio = double.TryParse(settings.GetSettingValue(PreviewPanelRatioSetting), CultureInfo.InvariantCulture, out double ratio) ? Math.Clamp(ratio, 0.1, 0.9) : 0.4;
    }

    internal void SaveLayoutTo(GameExtractionSettings settings)
    {
        settings.SetSettingValue(AssetGridSetting, ShowAssetGrid.ToString());
        settings.SetSettingValue(PreviewPanelSetting, ShowPreviewPanel.ToString());
        settings.SetSettingValue(PreviewPanelRatioSetting, PreviewPanelRatio.ToString(CultureInfo.InvariantCulture));
        settings.SetSettingValue(SkyboxRotationSetting, SkyboxRotation.ToString(CultureInfo.InvariantCulture));
    }

    internal void RequestSave() => SaveRequested?.Invoke();

    internal static Vector3 OrientToModelFront(Vector3 viewDirection, SceneUpAxis upAxis, ScenePreviewFrontAxis modelFront)
    {
        Vector3 front = modelFront switch
        {
            ScenePreviewFrontAxis.PositiveX	=> Vector3.UnitX,
            ScenePreviewFrontAxis.NegativeX	=> -Vector3.UnitX,
            ScenePreviewFrontAxis.PositiveY	=> Vector3.UnitY,
            ScenePreviewFrontAxis.NegativeY	=> -Vector3.UnitY,
            ScenePreviewFrontAxis.NegativeZ	=> -Vector3.UnitZ,
            _								=> Vector3.UnitZ,
        };

        Matrix4x4 sceneAxis = upAxis switch
        {
            SceneUpAxis.X	=> Matrix4x4.CreateRotationZ(MathF.PI * 0.5f),
            SceneUpAxis.Z	=> Matrix4x4.CreateRotationX(-MathF.PI * 0.5f),
            _				=> Matrix4x4.Identity,
        };

        Vector3 displayFront = Vector3.TransformNormal(front, sceneAxis);
        if (MathF.Abs(displayFront.Y) > 0.5f)
        {
            return viewDirection;
        }

        return Vector3.TransformNormal(viewDirection, Matrix4x4.CreateRotationY(MathF.Atan2(displayFront.X, displayFront.Z)));
    }

    private static TEnum GetEnumValue<TEnum>(GameExtractionSettings settings, GameExtractionSetting setting, TEnum fallback) where TEnum : struct, Enum
    {
        string? value = settings.GetSettingValue(setting)?.Replace(" ", string.Empty);
        return Enum.TryParse(value, ignoreCase: true, out TEnum parsed) && Enum.IsDefined(parsed) ? parsed : fallback;
    }
}
