namespace RedFox.Graphics3D.IO;

/// <summary>
/// Defines the kinds of scene content a translator supports.
/// </summary>
[Flags]
public enum SceneTranslatorContent
{
    /// <summary>
    /// The translator supports models.
    /// </summary>
    Model = 1,

    /// <summary>
    /// The translator supports animations.
    /// </summary>
    Animation = 2,
}
