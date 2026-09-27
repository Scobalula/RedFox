using System.Numerics;
using RedFox.Imaging.Codecs;
using RedFox.Imaging.Primitives;

namespace RedFox.Imaging.Formats;

internal static class Rgba8PixelConverter
{
    internal static byte[] Extract(in ImageSlice slice, ImageFormat format, string formatName)
    {
        int width = slice.Width;
        int height = slice.Height;
        int rowBytes = width * 4;
        var rgba = new byte[rowBytes * height];
        var source = slice.PixelSpan;

        if (format is ImageFormat.R8G8B8A8Unorm or ImageFormat.R8G8B8A8UnormSrgb)
        {
            for (int y = 0; y < height; y++)
                source.Slice(y * slice.RowPitch, rowBytes).CopyTo(rgba.AsSpan(y * rowBytes, rowBytes));
            return rgba;
        }

        if (format is ImageFormat.B8G8R8A8Unorm or ImageFormat.B8G8R8A8UnormSrgb or ImageFormat.B8G8R8X8Unorm or ImageFormat.B8G8R8X8UnormSrgb)
        {
            bool forceOpaque = format is ImageFormat.B8G8R8X8Unorm or ImageFormat.B8G8R8X8UnormSrgb;

            for (int y = 0; y < height; y++)
            {
                int sourceRow = y * slice.RowPitch;
                int destinationRow = y * rowBytes;

                for (int x = 0; x < width; x++)
                {
                    int sourceIndex = sourceRow + x * 4;
                    int destinationIndex = destinationRow + x * 4;
                    rgba[destinationIndex] = source[sourceIndex + 2];
                    rgba[destinationIndex + 1] = source[sourceIndex + 1];
                    rgba[destinationIndex + 2] = source[sourceIndex];
                    rgba[destinationIndex + 3] = forceOpaque ? (byte)255 : source[sourceIndex + 3];
                }
            }

            return rgba;
        }

        if (!PixelCodecs.TryGetCodec(format, out var codec) || codec is null)
            throw new NotSupportedException($"{formatName} writing is not supported for format {format}.");

        const int stripHeight = 4;
        var pixels = new Vector4[width * stripHeight];

        for (int stripY = 0; stripY < height; stripY += stripHeight)
        {
            int rows = Math.Min(stripHeight, height - stripY);
            codec.DecodeRows(source, pixels, stripY, rows, width, height);

            for (int row = 0; row < rows; row++)
            {
                int pixelBase = row * width;
                int destinationRow = (stripY + row) * rowBytes;

                for (int x = 0; x < width; x++)
                {
                    Vector4 pixel = pixels[pixelBase + x];
                    int destinationIndex = destinationRow + x * 4;
                    rgba[destinationIndex] = (byte)(Math.Clamp(pixel.X, 0f, 1f) * 255f + 0.5f);
                    rgba[destinationIndex + 1] = (byte)(Math.Clamp(pixel.Y, 0f, 1f) * 255f + 0.5f);
                    rgba[destinationIndex + 2] = (byte)(Math.Clamp(pixel.Z, 0f, 1f) * 255f + 0.5f);
                    rgba[destinationIndex + 3] = (byte)(Math.Clamp(pixel.W, 0f, 1f) * 255f + 0.5f);
                }
            }
        }

        return rgba;
    }
}
