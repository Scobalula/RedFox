using System;
using RedFox.Imaging.Formats.Bmp;
using RedFox.Imaging.Formats.Dds;
using RedFox.Imaging.Formats.Exr;
using RedFox.Imaging.Formats.Jpeg;
using RedFox.Imaging.Formats.Ktx;
using RedFox.Imaging.Formats.Png;
using RedFox.Imaging.Formats.Tga;
using RedFox.Imaging.Formats.Tiff;
using RedFox.Imaging.IO;

namespace RedFox.Imaging.Formats;

/// <summary>
/// Provides the explicit registration entry point for every image translator shipped in this package.
/// </summary>
public static class BuiltInImageFormats
{
    /// <summary>
    /// Registers a new instance of every built-in translator (BMP, DDS, EXR, JPEG, KTX, PNG, TGA, and TIFF) with the given manager.
    /// Existing translators with the same names are replaced.
    /// </summary>
    /// <param name="manager">The manager to register the translators with.</param>
    public static void RegisterAll(ImageTranslatorManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);

        manager.Register(new BmpImageTranslator());
        manager.Register(new DdsImageTranslator());
        manager.Register(new ExrImageTranslator());
        manager.Register(new JpegImageTranslator());
        manager.Register(new KtxImageTranslator());
        manager.Register(new PngImageTranslator());
        manager.Register(new TgaImageTranslator());
        manager.Register(new TiffImageTranslator());
    }
}
