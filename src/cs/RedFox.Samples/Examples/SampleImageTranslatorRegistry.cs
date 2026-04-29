using RedFox.Graphics2D.Bmp;
using RedFox.Graphics2D.Exr;
using RedFox.Graphics2D.IO;
using RedFox.Graphics2D.Jpeg;
using RedFox.Graphics2D.Ktx;
using RedFox.Graphics2D.Png;
using RedFox.Graphics2D.Tga;
using RedFox.Graphics2D.Tiff;

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