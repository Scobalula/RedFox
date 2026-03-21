// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using RedFox.Graphics2D;
using RedFox.Graphics2D.IO;

namespace RedFox.Tests.IO;

public sealed class DdsTranslatorTests
{
    [Fact]
    public void DdsWriterAndLoader_RoundTripR8G8B8A8Unorm()
    {
        byte[] sourcePixels =
        [
            255, 0, 0, 255,
            0, 255, 0, 255,
            0, 0, 255, 255,
            255, 255, 255, 255,
        ];

        Image source = new(2, 2, ImageFormat.R8G8B8A8Unorm, sourcePixels);
        using MemoryStream stream = new();
        DDSWriter.Save(stream, source);
        stream.Position = 0;
        Image loaded = DDSLoader.Load(stream);

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
    public void DdsWriter_CubemapWithoutFaceMultiple_ThrowsInvalidDataException()
    {
        Image invalidCubemap = new(4, 4, depth: 1, arraySize: 5, mipLevels: 1, ImageFormat.R8G8B8A8Unorm, isCubemap: true);
        using MemoryStream stream = new();

        Assert.Throws<InvalidDataException>(() => DDSWriter.Save(stream, invalidCubemap));
    }
}
