using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using RedFox.GameExtraction.UI.Models;
using RedFox.GameExtraction.UI.Views;
using RedFox.Graphics3D;

namespace RedFox.GameExtraction.UI.ViewModels;

public partial class PreviewViewModel
{
    private AssetRowViewModel? _modelAsset;
    private Scene[] _modelScenes = [];
    private Scene[] _externalAnimations = [];
    private AssetSourceViewModel? _animationSource;
    private List<PreviewModelEntry>? _requestedModels;
    private SceneCombineMode _committedMode = SceneCombineMode.ConnectBones;

    /// <summary>
    /// Gets the preview group members in base-model order.
    /// </summary>
    public ObservableCollection<PreviewModelEntry> Models { get; } = [];

    /// <summary>
    /// Gets whether a model preview group is active.
    /// </summary>
    public bool HasModelGroup => (_requestedModels?.Count ?? Models.Count) > 0;

    /// <summary>
    /// Adds or removes a model asset from the preview group.
    /// </summary>
    /// <param name="asset">The model asset to toggle.</param>
    /// <returns>A task that completes when the preview group is updated.</returns>
    public async Task ToggleModelAsync(AssetRowViewModel asset)
    {
        if (asset.Category != AssetCategory.Model)
        {
            return;
        }
        bool starting						= !HasModelGroup && _requestedModels is null;
        List<PreviewModelEntry> requested	= [.. _requestedModels ?? Models.ToList()];
        if (!HasModelGroup && requested.Count == 0 && _modelAsset is { } current && _modelScenes.Length > 0 && Content?.DataContext is ScenePreviewViewModel { HasModel: true } preview)
        {
            int index = preview.SelectedSceneIndex;
            requested.Add(new PreviewModelEntry(current, _modelScenes[Math.Clamp(index, 0, _modelScenes.Length - 1)]));
        }
        PreviewModelEntry? existing = requested.FirstOrDefault(model => ReferenceEquals(model.Asset, asset));
        if (existing is not null && !starting)
        {
            requested.Remove(existing);
        }
        else if (existing is null)
        {
            requested.Add(new PreviewModelEntry(asset, null));
        }
        await BuildModelGroupAsync(requested);
    }

    /// <summary>
    /// Rebuilds the current model group using the selected merge mode.
    /// </summary>
    /// <returns>A task that completes when the preview is rebuilt.</returns>
    public Task RebuildModelGroupAsync() => BuildModelGroupAsync([.. _requestedModels ?? Models.ToList()]);

    /// <summary>
    /// Releases the current preview view while retaining the model group.
    /// </summary>
    public void ReleaseView() => SetContent(null);

    /// <summary>
    /// Removes a source's models and applied animations from the preview group.
    /// </summary>
    /// <param name="source">The source being removed.</param>
    /// <returns>A task that completes when the remaining group is rebuilt.</returns>
    public async Task RemoveModelSourceAsync(AssetSourceViewModel source)
    {
        bool removedAnimation = ReferenceEquals(_animationSource, source);
        if (removedAnimation)
        {
            _externalAnimations	= [];
            _animationSource	= null;
        }
        if (ReferenceEquals(_modelAsset?.Source, source))
        {
            _modelAsset		= null;
            _modelScenes	= [];
        }
        List<PreviewModelEntry> current = [.. _requestedModels ?? Models.ToList()];
        if (current.Any(model => ReferenceEquals(model.Asset.Source, source)) || removedAnimation && HasModelGroup)
        {
            await BuildModelGroupAsync(current.Where(model => !ReferenceEquals(model.Asset.Source, source)).ToList());
        }
    }

    [RelayCommand]
    private void ClearModelSelection()
    {
        Clear();
        ErrorMessage	= null;
        StatusText		= "Waiting for selection";
    }

