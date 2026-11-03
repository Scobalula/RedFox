namespace RedFox.GameExtraction;

/// <summary>
/// Provides event data for a status notification.
/// </summary>
/// <param name="kind">The kind of notification.</param>
/// <param name="message">The notification message.</param>
public class StatusNotificationEventArgs(StatusNotificationKind kind, string message) : EventArgs
{
    /// <summary>
    /// Gets the kind of notification.
    /// </summary>
    public StatusNotificationKind Kind { get; } = kind;

    /// <summary>
    /// Gets the notification message.
    /// </summary>
    public string Message { get; } = message;
}
