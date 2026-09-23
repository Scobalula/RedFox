using RedFox.Graphics3D.IO;

namespace RedFox.Graphics3D.XAsset;

public sealed class XModelTranslator : SceneTranslator
{
    public override string Name => "XAsset XModel";

    public override bool CanRead => true;

    public override bool CanWrite => true;

    public override IReadOnlyList<string> Extensions => [".xmodel_export", ".xmodel_bin"];

    public override bool IsValid(string filePath, string ext, SceneTranslationContext context) => Extensions.Contains(ext, StringComparer.OrdinalIgnoreCase);

    public override void Read(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token) => XModelReader.Read(scene, stream, context, token);

    public override void Write(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token) => XModelWriter.Write(scene, stream, context, token);
}
