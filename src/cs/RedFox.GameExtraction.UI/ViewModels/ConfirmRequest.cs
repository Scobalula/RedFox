namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Describes a confirmation prompt shown to the user.
/// </summary>
/// <param name="Title">The window title.</param>
/// <param name="Message">The question shown to the user.</param>
/// <param name="ConfirmText">The text of the button that confirms the action.</param>
/// <param name="AlternateText">The text of an optional second choice, or <see langword="null"/> to hide it.</param>
public sealed record ConfirmRequest(string Title, string Message, string ConfirmText, string? AlternateText);
