namespace RedFox.GameExtraction.UI.Models;

/// <summary>
/// Describes the process selected by the user.
/// </summary>
public sealed class ProcessSelectionResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessSelectionResult"/> class.
    /// </summary>
    /// <param name="process">The selected process.</param>
    public ProcessSelectionResult(ProcessCandidateViewModel process)
    {
        Process = process ?? throw new ArgumentNullException(nameof(process));
    }

    /// <summary>
    /// Gets the selected process.
    /// </summary>
    public ProcessCandidateViewModel Process { get; }
}
