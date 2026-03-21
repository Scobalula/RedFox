namespace RedFox.Graphics2D.Jpeg;

internal sealed class JpegEndOfScanException(JpegMarker marker) : Exception($"End of scan reached at marker 0xFF{(byte)marker:X2}.")
{
    public JpegMarker Marker { get; } = marker;
}
