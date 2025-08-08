
using CallOfFile;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.CoDXAsset
{
    public class CoDXModelTranslator : Graphics3DTranslator
    {
        /// <inheritdoc/>
        public override string Name => nameof(CoDXModelTranslator);

        /// <inheritdoc/>
        public override bool SupportsReading => true;

        /// <inheritdoc/>
        public override bool SupportsWriting => true;

        /// <inheritdoc/>
        public override string[] Extensions => [".xmodel_bin", ".xmodel_export"];

        /// <inheritdoc/>
        public override void Read(Stream stream, string filePath, Graphics3DScene scene)
        {
            var isBin = filePath.EndsWith(".xmodel_bin", StringComparison.CurrentCultureIgnoreCase);

            TokenReader reader = isBin ? new BinaryTokenReader(stream) : new ExportTokenReader(stream);

            reader.RequestNextTokenOfType<TokenData>("MODEL");
            reader.RequestNextTokenOfType<TokenDataUInt>("VERSION");

            var skeleton = new Skeleton();
            var model = new Model(Path.GetFileNameWithoutExtension(filePath) + "_model", skeleton);


            CoDXModelHelper.ReadBones(reader, skeleton);
            //CoDXModelHelper.ReadGeometry(reader, model, skeleton);

            scene.AddObject(skeleton);
            scene.AddObject(model);
        }

        /// <inheritdoc/>
        public override void Write(Stream stream, string filePath, Graphics3DScene scene)
        {
            throw new NotImplementedException();
        }
    }
}
