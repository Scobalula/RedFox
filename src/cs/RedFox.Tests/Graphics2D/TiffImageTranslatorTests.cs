using System.Buffers.Binary;
using RedFox.Graphics2D;
using RedFox.Graphics2D.IO;
using RedFox.Graphics2D.Tiff;

namespace RedFox.Tests.Graphics2D;

public sealed class TiffImageTranslatorTests
{
    private const ushort TagPlanarConfiguration = 284;
    private const ushort TagPredictor = 317;
    private const ushort TagTileWidth = 322;
    private const ushort TagTileLength = 323;
    private const ushort TagTileOffsets = 324;
    private const ushort TagTileByteCounts = 325;
    private const ushort CompressionDeflate = 8;
    private const ushort CompressionAdobeDeflate = 32946;

    [Fact]
    public void TiffImageTranslator_IsValidRecognizesTiffMagic()
    {
        TiffImageTranslator translator = new();
        byte[] littleEndianHeader = [(byte)'I', (byte)'I', 42, 0];
        byte[] bigEndianHeader = [(byte)'M', (byte)'M', 0, 42];
        byte[] invalidHeader = [0, 1, 2, 3];

        Assert.True(translator.IsValid(littleEndianHeader, "texture.tiff", ".tiff"));
        Assert.True(translator.IsValid(bigEndianHeader, "texture.tif", ".tif"));
        Assert.False(translator.IsValid(invalidHeader, "texture.tiff", ".tiff"));
    }

    [Theory]
    [InlineData(TiffCompression.None)]
    [InlineData(TiffCompression.Deflate)]
    [InlineData(TiffCompression.LZW)]
    [InlineData(TiffCompression.PackBits)]
    public void TiffTranslator_RgbaRoundTrip_ViaManager_PreservesImageData(TiffCompression compression)
    {
        byte[] sourcePixels =
        [
            255, 0, 0, 255,
            0, 255, 0, 128,
            0, 0, 255, 64,
            255, 255, 255, 0,
        ];

        Image source = new(2, 2, ImageFormat.R8G8B8A8Unorm, sourcePixels);
        ImageTranslatorManager manager = CreateManagerWithTiffTranslator(compression);
        byte[] encoded = WriteImageWithManager(manager, source, "texture.tiff");
        TiffTestMetadata metadata = ReadMetadata(encoded);
        Image loaded = ReadImageWithManager(manager, encoded, "texture.tiff");

        Assert.Equal((ushort)compression, metadata.Compression);
        Assert.Equal(TiffConstants.PhotometricRGB, metadata.Photometric);
        Assert.Equal((ushort)4, metadata.SamplesPerPixel);
        Assert.Equal([8u, 8u, 8u, 8u], metadata.BitsPerSample);
        Assert.Equal(ImageFormat.R8G8B8A8Unorm, loaded.Format);
        Assert.Equal(source.PixelData.ToArray(), loaded.PixelData.ToArray());
    }

    [Theory]
    [InlineData(ImageCompressionPreference.None, TiffCompression.None, TiffPredictor.None)]
    [InlineData(ImageCompressionPreference.Fast, TiffCompression.PackBits, TiffPredictor.None)]
    [InlineData(ImageCompressionPreference.Balanced, TiffCompression.Deflate, TiffPredictor.Horizontal)]
    [InlineData(ImageCompressionPreference.SmallestSize, TiffCompression.Deflate, TiffPredictor.Horizontal)]
    public void TiffTranslator_RgbaRoundTrip_WithGenericWriteOptions_PreservesImageData(ImageCompressionPreference compressionPreference, TiffCompression expectedCompression, TiffPredictor expectedPredictor)
    {
        byte[] sourcePixels =
        [
            255, 0, 0, 255,
            0, 255, 0, 128,
            0, 0, 255, 64,
            255, 255, 255, 0,
        ];

        Image source = new(2, 2, ImageFormat.R8G8B8A8Unorm, sourcePixels);
        ImageTranslatorManager manager = ImageTranslatorTestHarness.CreateManager(new TiffImageTranslator());
        ImageTranslatorOptions options = new()
        {
            Compression = compressionPreference,
        };
        byte[] encoded = ImageTranslatorTestHarness.WriteImageWithManager(manager, source, "generic-options.tiff", options);
        TiffTestMetadata metadata = ReadMetadata(encoded);
        Image loaded = ImageTranslatorTestHarness.ReadImageWithManager(manager, encoded, "generic-options.tiff");

        Assert.Equal((ushort)expectedCompression, metadata.Compression);
        Assert.Equal((ushort)expectedPredictor, metadata.Predictor);
        Assert.Equal(source.PixelData.ToArray(), loaded.PixelData.ToArray());
    }

