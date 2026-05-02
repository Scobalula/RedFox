using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedFox.GameExtraction.UI.Models;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Provides source management commands for the source manager window.
/// </summary>
public partial class SourceManagerViewModel : ObservableObject
{
    private readonly Func<AssetSourceViewModel, Task> _unloadAsync;

    /// <summary>
    /// Initializes a new instance of the <see cref="SourceManagerViewModel"/> class.
    /// </summary>
    /// <param name="sources">The mounted source collection.</param>
    /// <param name="unloadAsync">The source unload callback.</param>
    public SourceManagerViewModel(
        ObservableCollection<AssetSourceViewModel> sources,
        Func<AssetSourceViewModel, Task> unloadAsync)
    {
        Sources = sources ?? throw new ArgumentNullException(nameof(sources));
        _unloadAsync = unloadAsync ?? throw new ArgumentNullException(nameof(unloadAsync));
    }

    /// <summary>
    /// Gets the mounted sources.
    /// </summary>
    public ObservableCollection<AssetSourceViewModel> Sources { get; }

    /// <summary>
    /// Unloads a source.
    /// </summary>
    /// <param name="source">The source to unload.</param>
    /// <returns>A task that completes when the source is unloaded.</returns>
    [RelayCommand]
    public Task UnloadAsync(AssetSourceViewModel source)
    {
        return _unloadAsync(source);
    }
}
