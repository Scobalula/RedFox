using System;
using System.Collections.Generic;
using System.Numerics;
using RedFox.Graphics2D.Codecs;

namespace RedFox.Graphics2D.Png;

internal static class PngEncoderHelper
{
    public static byte[] ExtractRgba8(in ImageSlice slice, ImageFormat format)
    {
        int width = slice.Width;
        int height = slice.Height;
        int rowBytes = width * 4;
        var rgba = new byte[rowBytes * height];
        var src = slice.PixelSpan;

        if (format is ImageFormat.R8G8B8A8Unorm or ImageFormat.R8G8B8A8UnormSrgb)
        {
            for (int y = 0; y < height; y++)
                src.Slice(y * slice.RowPitch, rowBytes).CopyTo(rgba.AsSpan(y * rowBytes, rowBytes));
            return rgba;
        }

        if (format is ImageFormat.B8G8R8A8Unorm or ImageFormat.B8G8R8A8UnormSrgb or ImageFormat.B8G8R8X8Unorm or ImageFormat.B8G8R8X8UnormSrgb)
        {
            bool forceOpaque = format is ImageFormat.B8G8R8X8Unorm or ImageFormat.B8G8R8X8UnormSrgb;

            for (int y = 0; y < height; y++)
            {
                int srcRow = y * slice.RowPitch;
                int dstRow = y * rowBytes;

                for (int x = 0; x < width; x++)
                {
                    int s = srcRow + x * 4;
                    int d = dstRow + x * 4;
                    rgba[d + 0] = src[s + 2];
                    rgba[d + 1] = src[s + 1];
                    rgba[d + 2] = src[s + 0];
                    rgba[d + 3] = forceOpaque ? (byte)255 : src[s + 3];
                }
            }

            return rgba;
        }

        if (!PixelCodecRegistry.TryGetCodec(format, out var codec) || codec is null)
            throw new NotSupportedException($"PNG writing is not supported for format {format}.");

        const int stripHeight = 4;
        var pixels = new Vector4[width * stripHeight];

        for (int stripY = 0; stripY < height; stripY += stripHeight)
        {
            int rows = Math.Min(stripHeight, height - stripY);
            codec.DecodeRows(src, pixels, stripY, rows, width, height);

            for (int row = 0; row < rows; row++)
            {
                int pixelBase = row * width;
                int dstRow = (stripY + row) * rowBytes;

                for (int x = 0; x < width; x++)
                {
                    Vector4 pixel = pixels[pixelBase + x];
                    int d = dstRow + x * 4;
                    rgba[d + 0] = (byte)(Math.Clamp(pixel.X, 0f, 1f) * 255f + 0.5f);
                    rgba[d + 1] = (byte)(Math.Clamp(pixel.Y, 0f, 1f) * 255f + 0.5f);
                    rgba[d + 2] = (byte)(Math.Clamp(pixel.Z, 0f, 1f) * 255f + 0.5f);
                    rgba[d + 3] = (byte)(Math.Clamp(pixel.W, 0f, 1f) * 255f + 0.5f);
                }
            }
        }

        return rgba;
    }

    public static PngColorModelInfo AnalyzeColorModel(ReadOnlySpan<byte> rgba, bool allowPaletteAnalysis)
    {
        bool grayscale = true;
        bool opaque = true;

        var palette = allowPaletteAnalysis ? new Dictionary<uint, int>(capacity: 256) : null;
        var paletteColors = allowPaletteAnalysis ? new List<uint>(capacity: 256) : null;
        bool canPalette = allowPaletteAnalysis;

        for (int i = 0; i < rgba.Length; i += 4)
        {
            byte r = rgba[i + 0];
            byte g = rgba[i + 1];
            byte b = rgba[i + 2];
            byte a = rgba[i + 3];

            if (grayscale && !(r == g && g == b))
                grayscale = false;
            if (opaque && a != 255)
                opaque = false;

            if (canPalette)
            {
                uint key = (uint)(r | (g << 8) | (b << 16) | (a << 24));
                if (!palette!.ContainsKey(key))
                {
                    if (palette.Count >= 256)
                    {
                        canPalette = false;
                    }
                    else
                    {
                        palette[key] = palette.Count;
                        paletteColors!.Add(key);
                    }
                }
            }
        }

        return new PngColorModelInfo(
            grayscale,
            opaque,
            canPalette,
            palette ?? [],
            paletteColors ?? []);
    }

