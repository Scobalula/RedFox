using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using RedFox.GameExtraction.UI.Models;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Provides process selection state for the process picker.
/// </summary>
public partial class ProcessSelectionViewModel : ObservableObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessSelectionViewModel"/> class.
    /// </summary>
    /// <param name="processes">The process rows to display.</param>
    public ProcessSelectionViewModel(IReadOnlyList<ProcessCandidateViewModel> processes)
    {
        ArgumentNullException.ThrowIfNull(processes);

        foreach (ProcessCandidateViewModel process in processes)
        {
            Processes.Add(process);
        }

        SelectedProcess = Processes.FirstOrDefault();
    }

    /// <summary>
    /// Gets the displayed process rows.
    /// </summary>
    public ObservableCollection<ProcessCandidateViewModel> Processes { get; } = [];

    /// <summary>
    /// Gets or sets the selected process row.
    /// </summary>
    [ObservableProperty]
    public partial ProcessCandidateViewModel? SelectedProcess { get; set; }

    /// <summary>
    /// Gets a value indicating whether the selected process can be opened.
    /// </summary>
    public bool CanOpen => SelectedProcess is not null;

    partial void OnSelectedProcessChanged(ProcessCandidateViewModel? value)
    {
        OnPropertyChanged(nameof(CanOpen));
    }
}
