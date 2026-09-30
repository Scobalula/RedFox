using CommunityToolkit.Mvvm.ComponentModel;
using RedFox.Audio;
using RedFox.GameExtraction.UI.Models;
using RedFox.Graphics3D;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Reads the payload of the most recently selected asset and exposes a typed preview model for it. The window maps each
/// preview model type to its view, so this class never touches controls.
/// </summary>
public partial class PreviewViewModel(AssetManager assetManager) : ObservableObject
{
    private CancellationTokenSource? _loadCancellation;
    private int _loadVersion;

    /// <summary>
    /// Gets the asset currently being previewed.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle), nameof(AssetNameDisplay), nameof(AssetTypeDisplay), nameof(SourceNameDisplay), nameof(InformationDisplay), nameof(HasInformation), nameof(HasAsset), nameof(PlaceholderTitle))]
    public partial AssetRowViewModel? Asset { get; private set; }

    /// <summary>
    /// Gets the preview model for the loaded payload: a <see cref="ScenePreviewViewModel"/>, <see cref="AudioPreviewViewModel"/>,
    /// <see cref="HexPreviewViewModel"/>, or <see cref="TablePreviewViewModel"/>, or <see langword="null"/> when nothing can be shown.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPlaceholder))]
    public partial object? Content { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the payload is being read.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaceholderTitle))]
    public partial bool IsLoading { get; private set; }

    /// <summary>
    /// Gets the error raised by the most recent read, if any.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaceholderTitle), nameof(HasError))]
    public partial string? ErrorMessage { get; private set; }

    /// <summary>
    /// Gets the number of assets selected in the main window.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionDisplay))]
    public partial int SelectionCount { get; private set; }

    /// <summary>
    /// Gets the name of the handler that services the asset.
    /// </summary>
    [ObservableProperty]
    public partial string HandlerDisplay { get; private set; } = "-";

    /// <summary>
    /// Gets the formatted payload type name.
    /// </summary>
    [ObservableProperty]
    public partial string PayloadTypeDisplay { get; private set; } = "-";

    /// <summary>
    /// Gets the number of referenced assets read alongside the payload.
    /// </summary>
    [ObservableProperty]
    public partial int ReferenceCount { get; private set; }

    /// <summary>
    /// Gets the current status message.
    /// </summary>
    [ObservableProperty]
    public partial string StatusText { get; private set; } = "Waiting for selection";

    /// <summary>
    /// Gets the preview window title.
    /// </summary>
    public string WindowTitle => Asset is null ? "Preview" : $"{Asset.Name} - Preview";

    /// <summary>
    /// Gets the asset name display.
    /// </summary>
    public string AssetNameDisplay => Asset?.Name ?? "No asset selected";

    /// <summary>
    /// Gets the asset type display.
    /// </summary>
    public string AssetTypeDisplay => Asset?.Type ?? string.Empty;

    /// <summary>
    /// Gets the source name display.
    /// </summary>
    public string SourceNameDisplay => Asset?.SourceName ?? string.Empty;

    /// <summary>
    /// Gets the loader-provided information for the asset.
    /// </summary>
    public string InformationDisplay => Asset?.Information ?? string.Empty;

    /// <summary>
    /// Gets a value indicating whether loader-provided information is available.
    /// </summary>
    public bool HasInformation => !string.IsNullOrWhiteSpace(InformationDisplay);

    /// <summary>
    /// Gets the multi-selection summary.
    /// </summary>
    public string SelectionDisplay => SelectionCount > 1 ? $"{SelectionCount:N0} selected, showing last" : string.Empty;

    /// <summary>
    /// Gets a value indicating whether an asset is being previewed.
    /// </summary>
    public bool HasAsset => Asset is not null;

    /// <summary>
    /// Gets a value indicating whether the most recent read failed.
    /// </summary>
    public bool HasError => ErrorMessage is not null;

    /// <summary>
    /// Gets a value indicating whether the placeholder replaces the previewer.
    /// </summary>
    public bool ShowPlaceholder => Content is null;

    /// <summary>
    /// Gets the placeholder title.
    /// </summary>
    public string PlaceholderTitle => ErrorMessage is not null ? "Preview failed" : IsLoading ? "Loading asset data" : Asset is null ? "No asset selected" : "No preview available";

    /// <summary>
    /// Reads the payload for the last asset in the selection. The current preview stays visible until the new payload arrives.
    /// </summary>
    /// <param name="selection">The assets selected in the main window.</param>
    /// <returns>A task that completes when the payload is loaded or the read is superseded.</returns>
    public async Task UpdateSelectionAsync(IReadOnlyList<AssetRowViewModel> selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        Cancel();
        SelectionCount = selection.Count;
        Asset = selection.Count > 0 ? selection[^1] : null;
        ErrorMessage = null;
        ReferenceCount = 0;
        PayloadTypeDisplay = "-";
        HandlerDisplay = Asset is null ? "-" : assetManager.FindHandler(Asset.Asset)?.GetType().Name ?? "Unknown Handler";

        if (Asset is null)
        {
            SetContent(null);
            StatusText = "Waiting for selection";
            return;
        }

        int loadVersion = ++_loadVersion;
        CancellationTokenSource cancellation = new();
        _loadCancellation = cancellation;
        IsLoading = true;
        StatusText = $"Reading {Asset.Name}";

        try
        {
            AssetReadResult result = await assetManager.ReadAsync(Asset.Asset, cancellation.Token).ConfigureAwait(true);
            if (loadVersion != _loadVersion)
            {
                return;
            }

            PayloadTypeDisplay = result.Data is null ? "None" : FormatTypeName(result.Data.GetType());
            ReferenceCount = result.References.Count;
            SetContent(CreateContent(result.Data));
            StatusText = Content is null ? $"No preview for {PayloadTypeDisplay}" : $"Ready ({PayloadTypeDisplay})";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            if (loadVersion == _loadVersion)
            {
                SetContent(null);
                ErrorMessage = exception.Message;
                StatusText = "Preview failed";
            }
        }
        finally
        {
            if (loadVersion == _loadVersion)
            {
                IsLoading = false;
            }
        }
    }

    /// <summary>
    /// Cancels any in-flight read.
    /// </summary>
    public void Cancel()
    {
        CancellationTokenSource? pending = _loadCancellation;
        _loadCancellation = null;
        _loadVersion++;
        IsLoading = false;
        pending?.Cancel();
        pending?.Dispose();
    }

    /// <summary>
    /// Cancels any in-flight read and releases the current preview model.
    /// </summary>
    public void Clear()
    {
        Cancel();
        SetContent(null);
    }

    private static string FormatTypeName(Type type)
    {
        if (type.IsArray)
        {
            return $"{FormatTypeName(type.GetElementType()!)}[]";
        }

        if (!type.IsGenericType)
        {
            return type.Name;
        }

        string name = type.Name[..type.Name.IndexOf('`')];
        return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(FormatTypeName))}>";
    }

    private object? CreateContent(object? payload)
    {
        return payload switch
        {
            null => null,
            Scene scene => CreateSceneContent([scene]),
            IEnumerable<Scene> scenes => CreateSceneContent([.. scenes]),
            AudioBuffer audio => new AudioPreviewViewModel(audio),
            byte[] bytes => new HexPreviewViewModel(bytes),
            _ when PreviewTable.TryCreate(payload, out PreviewTable? table) => new TablePreviewViewModel(table),
            _ => null,
        };
    }

    private object? CreateSceneContent(IReadOnlyList<Scene> scenes)
    {
        if (Content is ScenePreviewViewModel current && current.TryAppendAnimations(scenes))
        {
            return current;
        }

        return scenes.Count > 0 ? new ScenePreviewViewModel(scenes) : null;
    }

    private void SetContent(object? content)
    {
        if (ReferenceEquals(Content, content))
        {
            return;
        }

        (Content as IDisposable)?.Dispose();
        Content = content;
    }
}