    public static PngWriteSelection SelectWriteMode(in PngColorModelInfo info)
    {
        if (info.CanPalette && info.PaletteColors.Count > 0 && info.PaletteColors.Count <= 256)
        {
            int paletteCount = info.PaletteColors.Count;
            byte bitDepth = paletteCount <= 2 ? (byte)1 : paletteCount <= 4 ? (byte)2 : paletteCount <= 16 ? (byte)4 : (byte)8;

            var plte = new byte[paletteCount * 3];
            byte lastTransparent = 0;
            bool anyTransparent = false;

            for (int i = 0; i < paletteCount; i++)
            {
                uint c = info.PaletteColors[i];
                byte r = (byte)(c & 0xFF);
                byte g = (byte)((c >> 8) & 0xFF);
                byte b = (byte)((c >> 16) & 0xFF);
                byte a = (byte)((c >> 24) & 0xFF);

                int p = i * 3;
                plte[p + 0] = r;
                plte[p + 1] = g;
                plte[p + 2] = b;

                if (a != 255)
                {
                    anyTransparent = true;
                    lastTransparent = (byte)i;
                }
            }

            byte[]? trns = null;
            if (anyTransparent)
            {
                trns = new byte[lastTransparent + 1];
                for (int i = 0; i < trns.Length; i++)
                {
                    uint c = info.PaletteColors[i];
                    trns[i] = (byte)((c >> 24) & 0xFF);
                }
            }

            return new PngWriteSelection(
                ColorType: 3,
                BitDepth: bitDepth,
                Palette: plte,
                PaletteAlpha: trns,
                PaletteIndices: info.PaletteLookup);
        }

        if (info.IsGrayscale)
        {
            return info.IsOpaque
                ? new PngWriteSelection(ColorType: 0, BitDepth: 8, Palette: null, PaletteAlpha: null, PaletteIndices: null)
                : new PngWriteSelection(ColorType: 4, BitDepth: 8, Palette: null, PaletteAlpha: null, PaletteIndices: null);
        }

        return info.IsOpaque
            ? new PngWriteSelection(ColorType: 2, BitDepth: 8, Palette: null, PaletteAlpha: null, PaletteIndices: null)
            : new PngWriteSelection(ColorType: 6, BitDepth: 8, Palette: null, PaletteAlpha: null, PaletteIndices: null);
    }

    public static byte[] BuildScanlineData(ReadOnlySpan<byte> rgba, int width, int height, in PngWriteSelection selection)
    {
        int rowBytes = selection.ColorType switch
        {
            0 => width,
            2 => width * 3,
            3 => (width * selection.BitDepth + 7) / 8,
            4 => width * 2,
            6 => width * 4,
            _ => throw new NotSupportedException($"Unsupported PNG color type {selection.ColorType}.")
        };

        var output = new byte[rowBytes * height];

        for (int y = 0; y < height; y++)
        {
            int srcRow = y * width * 4;
            int dstRow = y * rowBytes;

            switch (selection.ColorType)
            {
                case 0:
                    for (int x = 0; x < width; x++)
                        output[dstRow + x] = rgba[srcRow + x * 4 + 0];
                    break;

                case 2:
                    for (int x = 0; x < width; x++)
                    {
                        int s = srcRow + x * 4;
                        int d = dstRow + x * 3;
                        output[d + 0] = rgba[s + 0];
                        output[d + 1] = rgba[s + 1];
                        output[d + 2] = rgba[s + 2];
                    }
                    break;

                case 3:
                    BuildIndexedRow(rgba, srcRow, width, output.AsSpan(dstRow, rowBytes), selection);
                    break;

                case 4:
                    for (int x = 0; x < width; x++)
                    {
                        int s = srcRow + x * 4;
                        int d = dstRow + x * 2;
                        output[d + 0] = rgba[s + 0];
                        output[d + 1] = rgba[s + 3];
                    }
                    break;

                case 6:
                    rgba.Slice(srcRow, width * 4).CopyTo(output.AsSpan(dstRow, width * 4));
                    break;
            }
        }

        return output;
    }

    public static void BuildIndexedRow(ReadOnlySpan<byte> rgba, int srcRow, int width, Span<byte> dstRow, in PngWriteSelection selection)
    {
        if (selection.PaletteIndices is null)
            throw new InvalidOperationException("Indexed row generation requires palette lookup table.");

        if (selection.BitDepth == 8)
        {
            for (int x = 0; x < width; x++)
            {
                int s = srcRow + x * 4;
                uint key = (uint)(rgba[s + 0] | (rgba[s + 1] << 8) | (rgba[s + 2] << 16) | (rgba[s + 3] << 24));
                dstRow[x] = (byte)selection.PaletteIndices[key];
            }
            return;
        }

        int mask = (1 << selection.BitDepth) - 1;
        int samplesPerByte = 8 / selection.BitDepth;

        for (int x = 0; x < width; x++)
        {
            int s = srcRow + x * 4;
            uint key = (uint)(rgba[s + 0] | (rgba[s + 1] << 8) | (rgba[s + 2] << 16) | (rgba[s + 3] << 24));
            int index = selection.PaletteIndices[key] & mask;

            int byteIndex = x / samplesPerByte;
            int sampleInByte = x % samplesPerByte;
            int shift = 8 - selection.BitDepth * (sampleInByte + 1);
            dstRow[byteIndex] |= (byte)(index << shift);
        }
    }
}
