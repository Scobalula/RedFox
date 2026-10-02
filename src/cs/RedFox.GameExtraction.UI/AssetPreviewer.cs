using Avalonia.Controls;
using System.Diagnostics.CodeAnalysis;
using RedFox.GameExtraction;

namespace RedFox.GameExtraction.UI;

/// <summary>
/// A delegate based asset previewer with a typed payload helper.
/// </summary>
public sealed class AssetPreviewer : IAssetPreviewer
{
    private readonly Func<AssetPreviewContext, Control?> _createPreview;

    /// <summary>
    /// Gets the settings this previewer exposes in the application settings window.
    /// </summary>
    public IReadOnlyList<GameExtractionSetting> SettingDefinitions { get; }

    /// <summary>
    /// Initializes a previewer from a factory that returns <see langword="null"/> for unsupported payloads.
    /// </summary>
    /// <param name="createPreview">The preview view factory.</param>
    public AssetPreviewer(Func<AssetPreviewContext, Control?> createPreview) : this(createPreview, [])
    {
    }

    /// <summary>
    /// Initializes a previewer with an explicit set of persisted settings.
    /// </summary>
    /// <param name="createPreview">The preview view factory.</param>
    /// <param name="settingDefinitions">The settings required by this previewer.</param>
    public AssetPreviewer(Func<AssetPreviewContext, Control?> createPreview, IReadOnlyList<GameExtractionSetting> settingDefinitions)
    {
        _createPreview = createPreview ?? throw new ArgumentNullException(nameof(createPreview));
        SettingDefinitions = settingDefinitions ?? throw new ArgumentNullException(nameof(settingDefinitions));
    }

    /// <summary>
    /// Creates a previewer that handles payloads of <typeparamref name="TPayload"/>.
    /// </summary>
    /// <typeparam name="TPayload">The payload type handled by the previewer.</typeparam>
    /// <param name="createPreview">Creates a view for the matching payload.</param>
    /// <returns>A previewer that declines payloads of other types.</returns>
    public static AssetPreviewer For<TPayload>(Func<TPayload, Control> createPreview)
    {
        ArgumentNullException.ThrowIfNull(createPreview);
        return new AssetPreviewer(context => context.Data is TPayload payload ? createPreview(payload) : null);
    }

    /// <summary>
    /// Creates a typed previewer with an explicit set of persisted settings.
    /// </summary>
    public static AssetPreviewer For<TPayload>(Func<TPayload, Control> createPreview, IReadOnlyList<GameExtractionSetting> settingDefinitions)
    {
        ArgumentNullException.ThrowIfNull(createPreview);
        ArgumentNullException.ThrowIfNull(settingDefinitions);
        return new AssetPreviewer(context => context.Data is TPayload payload ? createPreview(payload) : null, settingDefinitions);
    }

    /// <inheritdoc/>
    public bool TryCreatePreview(AssetPreviewContext context, [NotNullWhen(true)] out Control? preview)
    {
        ArgumentNullException.ThrowIfNull(context);
        preview = _createPreview(context);
        return preview is not null;
    }
}
