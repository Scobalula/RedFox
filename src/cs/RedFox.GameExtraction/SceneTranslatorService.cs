using RedFox.Graphics3D.Formats;
using RedFox.Graphics3D.IO;

namespace RedFox.GameExtraction;

/// <summary>
/// Provides access to scene translation functionality and manages the registration of scene translators.
/// </summary>
public class SceneTranslatorService
{
    /// <summary>
    /// Gets the manager responsible for handling scene translation operations.
    /// </summary>
    public SceneTranslatorManager Manager { get; } = BuiltInFormats.CreateDefaultManager();
}