    private async Task BuildModelGroupAsync(List<PreviewModelEntry> requested)
    {
        if (requested.Count == 0)
        {
            ClearModelSelection();
            return;
        }
        SceneCombineMode previousMode = Models.Count > 0 ? _committedMode : _previewSettings.MergeMode;
        Cancel();
        SetRequestedModels(requested);

        var cancellation	= new CancellationTokenSource();
        var token			= cancellation.Token;
        var version			= _loadVersion;
        _loadCancellation	= cancellation;

        IsLoading		= true;
        ErrorMessage	= null;

        bool acquired = false;
        try
        {
            await _readGate.WaitAsync(token);
            acquired									= true;
            GameExtractionConfiguration configuration	= _configurationFactory();
            for (int index = 0; index < requested.Count; index++)
            {
                PreviewModelEntry entry = requested[index];
                if (entry.Scene is not null)
                {
                    continue;
                }
                AssetReadResult read	= await Task.Run(() => assetManager.ReadAsync(entry.Asset.Asset, configuration, AssetReadPurpose.Preview, token), token);
                token.ThrowIfCancellationRequested();
                Scene[] scenes = GetScenes(read.Data);
                if (scenes.Length == 0 || !scenes[0].EnumerateDescendants().Any(node => node is Mesh or SkeletonBone))
                {
                    entry.Asset.IsInPreviewGroup = false;
                    requested.RemoveAt(index);
                    index--;
                    continue;
                }
                Scene snapshot = await Task.Run(() => SceneCombiner.Combine([scenes[0]], SceneCombineMode.InPlace, token), token);
                requested[index] = new PreviewModelEntry(entry.Asset, snapshot);
            }
            token.ThrowIfCancellationRequested();
            if (requested.Count == 0)
            {
                ClearModelSelection();
                return;
            }
            SceneCombineMode mode	= _previewSettings.MergeMode;
            Scene combined			= await Task.Run(() =>
            {
                Scene scene		= SceneCombiner.Combine(requested.Select(model => model.Scene ?? throw new InvalidOperationException("Model has not loaded.")).ToArray(), mode, token);
                int modelIndex	= 0;
                foreach (SceneNode model in scene.EnumerateChildren())
                {
                    foreach (Animation animation in model.EnumerateDescendants<Animation>())
                    {
                        animation.Name = $"{requested[modelIndex].Name}: {animation.Name}";
                    }
                    modelIndex++;
                }
                foreach (Scene external in _externalAnimations)
                {
                    foreach (Animation animation in external.EnumerateDescendants<Animation>())
                    {
                        Animation copy = (Animation)animation.Clone();
                        scene.AddNode(copy);
                    }
                }
                return scene;
            }, token);
            IReadOnlyDictionary<Scene, ScenePreviewData> prepared = await Task.Run(() =>
            {
                var data = PrepareSceneGeometry(combined, token);
                SceneCombiner.BindAnimations(combined, mode, token);
                return data;
            }, token);
            if (version != _loadVersion)
            {
                return;
            }
            if (mode != _previewSettings.MergeMode)
            {
                _readGate.Release();
                acquired = false;
                await BuildModelGroupAsync(requested);
                return;
            }
            ScenePreviewViewModel? previous	= Content?.DataContext as ScenePreviewViewModel;
            int selectedAnimation			= previous?.SelectedAnimationIndex ?? 0;
            string? selectedClip			= previous is not null && selectedAnimation > 0 ? previous.AnimationNames[selectedAnimation] : null;
            double position					= previous?.AnimationPosition ?? 0;
            previous?.CaptureCameraState();
            ScenePreviewCameraState? cameraState	= _previewSettings.CameraState;
            ScenePreviewViewModel viewModel			= new([combined], _previewSettings, null, prepared)
            {
                CombinationMode			= mode,
                ClearSelectionCommand	= ClearModelSelectionCommand,
            };
            int clipIndex					= selectedClip is null ? 0 : viewModel.AnimationNames.ToList().IndexOf(selectedClip);
            if (clipIndex < 0 && requested.Count > 0)
            {
                clipIndex = viewModel.AnimationNames.ToList().IndexOf($"{requested[0].Name}: {selectedClip}");
            }
            ScenePreviewView view;
            if (Content is ScenePreviewView existingView)
            {
                previous?.Dispose();
                existingView.DataContext		= viewModel;
                viewModel.SelectedSceneIndex	= 0;

                view = existingView;
            }
            else
            {
                view = new ScenePreviewView { DataContext = viewModel };
            }
            bool paused = _previewSettings.IsAnimationPaused;

            viewModel.SelectedAnimationIndex	= Math.Max(0, clipIndex);
            viewModel.AnimationPosition			= Math.Min(position, viewModel.AnimationDuration);
            _previewSettings.IsAnimationPaused	= paused;
            if (previous is not null && cameraState is { } state && viewModel.ViewportController is { } viewport)
            {
                OrbitCamera camera	= viewport.Camera;
                camera.YawRadians	= state.YawRadians;
                camera.PitchRadians	= state.PitchRadians;
                camera.OrbitTarget	= state.OrbitTarget;
                camera.Distance		= state.Distance;
                camera.ApplyOrbit();
                if (_previewSettings.AutoFitScene)
                {
                    viewport.FitViewDirection = camera.GetForward();
                    viewport.FitCameraToScene();
                    camera.YawRadians	= state.YawRadians;
                    camera.PitchRadians	= state.PitchRadians;
                    camera.ApplyOrbit();
                }
                viewModel.CaptureCameraState();
            }
            SetContent(view);
            Models.Clear();
            foreach (PreviewModelEntry model in requested)
            {
                Models.Add(model);
            }
            Asset			= requested[0].Asset;
            _committedMode	= mode;
            NotifyModelGroupChanged();
            StatusText = $"{Models.Count} models";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            if (version == _loadVersion)
            {
                ErrorMessage				= exception.Message;
                StatusText					= $"Merge failed: {exception.Message}";
                _previewSettings.MergeMode	= previousMode;
                _previewSettings.RequestSave();
            }
        }
        finally
        {
            if (acquired)
            {
                _readGate.Release();
            }
            if (version == _loadVersion)
            {
                SetRequestedModels(null);
                IsLoading = false;
            }
        }
    }

    private void SetRequestedModels(List<PreviewModelEntry>? requested)
    {
        HashSet<AssetRowViewModel> selected = [.. (requested ?? Models.ToList()).Select(model => model.Asset)];
        foreach (PreviewModelEntry model in Models.Concat(_requestedModels ?? []))
        {
            model.Asset.IsInPreviewGroup = selected.Contains(model.Asset);
        }
        foreach (AssetRowViewModel asset in selected)
        {
            asset.IsInPreviewGroup = true;
        }
        _requestedModels = requested;
        if (Content?.DataContext is ScenePreviewViewModel preview)
        {
            preview.ClearSelectionCommand = selected.Count > 0 ? ClearModelSelectionCommand : null;
        }
        NotifyModelGroupChanged();
    }

    private void ResetModelGroup()
    {
        foreach (PreviewModelEntry model in Models.Concat(_requestedModels ?? []))
        {
            model.Asset.IsInPreviewGroup = false;
        }
        Models.Clear();
        _requestedModels = null;
        if (Content?.DataContext is ScenePreviewViewModel preview)
        {
            preview.ClearSelectionCommand = null;
        }
        NotifyModelGroupChanged();
    }

    private void NotifyModelGroupChanged()
    {
        OnPropertyChanged(nameof(HasModelGroup));
    }

    private static Scene[] GetScenes(object? data) => data switch
    {
        Scene scene					=> [scene],
        IEnumerable<Scene> scenes	=> scenes.ToArray(),
        _								=> [],
    };
}
