using Avalonia.Controls;
using RedFox.Audio;
using RedFox.GameExtraction.UI.Controls;
using RedFox.GameExtraction.UI.Models;
using RedFox.GameExtraction.UI.ViewModels;
using RedFox.GameExtraction.UI.Views;
using RedFox.Graphics3D;

namespace RedFox.GameExtraction.UI;

/// <summary>
/// Helpers for registering the built-in previewers and combining them with caller supplied previewers.
/// </summary>
public static class AssetPreviewers
{
    /// <summary>
    /// Creates previewers for scenes, images, audio buffers, tables, and byte arrays.
    /// </summary>
    public static IReadOnlyList<IAssetPreviewer> CreateDefault() =>
    [
        CreateScene(),
        CreateImage(),
        CreateAudio(),
        CreateTable(),
        CreateHex(),
    ];

    /// <summary>
    /// Places custom previewers before the built-ins, so callers can override the common payload views.
    /// </summary>
    /// <param name="customPreviewers">Caller supplied previewers, checked in order.</param>
    public static IReadOnlyList<IAssetPreviewer> WithDefaults(params IAssetPreviewer[] customPreviewers)
    {
        ArgumentNullException.ThrowIfNull(customPreviewers);
        if (customPreviewers.Any(previewer => previewer is null))
        {
            throw new ArgumentException("Previewers cannot contain null entries.", nameof(customPreviewers));
        }

        return Array.AsReadOnly([.. customPreviewers, .. CreateDefault()]);
    }

    /// <summary>
    /// Creates the standard 3D scene previewer.
    /// </summary>
    public static IAssetPreviewer CreateScene()
    {
        return new AssetPreviewer(context =>
        {
            IReadOnlyList<Scene> scenes = context.Data switch
            {
                Scene scene => [scene],
                IEnumerable<Scene> sceneSequence => [.. sceneSequence],
                _ => [],
            };

            if (scenes.Count == 0)
            {
                return null;
            }

            if (context.CurrentPreview?.DataContext is ScenePreviewViewModel current)
            {
                if (current.TryAppendAnimations(scenes))
                {
                    return context.CurrentPreview;
                }

                current.CaptureCameraState();
            }

            return new ScenePreviewView { DataContext = new ScenePreviewViewModel(scenes, context.PreviewSettings, context.PreparedSceneBounds, context.PreparedSceneData) };
        }, ScenePreviewSettings.SettingDefinitions);
    }

    /// <summary>
    /// Creates the standard image previewer for images and textures, which reads unloaded textures in the background.
    /// </summary>
    public static IAssetPreviewer CreateImage()
    {
        ImagePreviewSettings settings = new();
        return new AssetPreviewer(context => context.Data switch
        {
            RedFox.Imaging.Image image => new ImagePreviewView { DataContext = new ImagePreviewViewModel(image, settings) },
            Texture texture => new ImagePreviewView { DataContext = new ImagePreviewViewModel(texture, context.AssetManager.GetRequiredService<ImageTranslatorService>().Manager, settings) },
            _ => null,
        });
    }

    /// <summary>
    /// Creates the standard audio playback previewer.
    /// </summary>
    public static IAssetPreviewer CreateAudio() => AssetPreviewer.For<AudioBuffer>(audio =>
        new AudioPreviewView { DataContext = new AudioPreviewViewModel(audio) });

    /// <summary>
    /// Creates the standard table previewer for dictionary and enumerable table payloads.
    /// </summary>
    public static IAssetPreviewer CreateTable() => new AssetPreviewer(context =>
        PreviewTable.TryCreate(context.Data, out PreviewTable? table)
            ? new TablePreviewView { DataContext = new TablePreviewViewModel(table) }
            : null);

    /// <summary>
    /// Creates the standard hex previewer for byte array payloads.
    /// </summary>
    public static IAssetPreviewer CreateHex() => AssetPreviewer.For<byte[]>(bytes =>
        new HexBytesPreviewControl { Bytes = bytes });
}
