using RedFox.Graphics3D;
using RedFox.Graphics3D.IO;

namespace RedFox.Tests.Graphics3D;

/// <summary>
/// A minimal translator used to exercise translator registration and selection.
/// Reading adds a group named after the translator; writing emits its signature.
/// </summary>
public sealed class StubSceneTranslator(string name, string extension, bool canRead, bool canWrite) : SceneTranslator
{
    /// <summary>
    /// Gets the signature written by <see cref="Write"/> and required by <see cref="SceneTranslator.IsValid(string, string, SceneTranslationContext, ReadOnlySpan{byte})"/>.
    /// </summary>
    public static ReadOnlySpan<byte> Signature => "STUB"u8;

    /// <inheritdoc/>
    public override string Name => name;

    /// <inheritdoc/>
    public override bool CanRead => canRead;

    /// <inheritdoc/>
    public override bool CanWrite => canWrite;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Extensions => [extension];

    /// <inheritdoc/>
    public override ReadOnlySpan<byte> MagicValue => Signature;

    /// <inheritdoc/>
    public override void Read(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token) => scene.RootNode.AddNode(new Group(Name));

    /// <inheritdoc/>
    public override void Write(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token) => stream.Write(Signature);
}
