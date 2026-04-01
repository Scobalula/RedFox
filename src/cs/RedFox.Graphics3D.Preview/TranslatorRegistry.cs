using RedFox.Graphics2D.Bmp;
using RedFox.Graphics2D.Exr;
using RedFox.Graphics2D.IO;
using RedFox.Graphics2D.Jpeg;
using RedFox.Graphics2D.Ktx;
using RedFox.Graphics2D.Png;
using RedFox.Graphics2D.Tga;
using RedFox.Graphics2D.Tiff;
using RedFox.Graphics3D.Bvh;
using RedFox.Graphics3D.Gltf;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.KaydaraFbx;
using RedFox.Graphics3D.MayaAscii;
using RedFox.Graphics3D.Md5;
using RedFox.Graphics3D.Semodel;
using RedFox.Graphics3D.SEAnim;
using RedFox.Graphics3D.Smd;
using RedFox.Graphics3D.WavefrontObj;

namespace RedFox.Graphics3D.Preview;

public static class TranslatorRegistry
{
    public static SceneTranslatorManager CreateSceneTranslatorManager()
    {
        SceneTranslatorManager manager = new();
        manager.Register(new ObjTranslator());
        manager.Register(new GltfTranslator());
        manager.Register(new FbxTranslator());
        manager.Register(new MayaAsciiTranslator());
        manager.Register(new Md5MeshTranslator());
        manager.Register(new Md5AnimTranslator());
        manager.Register(new BvhTranslator());
        manager.Register(new SemodelTranslator());
        manager.Register(new SeanimTranslator());
        manager.Register(new SmdTranslator());
        return manager;
    }

    public static ImageTranslatorManager CreateImageTranslatorManager()
    {
        ImageTranslatorManager manager = new();
        manager.Register(new PngImageTranslator());
        manager.Register(new JpegImageTranslator());
        manager.Register(new DdsImageTranslator());
        manager.Register(new BmpImageTranslator());
        manager.Register(new TgaImageTranslator());
        manager.Register(new ExrImageTranslator());
        manager.Register(new TiffImageTranslator());
        manager.Register(new KtxImageTranslator());
        return manager;
    }
}
