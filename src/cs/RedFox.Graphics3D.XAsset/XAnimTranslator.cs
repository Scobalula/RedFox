using RedFox.Graphics3D.IO;

namespace RedFox.Graphics3D.XAsset;

public sealed class XAnimTranslator : SceneTranslator
{
    public override string Name => "XAsset XAnim";

    public override bool CanRead => true;

    public override bool CanWrite => true;

    public override IReadOnlyList<string> Extensions => [".xanim_export", ".xanim_bin"];

    public override bool IsValid(string filePath, string ext, SceneTranslationContext context) => Extensions.Contains(ext, StringComparer.OrdinalIgnoreCase);

    public override void Read(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token) => XAnimReader.Read(scene, stream, context, token);

    public override void Write(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token) => XAnimWriter.Write(scene, stream, context, token);
}
