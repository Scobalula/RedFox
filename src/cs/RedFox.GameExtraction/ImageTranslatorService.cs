using RedFox.Imaging.Formats.Exr;
using RedFox.Imaging.Formats.Dds;
using RedFox.Imaging.IO;
using RedFox.Imaging.Formats.Jpeg;
using RedFox.Imaging.Formats.Png;
using RedFox.Imaging.Formats.Tga;
using RedFox.Imaging.Formats.Tiff;

namespace RedFox.GameExtraction;

/// <summary>
/// 
/// </summary>
public class ImageTranslatorService
{
    /// <summary>
    /// Gets the manager responsible for handling scene translation operations.
    /// </summary>
    public ImageTranslatorManager Manager { get; } = new ImageTranslatorManager();

    /// <summary>
    /// Initializes a new instance of the ImageTranslatorService class.
    /// </summary>
    public ImageTranslatorService()
    {
        Manager.Register(new PngImageTranslator());
        Manager.Register(new DdsImageTranslator());
        Manager.Register(new TgaImageTranslator());
        Manager.Register(new TiffImageTranslator());
        Manager.Register(new JpegImageTranslator());
        Manager.Register(new ExrImageTranslator());
    }
}
