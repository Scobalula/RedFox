using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RedFox.GameExtraction.UI.ViewModels;

public partial class ProgressDialogViewModel(string title) : ObservableObject
{
    public string Title { get; } = title;

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Initializing...";

    [ObservableProperty]
    public partial double ProgressValue { get; set; }

    [ObservableProperty]
    public partial int Current { get; set; }
    [ObservableProperty]
    public partial int Total { get; set; }
    [ObservableProperty]
    public partial bool IsIndeterminate { get; set; } = true;
    [ObservableProperty]
    public partial bool IsCancelling { get; set; }

    public string ProgressText => Total > 0
        ? $"{Current:N0} / {Total:N0}"
        : string.Empty;

    partial void OnCurrentChanged(int value) => OnPropertyChanged(nameof(ProgressText));
    partial void OnTotalChanged(int value) => OnPropertyChanged(nameof(ProgressText));

    public IRelayCommand? CancelCommand { get; set; }
}
