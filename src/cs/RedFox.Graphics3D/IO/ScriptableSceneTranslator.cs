namespace RedFox.Graphics3D.IO;

/// <summary>
/// A <see cref="SceneTranslator"/> base class intended for scripting hosts (for example pythonnet)
/// that cannot proxy virtual members whose signatures contain <see langword="ref struct"/> types
/// such as <see cref="ReadOnlySpan{T}"/>. The offending members are sealed here so derived
/// scripted types only need to override the safe abstract surface.
/// </summary>
public abstract class ScriptableSceneTranslator : SceneTranslator
{
    /// <inheritdoc />
    public sealed override ReadOnlySpan<byte> MagicValue => base.MagicValue;

    /// <inheritdoc />
    public sealed override bool IsValid(string filePath, string ext, SceneTranslationContext context, ReadOnlySpan<byte> startOfFile) =>
        IsValid(filePath, ext, context);
}