    [Theory]
    [InlineData(TiffCompression.Deflate)]
    [InlineData(TiffCompression.LZW)]
    public void TiffTranslator_Write_WithHorizontalPredictor_SetsPredictorTagAndRoundTrips(TiffCompression compression)
    {
        byte[] sourcePixels =
        [
            255, 0, 0, 255,
            0, 255, 0, 255,
            0, 0, 255, 255,
            255, 255, 0, 255,
        ];

        Image source = new(2, 2, ImageFormat.R8G8B8A8Unorm, sourcePixels);
        ImageTranslatorManager manager = CreateManagerWithTiffTranslator(compression, TiffPredictor.Horizontal);
        byte[] encoded = WriteImageWithManager(manager, source, "predictor.tiff");
        TiffTestMetadata metadata = ReadMetadata(encoded);
        Image loaded = ReadImageWithManager(manager, encoded, "predictor.tiff");

        Assert.Equal((ushort)compression, metadata.Compression);
        Assert.Equal((ushort)TiffPredictor.Horizontal, metadata.Predictor);
        Assert.Equal(source.PixelData.ToArray(), loaded.PixelData.ToArray());
    }

    [Fact]
    public void TiffTranslator_Write_R8Unorm_UsesSingleChannelGrayscaleMetadata()
    {
        byte[] sourcePixels = [0, 85, 170, 255];
        Image source = new(2, 2, ImageFormat.R8Unorm, sourcePixels);
        ImageTranslatorManager manager = CreateManagerWithTiffTranslator(TiffCompression.None);
        byte[] encoded = WriteImageWithManager(manager, source, "grayscale.tiff");
        TiffTestMetadata metadata = ReadMetadata(encoded);
        Image loaded = ReadImageWithManager(manager, encoded, "grayscale.tiff");

        Assert.Equal((ushort)1, metadata.SamplesPerPixel);
        Assert.Equal(TiffConstants.PhotometricMinIsBlack, metadata.Photometric);
        Assert.Equal([8u], metadata.BitsPerSample);
        Assert.Equal(ExpandGrayscale8ToRgba8(sourcePixels), loaded.PixelData.ToArray());
    }

    [Fact]
    public void TiffTranslator_Write_R16Unorm_PreservesSixteenBitGrayscaleMetadata()
    {
        byte[] sourcePixels =
        [
            0x00, 0x00,
            0x00, 0x40,
            0x00, 0x80,
            0xFF, 0xFF,
        ];

        Image source = new(2, 2, ImageFormat.R16Unorm, sourcePixels);
        ImageTranslatorManager manager = CreateManagerWithTiffTranslator(TiffCompression.None);
        byte[] encoded = WriteImageWithManager(manager, source, "grayscale16.tiff");
        TiffTestMetadata metadata = ReadMetadata(encoded);
        Image loaded = ReadImageWithManager(manager, encoded, "grayscale16.tiff");

        Assert.Equal((ushort)1, metadata.SamplesPerPixel);
        Assert.Equal(TiffConstants.PhotometricMinIsBlack, metadata.Photometric);
        Assert.Equal([16u], metadata.BitsPerSample);
        Assert.Equal(ExpandGrayscale16ToRgba8(sourcePixels), loaded.PixelData.ToArray());
    }

    [Fact]
    public void TiffTranslator_Write_R16G16B16A16Unorm_PreservesSixteenBitChannelMetadata()
    {
        byte[] sourcePixels =
        [
            0x00, 0x00, 0xFF, 0x00, 0xFF, 0x7F, 0xFF, 0xFF,
            0xFF, 0xFF, 0x80, 0x00, 0x40, 0x00, 0x20, 0x00,
        ];

        Image source = new(2, 1, ImageFormat.R16G16B16A16Unorm, sourcePixels);
        ImageTranslatorManager manager = CreateManagerWithTiffTranslator(TiffCompression.None);
        byte[] encoded = WriteImageWithManager(manager, source, "rgba16.tiff");
        TiffTestMetadata metadata = ReadMetadata(encoded);
        Image loaded = ReadImageWithManager(manager, encoded, "rgba16.tiff");

        Assert.Equal((ushort)4, metadata.SamplesPerPixel);
        Assert.Equal(TiffConstants.PhotometricRGB, metadata.Photometric);
        Assert.Equal([16u, 16u, 16u, 16u], metadata.BitsPerSample);
        Assert.Equal(ExpandRgba16ToRgba8(sourcePixels), loaded.PixelData.ToArray());
    }

