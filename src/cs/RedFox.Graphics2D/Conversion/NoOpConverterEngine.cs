namespace RedFox.Graphics2D.Conversion
{
    internal sealed class NoOpConverterEngine : ConverterEngine
    {
        public override string Name => "None";

        public override bool TryConvert(
            ReadOnlySpan<byte> source,
            ImageFormat sourceFormat,
            Span<byte> destination,
            ImageFormat destinationFormat,
            int width,
            int height,
            ImageConvertFlags flags)
        {
            return false;
        }
    }
}
