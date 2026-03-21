// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using RedFox.Graphics2D;
using RedFox.Graphics2D.Codecs;
using RedFox.Graphics2D.IO;

namespace RedFox.Tests.Graphics2D;

public sealed class DdsImageTranslatorTests
{
    [Fact]
    public void DdsTranslator_RoundTripViaManager_PreservesImageData()
    {
        byte[] sourcePixels =
        [
            255, 0, 0, 255,
            0, 255, 0, 255,
            0, 0, 255, 255,
            255, 255, 255, 255,
        ];

        Image source = new(2, 2, ImageFormat.R8G8B8A8Unorm, sourcePixels);
        ImageTranslatorManager manager = CreateManagerWithDdsTranslator();
        using MemoryStream stream = new();
        manager.Write(stream, "texture.dds", source);
        stream.Position = 0;
        Image loaded = manager.Read(stream, "texture.dds");

        Assert.Equal(source.Width, loaded.Width);
        Assert.Equal(source.Height, loaded.Height);
        Assert.Equal(source.Depth, loaded.Depth);
        Assert.Equal(source.ArraySize, loaded.ArraySize);
        Assert.Equal(source.MipLevels, loaded.MipLevels);
        Assert.Equal(source.Format, loaded.Format);
        Assert.Equal(source.IsCubemap, loaded.IsCubemap);
        Assert.Equal(source.PixelData.ToArray(), loaded.PixelData.ToArray());
    }

    [Fact]
    public void DdsImageTranslator_IsValidRecognizesDdsMagic()
    {
        DdsImageTranslator translator = new();
        byte[] validHeader = [0x44, 0x44, 0x53, 0x20];
        byte[] invalidHeader = [0x00, 0x01, 0x02, 0x03];

        Assert.True(translator.IsValid(validHeader, "texture.dds", ".dds"));
        Assert.False(translator.IsValid(invalidHeader, "texture.dds", ".dds"));
    }

    [Fact]
    public void DdsTranslator_ViaManager_InvalidCubemap_ThrowsInvalidDataException()
    {
        Image invalidCubemap = new(4, 4, depth: 1, arraySize: 5, mipLevels: 1, ImageFormat.R8G8B8A8Unorm, isCubemap: true);
        ImageTranslatorManager manager = CreateManagerWithDdsTranslator();
        using MemoryStream stream = new();

        Assert.Throws<InvalidDataException>(() => manager.Write(stream, "cubemap.dds", invalidCubemap));
    }

    [Fact]
    public void DdsSamples_DeterministicRewriteAcrossRoundTrips_ViaTranslatorManager()
    {
        string ddsDirectory = GetRequiredDdsDirectory();
        if (string.IsNullOrWhiteSpace(ddsDirectory))
        {
            return;
        }

        string[] ddsFiles = Directory.GetFiles(ddsDirectory, "*.dds", SearchOption.AllDirectories);
        if (ddsFiles.Length == 0)
        {
            return;
        }

        ImageTranslatorManager manager = CreateManagerWithDdsTranslator();
        foreach (string ddsFile in ddsFiles)
        {
            using FileStream inputFileStream = File.OpenRead(ddsFile);
            Image firstLoad = manager.Read(inputFileStream, ddsFile);
            byte[] firstWrite = WriteImageWithManager(manager, firstLoad, ddsFile);
            Image secondLoad = ReadImageWithManager(manager, firstWrite, ddsFile);
            byte[] secondWrite = WriteImageWithManager(manager, secondLoad, ddsFile);

            Assert.True(firstWrite.AsSpan().SequenceEqual(secondWrite), $"DDS rewrite changed bytes for '{ddsFile}'.");
            Assert.Equal(firstLoad.Width, secondLoad.Width);
            Assert.Equal(firstLoad.Height, secondLoad.Height);
            Assert.Equal(firstLoad.Depth, secondLoad.Depth);
            Assert.Equal(firstLoad.ArraySize, secondLoad.ArraySize);
            Assert.Equal(firstLoad.MipLevels, secondLoad.MipLevels);
            Assert.Equal(firstLoad.Format, secondLoad.Format);
            Assert.Equal(firstLoad.IsCubemap, secondLoad.IsCubemap);
            Assert.Equal(firstLoad.PixelData.ToArray(), secondLoad.PixelData.ToArray());
        }
    }

    [Theory]
    [InlineData(ImageFormat.R8G8B8A8Unorm)]
    [InlineData(ImageFormat.R16G16B16A16Float)]
    [InlineData(ImageFormat.R32G32B32A32Float)]
    public void DdsSamples_DecodeBaseline_ToCommonFormats(ImageFormat decodeTargetFormat)
    {
        string ddsDirectory = GetRequiredDdsDirectory();
        if (string.IsNullOrWhiteSpace(ddsDirectory))
        {
            return;
        }

        string[] ddsFiles = Directory.GetFiles(ddsDirectory, "*.dds", SearchOption.AllDirectories);
        if (ddsFiles.Length == 0)
        {
            return;
        }

        ImageTranslatorManager manager = CreateManagerWithDdsTranslator();
        int decodedSampleCount = 0;
        foreach (string ddsFile in ddsFiles)
        {
            using FileStream inputFileStream = File.OpenRead(ddsFile);
            Image image = manager.Read(inputFileStream, ddsFile);
            if (!PixelCodecRegistry.TryGetCodec(image.Format, out _))
            {
                continue;
            }

            Image decodeImage = CloneImage(image);
            decodeImage.Convert(decodeTargetFormat);
            Assert.Equal(decodeTargetFormat, decodeImage.Format);

            float[] decodedPixels = decodeImage.DecodeSlice<float>(0, 0, 0);
            Assert.True(decodedPixels.Length > 0);

            ref readonly ImageSlice slice = ref decodeImage.GetSlice(0, 0, 0);
            Assert.Equal(slice.Width * slice.Height * 4, decodedPixels.Length);
            decodedSampleCount++;
        }

        Assert.True(decodedSampleCount > 0, "No decodable DDS files were found for baseline decode coverage.");
    }

    private static ImageTranslatorManager CreateManagerWithDdsTranslator()
    {
        ImageTranslatorManager manager = new();
        manager.Register(new DdsImageTranslator());
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

    private static Image CloneImage(Image source)
    {
        return new Image(source.Width, source.Height, source.Depth, source.ArraySize, source.MipLevels, source.Format, source.IsCubemap, source.PixelData.ToArray());
    }

    private static string GetRequiredDdsDirectory()
    {
        string? testsRoot = Environment.GetEnvironmentVariable("REDFOX_TESTS_DIR");
        if (string.IsNullOrWhiteSpace(testsRoot))
        {
            return string.Empty;
        }

        string ddsDirectory = Path.Combine(testsRoot, "Input", "DDS");
        if (!Directory.Exists(ddsDirectory))
        {
            return string.Empty;
        }

        return ddsDirectory;
    }
}
