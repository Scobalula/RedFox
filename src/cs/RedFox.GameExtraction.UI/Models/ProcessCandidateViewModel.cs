using RedFox.GameExtraction;

namespace RedFox.GameExtraction.UI.Models;

/// <summary>
/// Presents a system process and the source readers that can open it.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ProcessCandidateViewModel"/> class.
/// </remarks>
/// <param name="request">The process source request.</param>
/// <param name="processId">The process identifier.</param>
/// <param name="processName">The process name.</param>
/// <param name="windowTitle">The process main window title.</param>
/// <param name="matchingReaders">The matching source readers.</param>
public sealed class ProcessCandidateViewModel(
    AssetSourceRequest request,
    int processId,
    string processName,
    string windowTitle,
    IReadOnlyList<ProcessReaderViewModel> matchingReaders)
{
    /// <summary>
    /// Gets the process source request.
    /// </summary>
    public AssetSourceRequest Request { get; } = request ?? throw new ArgumentNullException(nameof(request));

    /// <summary>
    /// Gets the process identifier.
    /// </summary>
    public int ProcessId { get; } = processId;

    /// <summary>
    /// Gets the process name.
    /// </summary>
    public string ProcessName { get; } = processName;

    /// <summary>
    /// Gets the main window title, when available.
    /// </summary>
    public string WindowTitle { get; } = windowTitle;

    /// <summary>
    /// Gets the matching source readers.
    /// </summary>
    public IReadOnlyList<ProcessReaderViewModel> MatchingReaders { get; } = matchingReaders;

    /// <summary>
    /// Gets a value indicating whether the process has at least one matching reader.
    /// </summary>
    public bool HasMatchingReader => MatchingReaders.Count > 0;

    /// <summary>
    /// Gets the display name for the process row.
    /// </summary>
    public string DisplayName => $"{ProcessName} ({ProcessId})";

    /// <summary>
    /// Gets display text for the matching readers.
    /// </summary>
    public string ReaderSummary => MatchingReaders.Count switch
    {
        0 => "Undetermined",
        1 => MatchingReaders[0].DisplayName,
        _ => $"{MatchingReaders.Count} readers",
    };
}
