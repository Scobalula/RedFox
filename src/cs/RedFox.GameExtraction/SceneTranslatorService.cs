using RedFox.Graphics3D.Formats;
using RedFox.Graphics3D.IO;

namespace RedFox.GameExtraction;

/// <summary>
/// Owns the scene translator manager used for model and animation import and export, pre-populated with the built-in formats.
/// </summary>
public class SceneTranslatorService
{
    /// <summary>
    /// Gets the manager responsible for handling scene translation operations.
    /// </summary>
    public SceneTranslatorManager Manager { get; } = new SceneTranslatorManager();

    /// <summary>
    /// Initializes a new instance of the <see cref="SceneTranslatorService"/> class and registers every built-in scene format.
    /// </summary>
    public SceneTranslatorService()
    {
        BuiltInSceneFormats.RegisterAll(Manager);
    }
}
