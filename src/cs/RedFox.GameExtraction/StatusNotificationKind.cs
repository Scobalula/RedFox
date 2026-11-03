namespace RedFox.GameExtraction;

/// <summary>
/// Identifies the kind of status notification shown to the user.
/// </summary>
public enum StatusNotificationKind
{
    /// <summary>
    /// General information.
    /// </summary>
    Information,

    /// <summary>
    /// An operation completed successfully.
    /// </summary>
    Success,

    /// <summary>
    /// An operation completed with problems.
    /// </summary>
    Warning,

    /// <summary>
    /// An operation failed.
    /// </summary>
    Error,
}
