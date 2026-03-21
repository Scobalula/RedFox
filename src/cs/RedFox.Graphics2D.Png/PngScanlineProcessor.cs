using System;
using System.IO;

namespace RedFox.Graphics2D.Png;

internal static class PngScanlineProcessor
{
    public static void UnfilterRow(Span<byte> row, ReadOnlySpan<byte> prevRow, int bytesPerPixel, byte filterType)
    {
        switch (filterType)
        {
            case 0:
                return;
            case 1:
                for (int i = 0; i < row.Length; i++)
                {
                    byte left = i >= bytesPerPixel ? row[i - bytesPerPixel] : (byte)0;
                    row[i] = unchecked((byte)(row[i] + left));
                }
                return;
            case 2:
                for (int i = 0; i < row.Length; i++)
                    row[i] = unchecked((byte)(row[i] + prevRow[i]));
                return;
            case 3:
                for (int i = 0; i < row.Length; i++)
                {
                    byte left = i >= bytesPerPixel ? row[i - bytesPerPixel] : (byte)0;
                    byte up = prevRow[i];
                    row[i] = unchecked((byte)(row[i] + ((left + up) >> 1)));
                }
                return;
            case 4:
                for (int i = 0; i < row.Length; i++)
                {
                    int a = i >= bytesPerPixel ? row[i - bytesPerPixel] : 0;
                    int b = prevRow[i];
                    int c = i >= bytesPerPixel ? prevRow[i - bytesPerPixel] : 0;
                    row[i] = unchecked((byte)(row[i] + PaethPredictor(a, b, c)));
                }
                return;
            default:
                throw new NotSupportedException($"Unsupported PNG filter type {filterType}.");
        }
    }

    public static byte[] ApplyAdaptiveFiltering(ReadOnlySpan<byte> scanlines, int width, int height, byte colorType, byte bitDepth)
    {
        int channels = PngFormatValidator.GetChannelCount(colorType);
        int bitsPerPixel = channels * bitDepth;
        int rowBytes = (width * bitsPerPixel + 7) / 8;
        int bytesPerPixel = Math.Max(1, (bitsPerPixel + 7) / 8);

        bool fastMode = rowBytes * height > PngConstants.FullAdaptiveFilterMaxBytes;

        var output = new byte[height * (rowBytes + 1)];
        var prev = new byte[rowBytes];
        var filteredCandidate = new byte[rowBytes];
        var filteredCandidate2 = fastMode ? new byte[rowBytes] : null;

        for (int y = 0; y < height; y++)
        {
            var row = scanlines.Slice(y * rowBytes, rowBytes);

            byte bestFilter;

            if (fastMode)
            {
                // Large images use a cheaper filter search to avoid O(5 * rowBytes * height) overhead.
                FilterRow(row, prev, filteredCandidate, bytesPerPixel, 0);
                FilterRow(row, prev, filteredCandidate2!, bytesPerPixel, 1);

                long scoreNone = ScoreFilteredRow(filteredCandidate);
                long scoreSub = ScoreFilteredRow(filteredCandidate2);
                bestFilter = scoreSub < scoreNone ? (byte)1 : (byte)0;
            }
            else
            {
                bestFilter = 0;
                long bestScore = long.MaxValue;

                for (byte filter = 0; filter <= 4; filter++)
                {
                    FilterRow(row, prev, filteredCandidate, bytesPerPixel, filter);
                    long score = ScoreFilteredRow(filteredCandidate);

                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestFilter = filter;
                    }
                }
            }

            int dstRow = y * (rowBytes + 1);
            output[dstRow] = bestFilter;
            FilterRow(row, prev, output.AsSpan(dstRow + 1, rowBytes), bytesPerPixel, bestFilter);
            row.CopyTo(prev);
        }

