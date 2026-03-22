using CommunityToolkit.Mvvm.ComponentModel;

namespace RedFox.GameExtraction.UI.ViewModels;

public partial class StatusBarViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string StatusText { get; set; } = "Ready";

    [ObservableProperty]
    public partial int AssetCount { get; set; }

    public string AssetCountDisplay => AssetCount > 0
        ? $"{AssetCount:N0} assets loaded"
        : "No assets loaded";

    partial void OnAssetCountChanged(int value)
    {
        OnPropertyChanged(nameof(AssetCountDisplay));
    }
}