    [Fact]
    public void TiffSamples_ReadAcrossCorpus_DoesNotThrowAndProducesPixels()
    {
        string[] tiffFiles = GetRequiredTiffFiles();
        if (tiffFiles.Length == 0)
        {
            return;
        }

        ImageTranslatorManager manager = CreateManagerWithTiffTranslator(TiffCompression.None);
        List<string> failures = [];

        foreach (string tiffFile in tiffFiles)
        {
            try
            {
                using FileStream inputFileStream = File.OpenRead(tiffFile);
                Image image = manager.Read(inputFileStream, tiffFile);

                Assert.True(image.Width > 0, $"Expected positive width for '{tiffFile}'.");
                Assert.True(image.Height > 0, $"Expected positive height for '{tiffFile}'.");
                Assert.Equal(ImageFormat.R8G8B8A8Unorm, image.Format);
                Assert.Equal(image.Width * image.Height * 4, image.PixelData.Length);
            }
            catch (Exception exception)
            {
                failures.Add($"{Path.GetFileName(tiffFile)} :: {DescribeFailureContext(tiffFile)} :: {exception.GetType().Name}: {exception.Message}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static ImageTranslatorManager CreateManagerWithTiffTranslator(TiffCompression compression)
    {
        return CreateManagerWithTiffTranslator(compression, TiffPredictor.None);
    }

    private static ImageTranslatorManager CreateManagerWithTiffTranslator(TiffCompression compression, TiffPredictor predictor)
    {
        TiffImageTranslator translator = new()
        {
            EncoderOptions = new TiffEncoderOptions
            {
                Compression = compression,
                Predictor = predictor,
            },
        };

        ImageTranslatorManager manager = new();
        manager.Register(translator);
        return manager;
    }

    private static byte[] WriteImageWithManager(ImageTranslatorManager manager, Image image, string sourcePath)
    {
        using MemoryStream stream = new();
        manager.Write(stream, sourcePath, image);
        return stream.ToArray();
    }

    private static Image ReadImageWithManager(ImageTranslatorManager manager, byte[] data, string sourcePath)
    {
        using MemoryStream stream = new(data, false);
        return manager.Read(stream, sourcePath);
    }

    private static string[] GetRequiredTiffFiles()
    {
        string? testsRoot = Environment.GetEnvironmentVariable("REDFOX_TESTS_DIR");
        if (string.IsNullOrWhiteSpace(testsRoot))
        {
            return [];
        }

        string tiffDirectory = Path.Combine(testsRoot, "Input", "Tiff");
        if (!Directory.Exists(tiffDirectory))
        {
            return [];
        }

        List<string> tiffFiles = [];
        tiffFiles.AddRange(Directory.GetFiles(tiffDirectory, "*.tif", SearchOption.AllDirectories));
        tiffFiles.AddRange(Directory.GetFiles(tiffDirectory, "*.tiff", SearchOption.AllDirectories));
        tiffFiles.Sort(StringComparer.OrdinalIgnoreCase);
        return [.. tiffFiles];
    }

    private static string DescribeFailureContext(string tiffFile)
    {
        try
        {
            byte[] data = File.ReadAllBytes(tiffFile);
            TiffTestMetadata metadata = ReadMetadata(data);
            string compression = metadata.Compression switch
            {
                TiffConstants.CompressionNone => "none",
                (ushort)TiffCompression.LZW => "lzw",
                (ushort)TiffCompression.PackBits => "packbits",
                CompressionDeflate => "deflate",
                CompressionAdobeDeflate => "adobe-deflate",
                _ => metadata.Compression.ToString(),
            };

            return $"compression={compression}, samples={metadata.SamplesPerPixel}, bits=[{string.Join(",", metadata.BitsPerSample)}], photometric={metadata.Photometric}, predictor={metadata.Predictor}, planar={metadata.PlanarConfiguration}, tiled={metadata.IsTiled}";
        }
        catch (Exception exception)
        {
            return $"metadata-unavailable ({exception.GetType().Name}: {exception.Message})";
        }
    }

    private static TiffTestMetadata ReadMetadata(ReadOnlySpan<byte> data)
    {
        bool littleEndian = data.Length >= 2 && data[0] == (byte)'I' && data[1] == (byte)'I';
        uint ifdOffset = TiffIfdReader.ReadUInt32(data, 4, littleEndian);
        TiffIfdEntry[] tags = TiffIfdReader.ParseIFD(data, ifdOffset, littleEndian);
        uint[] bitsPerSample = TiffIfdReader.GetTagUintArray(tags, TiffConstants.TagBitsPerSample, data, littleEndian);

        if (bitsPerSample.Length == 0)
        {
            bitsPerSample = [8];
        }

        return new TiffTestMetadata
        {
            Compression = (ushort)TiffIfdReader.GetTagInt(tags, TiffConstants.TagCompression, data, littleEndian, TiffConstants.CompressionNone),
            Photometric = (ushort)TiffIfdReader.GetTagInt(tags, TiffConstants.TagPhotometric, data, littleEndian, TiffConstants.PhotometricRGB),
            SamplesPerPixel = (ushort)TiffIfdReader.GetTagInt(tags, TiffConstants.TagSamplesPerPixel, data, littleEndian, 1),
            BitsPerSample = bitsPerSample,
            Predictor = (ushort)TiffIfdReader.GetTagInt(tags, TagPredictor, data, littleEndian, 1),
            PlanarConfiguration = (ushort)TiffIfdReader.GetTagInt(tags, TagPlanarConfiguration, data, littleEndian, 1),
            IsTiled = HasTag(tags, TagTileWidth) || HasTag(tags, TagTileLength) || HasTag(tags, TagTileOffsets) || HasTag(tags, TagTileByteCounts),
        };
    }

    private static bool HasTag(ReadOnlySpan<TiffIfdEntry> tags, ushort tag)
    {
        foreach (TiffIfdEntry entry in tags)
        {
            if (entry.Tag == tag)
            {
                return true;
            }
        }

        return false;
    }

    private static byte[] ExpandGrayscale8ToRgba8(ReadOnlySpan<byte> source)
    {
        byte[] result = new byte[source.Length * 4];
        for (int i = 0; i < source.Length; i++)
        {
            int destinationOffset = i * 4;
            result[destinationOffset + 0] = source[i];
            result[destinationOffset + 1] = source[i];
            result[destinationOffset + 2] = source[i];
            result[destinationOffset + 3] = 255;
        }

        return result;
    }

    private static byte[] ExpandGrayscale16ToRgba8(ReadOnlySpan<byte> source)
    {
        int pixelCount = source.Length / 2;
        byte[] result = new byte[pixelCount * 4];

        for (int i = 0; i < pixelCount; i++)
        {
            byte value = (byte)(BinaryPrimitives.ReadUInt16LittleEndian(source[(i * 2)..]) >> 8);
            int destinationOffset = i * 4;
            result[destinationOffset + 0] = value;
            result[destinationOffset + 1] = value;
            result[destinationOffset + 2] = value;
            result[destinationOffset + 3] = 255;
        }

        return result;
    }

    private static byte[] ExpandRgba16ToRgba8(ReadOnlySpan<byte> source)
    {
        int pixelCount = source.Length / 8;
        byte[] result = new byte[pixelCount * 4];

        for (int i = 0; i < pixelCount; i++)
        {
            int sourceOffset = i * 8;
            int destinationOffset = i * 4;
            result[destinationOffset + 0] = (byte)(BinaryPrimitives.ReadUInt16LittleEndian(source[sourceOffset..]) >> 8);
            result[destinationOffset + 1] = (byte)(BinaryPrimitives.ReadUInt16LittleEndian(source[(sourceOffset + 2)..]) >> 8);
            result[destinationOffset + 2] = (byte)(BinaryPrimitives.ReadUInt16LittleEndian(source[(sourceOffset + 4)..]) >> 8);
            result[destinationOffset + 3] = (byte)(BinaryPrimitives.ReadUInt16LittleEndian(source[(sourceOffset + 6)..]) >> 8);
        }

        return result;
    }

    private sealed class TiffTestMetadata
    {
        public required ushort Compression { get; init; }

        public required ushort Photometric { get; init; }

        public required ushort SamplesPerPixel { get; init; }

        public required uint[] BitsPerSample { get; init; }

        public required ushort Predictor { get; init; }

        public required ushort PlanarConfiguration { get; init; }

        public required bool IsTiled { get; init; }
    }
}