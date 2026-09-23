using RedFox.Graphics3D.IO;

namespace RedFox.Graphics3D.Formats.XAsset;

/// <summary>
/// Translates Call of Duty XAnim assets to and from <see cref="Scene"/> instances.
/// </summary>
public sealed class XAnimTranslator : SceneTranslator
{
    /// <summary>
    /// Gets the display name used to identify this translator.
    /// </summary>
    public override string Name => "XAsset XAnim";

    /// <summary>
    /// Gets whether this translator can read XAnim assets.
    /// </summary>
    public override bool CanRead => true;

    /// <summary>
    /// Gets whether this translator can write XAnim assets.
    /// </summary>
    public override bool CanWrite => true;

    /// <summary>
    /// Gets the XAnim file extensions handled by this translator.
    /// </summary>
    public override IReadOnlyList<string> Extensions => [".xanim_export", ".xanim_bin"];

    /// <summary>
    /// Determines whether the supplied file metadata identifies an XAnim asset.
    /// </summary>
    /// <param name="filePath">The source file path.</param>
    /// <param name="ext">The normalized file extension.</param>
    /// <param name="context">The translation context for the operation.</param>
    /// <returns><see langword="true"/> when the extension is supported; otherwise, <see langword="false"/>.</returns>
    public override bool IsValid(string filePath, string ext, SceneTranslationContext context) => Extensions.Contains(ext, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Reads an XAnim asset from a stream into a scene.
    /// </summary>
    /// <param name="scene">The destination scene.</param>
    /// <param name="stream">The readable XAnim stream.</param>
    /// <param name="context">The translation context for the operation.</param>
    /// <param name="token">An optional cancellation token.</param>
    public override void Read(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token) => XAnimReader.Read(scene, stream, context, token);

    /// <summary>
    /// Writes scene animation data as an XAnim asset.
    /// </summary>
    /// <param name="scene">The source scene.</param>
    /// <param name="stream">The destination XAnim stream.</param>
    /// <param name="context">The translation context for the operation.</param>
    /// <param name="token">An optional cancellation token.</param>
    public override void Write(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token) => XAnimWriter.Write(scene, stream, context, token);
}