        return output;
    }

    public static void FilterRow(ReadOnlySpan<byte> row, ReadOnlySpan<byte> prev, Span<byte> dst, int bytesPerPixel, byte filter)
    {
        switch (filter)
        {
            case 0:
                row.CopyTo(dst);
                return;

            case 1:
                for (int i = 0; i < row.Length; i++)
                {
                    byte left = i >= bytesPerPixel ? row[i - bytesPerPixel] : (byte)0;
                    dst[i] = unchecked((byte)(row[i] - left));
                }
                return;

            case 2:
                for (int i = 0; i < row.Length; i++)
                    dst[i] = unchecked((byte)(row[i] - prev[i]));
                return;

            case 3:
                for (int i = 0; i < row.Length; i++)
                {
                    byte left = i >= bytesPerPixel ? row[i - bytesPerPixel] : (byte)0;
                    byte up = prev[i];
                    dst[i] = unchecked((byte)(row[i] - ((left + up) >> 1)));
                }
                return;

            case 4:
                for (int i = 0; i < row.Length; i++)
                {
                    int a = i >= bytesPerPixel ? row[i - bytesPerPixel] : 0;
                    int b = prev[i];
                    int c = i >= bytesPerPixel ? prev[i - bytesPerPixel] : 0;
                    dst[i] = unchecked((byte)(row[i] - PngScanlineProcessor.PaethPredictor(a, b, c)));
                }
                return;

            default:
                throw new NotSupportedException($"Unsupported PNG filter type {filter}.");
        }
    }

    public static long ScoreFilteredRow(ReadOnlySpan<byte> row)
    {
        long score = 0;
        for (int i = 0; i < row.Length; i++)
        {
            int v = row[i];
            score += v < 128 ? v : 256 - v;
        }
        return score;
    }

    public static int PaethPredictor(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a);
        int pb = Math.Abs(p - b);
        int pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc)
            return a;
        if (pb <= pc)
            return b;
        return c;
    }

    public static int ReadPackedSample(ReadOnlySpan<byte> row, int pixelIndex, int bitDepth)
    {
        int samplesPerByte = 8 / bitDepth;
        int byteIndex = pixelIndex / samplesPerByte;
        int sampleInByte = pixelIndex % samplesPerByte;
        int shift = 8 - bitDepth * (sampleInByte + 1);
        int mask = (1 << bitDepth) - 1;
        return (row[byteIndex] >> shift) & mask;
    }

    public static byte ScaleSampleToByte(int value, int bitDepth)
    {
        if (bitDepth == 8)
            return (byte)value;
        if (bitDepth == 16)
            return (byte)(value >> 8);
        int max = (1 << bitDepth) - 1;
        return (byte)((value * 255 + (max / 2)) / max);
    }

    public static void WriteRgba(Span<byte> rgba, int width, int x, int y, byte r, byte g, byte b, byte a)
    {
        int i = (y * width + x) * 4;
        rgba[i + 0] = r;
        rgba[i + 1] = g;
        rgba[i + 2] = b;
        rgba[i + 3] = a;
    }

    public static byte[] DecodePixels(in PngHeader header, byte[] compressedData, byte[]? palette, byte[]? transparency)
    {
        var inflated = PngDecompressor.InflateZlib(compressedData);
        var rgba = new byte[header.Width * header.Height * 4];

        if (header.InterlaceMethod == 0)
        {
            var ctx = new PngDecodePassContext(header, inflated, 0, header.Width, header.Height, 0, 0, 1, 1, rgba, palette, transparency);
            DecodePass(ctx, out int consumedBytes);
            if (consumedBytes != inflated.Length)
                throw new InvalidDataException("Unexpected trailing data in non-interlaced PNG image data.");
            return rgba;
        }

        int cursor = 0;
        for (int pass = 0; pass < 7; pass++)
        {
            int passWidth = PngFormatValidator.ComputePassDimension(header.Width, PngConstants.Adam7StartX[pass], PngConstants.Adam7StepX[pass]);
            int passHeight = PngFormatValidator.ComputePassDimension(header.Height, PngConstants.Adam7StartY[pass], PngConstants.Adam7StepY[pass]);
            if (passWidth == 0 || passHeight == 0)
                continue;
            var ctx = new PngDecodePassContext(
                header,
                inflated,
                cursor,
                passWidth,
                passHeight,
                PngConstants.Adam7StartX[pass],
                PngConstants.Adam7StartY[pass],
                PngConstants.Adam7StepX[pass],
                PngConstants.Adam7StepY[pass],
                rgba,
                palette,
                transparency);
            DecodePass(ctx, out int consumedPassBytes);
            cursor += consumedPassBytes;
        }
        if (cursor != inflated.Length)
            throw new InvalidDataException("Unexpected trailing data in Adam7 PNG image data.");
        return rgba;
    }

    public static void DecodePass(in PngDecodePassContext ctx, out int consumedBytes)
    {
        int channels = PngFormatValidator.GetChannelCount(ctx.Header.ColorType);
        int bitsPerPixel = channels * ctx.Header.BitDepth;
        int bytesPerPixel = Math.Max(1, (bitsPerPixel + 7) / 8);
        int rowBytes = (ctx.PassWidth * bitsPerPixel + 7) / 8;

        var prevRow = new byte[rowBytes];
        var curRow = new byte[rowBytes];
        int cursor = ctx.StartOffset;
        for (int row = 0; row < ctx.PassHeight; row++)
        {
            if (cursor >= ctx.Inflated.Length)
                throw new InvalidDataException("Unexpected end of PNG image data.");
            byte filterType = ctx.Inflated[cursor++];
            if (cursor + rowBytes > ctx.Inflated.Length)
                throw new InvalidDataException("PNG image data row exceeds stream length.");
            ctx.Inflated.Slice(cursor, rowBytes).CopyTo(curRow);
            cursor += rowBytes;
            UnfilterRow(curRow, prevRow, bytesPerPixel, filterType);
            // WriteDecodedRow is assumed to be in another helper or can be added here if needed
            // WriteDecodedRow(ctx, curRow, row);
            (prevRow, curRow) = (curRow, prevRow);
        }
        consumedBytes = cursor - ctx.StartOffset;
    }
}
