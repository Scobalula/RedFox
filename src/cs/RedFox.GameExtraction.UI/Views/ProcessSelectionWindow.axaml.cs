using Avalonia.Controls;
using Avalonia.Input;
using RedFox.GameExtraction.UI.Models;
using RedFox.GameExtraction.UI.ViewModels;

namespace RedFox.GameExtraction.UI.Views;

/// <summary>
/// Process selection dialog.
/// </summary>
public partial class ProcessSelectionWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessSelectionWindow"/> class.
    /// </summary>
    public ProcessSelectionWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Initializes the process picker.
    /// </summary>
    /// <param name="processes">The process rows to display.</param>
    public void Initialize(IReadOnlyList<ProcessCandidateViewModel> processes)
    {
        DataContext = new ProcessSelectionViewModel(processes);
    }

    private void OnCancelClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs args)
    {
        Close(null);
    }

    private void OnOpenClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs args)
    {
        CloseSelection();
    }

    private void OnProcessDoubleTapped(object? sender, TappedEventArgs args)
    {
        CloseSelection();
    }

    private void CloseSelection()
    {
        if (DataContext is not ProcessSelectionViewModel viewModel ||
            viewModel.SelectedProcess is null)
        {
            return;
        }

        Close(new ProcessSelectionResult(viewModel.SelectedProcess));
    }
}
