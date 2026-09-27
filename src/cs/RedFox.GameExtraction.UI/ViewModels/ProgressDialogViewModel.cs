using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Tracks the title, status, and progress of a long-running operation.
/// </summary>
public partial class ProgressDialogViewModel(string title) : ObservableObject
{
    /// <summary>
    /// Gets the dialog title.
    /// </summary>
    public string Title { get; } = title;

    /// <summary>
    /// Gets or sets the current status message.
    /// </summary>
    [ObservableProperty]
    public partial string StatusText { get; set; } = "Initializing...";

    /// <summary>
    /// Gets or sets the current progress value.
    /// </summary>
    [ObservableProperty]
    public partial double ProgressValue { get; set; }

    /// <summary>
    /// Gets or sets the current completed item count.
    /// </summary>
    [ObservableProperty]
    public partial int Current { get; set; }

    /// <summary>
    /// Gets or sets the total item count.
    /// </summary>
    [ObservableProperty]
    public partial int Total { get; set; }

    /// <summary>
    /// Gets or sets whether the progress value is indeterminate.
    /// </summary>
    [ObservableProperty]
    public partial bool IsIndeterminate { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the operation is cancelling.
    /// </summary>
    [ObservableProperty]
    public partial bool IsCancelling { get; set; }

    /// <summary>
    /// Gets the formatted completed and total item counts.
    /// </summary>
    public string ProgressText => Total > 0
        ? $"{Current:N0} / {Total:N0}"
        : string.Empty;

    partial void OnCurrentChanged(int value) => OnPropertyChanged(nameof(ProgressText));
    partial void OnTotalChanged(int value) => OnPropertyChanged(nameof(ProgressText));

    /// <summary>
    /// Gets or sets the command that cancels the operation.
    /// </summary>
    public IRelayCommand? CancelCommand { get; set; }
}
