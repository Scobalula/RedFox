using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedFox.GameExtraction;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Represents a view model for managing a collection of loaded sources, providing functionality to unload individual
/// sources.
/// </summary>
/// <param name="sources">The collection of loaded sources to be managed by the view model. Cannot be null.</param>
/// <param name="unloadAction">The action to invoke when a source is unloaded. Receives the source to unload as a parameter. Cannot be null.</param>
public partial class SourceManagerViewModel(ObservableCollection<LoadedSource> sources, Action<LoadedSource> unloadAction) : ObservableObject
{
    public ObservableCollection<LoadedSource> Sources { get; } = sources;

    [RelayCommand]
    private void Unload(LoadedSource source)
    {
        unloadAction(source);
    }
}
