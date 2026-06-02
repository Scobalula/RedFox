using RedFox.Graphics3D.IO;

namespace RedFox.Graphics3D.ActorX;

/// <summary>
/// Reads and writes ActorX PSK and PSKX skeletal mesh files.
/// </summary>
public sealed class PskTranslator : SceneTranslator
{
    /// <inheritdoc/>
    public override string Name => "ActorX PSK";

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Extensions => [".psk", ".pskx"];

    /// <inheritdoc/>
    public override ReadOnlySpan<byte> MagicValue => "ACTRHEAD"u8;

    /// <inheritdoc/>
    public override void Read(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        var reader = new PskReader(stream, context.Name);
        reader.Read(scene);
    }

    /// <inheritdoc/>
    public override void Write(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        var writer = new PskWriter(stream);
        writer.Write(context.GetSelection(scene));
    }
}
