using RedFox.Imaging.Formats.Bmp;
using RedFox.Imaging.Formats.Dds;
using RedFox.Imaging.Formats.Exr;
using RedFox.Imaging.IO;
using RedFox.Imaging.Formats.Jpeg;
using RedFox.Imaging.Formats.Ktx;
using RedFox.Imaging.Formats.Png;
using RedFox.Imaging.Formats.Tga;
using RedFox.Imaging.Formats.Tiff;

namespace RedFox.Samples.Examples;

internal static class SampleImageTranslatorRegistry
{
    internal static ImageTranslatorManager CreateDefaultManager()
    {
        ImageTranslatorManager manager = new();
        RegisterDefaults(manager);
        return manager;
    }

    internal static void RegisterDefaults(ImageTranslatorManager manager)
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
