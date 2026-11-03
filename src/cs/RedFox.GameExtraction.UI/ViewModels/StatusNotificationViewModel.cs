using CommunityToolkit.Mvvm.ComponentModel;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Represents a status notification shown to the user.
/// </summary>
/// <param name="kind">The kind of notification.</param>
/// <param name="message">The notification message.</param>
public partial class StatusNotificationViewModel(StatusNotificationKind kind, string message) : ObservableObject
{
    /// <summary>
    /// Gets the kind of notification.
    /// </summary>
    public StatusNotificationKind Kind { get; } = kind;

    /// <summary>
    /// Gets the notification message.
    /// </summary>
    public string Message { get; } = message;

    /// <summary>
    /// Gets or sets whether the notification is animating away.
    /// </summary>
    [ObservableProperty]
    public partial bool IsClosing { get; set; }
}
