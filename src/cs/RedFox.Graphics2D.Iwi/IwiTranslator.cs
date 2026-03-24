using RedFox.Graphics2D.IO;

namespace RedFox.Graphics2D.Iwi
{
    public class IwiTranslator : ImageTranslator
    {
        /// <inheritdoc/>
        public override string Name => "IWI";

        /// <inheritdoc/>
        public override bool CanRead => true;

        /// <inheritdoc/>
        public override bool CanWrite => true;

        /// <inheritdoc/>
        public override IReadOnlyList<string> Extensions => [".iwi"];

        /// <inheritdoc/>
        public override Image Read(Stream stream)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void Write(Stream stream, Image image)
        {
            throw new NotImplementedException();
        }
    }
}
