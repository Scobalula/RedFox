using RedFox.Graphics2D.Exr;
using RedFox.Graphics2D.IO;
using RedFox.Graphics2D.Jpeg;
using RedFox.Graphics2D.Png;
using RedFox.Graphics2D.Tga;
using RedFox.Graphics2D.Tiff;
using RedFox.Graphics3D.Bvh;
using RedFox.Graphics3D.Cast;
using RedFox.Graphics3D.Gltf;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.KaydaraFbx;
using RedFox.Graphics3D.MayaAscii;
using RedFox.Graphics3D.Md5;
using RedFox.Graphics3D.SEAnim;
using RedFox.Graphics3D.Semodel;
using RedFox.Graphics3D.Smd;
using RedFox.Graphics3D.WavefrontObj;
using System;
using System.Collections.Generic;
using System.Text;

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
