using Avalonia.Input;

namespace RedFox.GameExtraction.UI;

/// <summary>
/// Implemented by preview views that respond to keyboard shortcuts. The preview window, and the main window while a preview is open,
/// forward key presses to the current preview so shortcuts work without first focusing the preview.
/// </summary>
public interface IPreviewKeyHandler
{
    /// <summary>
    /// Handles a key press.
    /// </summary>
    /// <param name="key">The pressed key.</param>
    /// <param name="modifiers">The modifier keys held during the press.</param>
    /// <returns><see langword="true"/> when the preview handled the key; otherwise, <see langword="false"/>.</returns>
    bool HandleKey(Key key, KeyModifiers modifiers);
}
