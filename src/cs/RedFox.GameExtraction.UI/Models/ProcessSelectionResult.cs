namespace RedFox.GameExtraction.UI.Models;

/// <summary>
/// Describes the process selected by the user.
/// </summary>
/// <param name="process">The selected process.</param>
public sealed class ProcessSelectionResult(ProcessCandidateViewModel process)
{
    /// <summary>
    /// Gets the selected process.
    /// </summary>
    public ProcessCandidateViewModel Process { get; } = process ?? throw new ArgumentNullException(nameof(process));
}
