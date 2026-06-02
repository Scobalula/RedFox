using RedFox.Graphics3D.IO;

namespace RedFox.Graphics3D.ActorX;

/// <summary>
/// Reads and writes ActorX PSA skeletal animation files.
/// </summary>
public sealed class PsaTranslator : SceneTranslator
{
    /// <inheritdoc/>
    public override string Name => "ActorX PSA";

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Extensions => [".psa"];

    /// <inheritdoc/>
    public override ReadOnlySpan<byte> MagicValue => "ANIMHEAD"u8;

    /// <inheritdoc/>
    public override void Read(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        var reader = new PsaReader(stream, context.Name);
        reader.Read(scene);
    }

    /// <inheritdoc/>
    public override void Write(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        var writer = new PsaWriter(stream);
        writer.Write(context.GetSelection(scene));
    }
}
