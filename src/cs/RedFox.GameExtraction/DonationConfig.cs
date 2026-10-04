namespace RedFox.GameExtraction;

/// <summary>
/// Configures the donation prompt shown by GameExtraction frontends.
/// </summary>
public sealed class DonationConfig
{
    /// <summary>
    /// Gets the settings key recording that the first launch donation prompt has been shown.
    /// </summary>
    public const string PromptedSettingKey = "DonationPrompted";

    /// <summary>
    /// Gets the URL opened when the user chooses to donate.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Gets the optional message from the creator shown alongside the donation prompt.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Marks the first launch donation prompt as shown in the provided settings.
    /// </summary>
    /// <param name="settings">The persisted settings.</param>
    /// <returns><see langword="true"/> when this is the first launch and the prompt should be shown.</returns>
    public static bool TryMarkPrompted(GameExtractionSettings settings) => settings.Values.TryAdd(PromptedSettingKey, bool.TrueString);
}
