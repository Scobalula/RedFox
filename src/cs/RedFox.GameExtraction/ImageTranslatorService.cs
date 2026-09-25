using RedFox.Imaging.Formats;
using RedFox.Imaging.IO;

namespace RedFox.GameExtraction;

/// <summary>
/// Owns the image translator manager used for texture import and export, pre-populated with the built-in formats.
/// </summary>
public class ImageTranslatorService
{
    /// <summary>
    /// Gets the manager responsible for image translation operations.
    /// </summary>
    public ImageTranslatorManager Manager { get; } = new ImageTranslatorManager();

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageTranslatorService"/> class and registers every built-in image format.
    /// </summary>
    public ImageTranslatorService()
    {
        BuiltInImageFormats.RegisterAll(Manager);
    }
}
