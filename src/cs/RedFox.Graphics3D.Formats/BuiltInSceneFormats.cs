using RedFox.Graphics3D.Formats.ActorX;
using RedFox.Graphics3D.Formats.BiovisionHierarchy;
using RedFox.Graphics3D.Formats.Cast;
using RedFox.Graphics3D.Formats.GLTransmissionFormat;
using RedFox.Graphics3D.Formats.IdTech;
using RedFox.Graphics3D.Formats.IwEngine;
using RedFox.Graphics3D.Formats.KaydaraFbx;
using RedFox.Graphics3D.Formats.MayaAscii;
using RedFox.Graphics3D.Formats.SEAnim;
using RedFox.Graphics3D.Formats.SEModel;
using RedFox.Graphics3D.Formats.StudioMDL;
using RedFox.Graphics3D.Formats.WavefrontObj;
using RedFox.Graphics3D.IO;

namespace RedFox.Graphics3D.Formats;

/// <summary>
/// Provides the explicit registration entry point for every scene translator shipped in this package.
/// </summary>
/// <remarks>
/// Registration is deterministic and does not use reflection, assembly scanning, or module initializers.
/// Custom translators can be registered on the same manager afterwards.
/// </remarks>
public static class BuiltInSceneFormats
{
    /// <summary>
    /// Registers a new instance of every built-in translator (ActorX, BVH, Cast, FBX, glTF, Maya ASCII,
    /// MD5, SEAnim, SEModel, SMD, Wavefront OBJ, and XAsset) with the given manager.
    /// Existing translators with the same names are replaced.
    /// </summary>
    /// <param name="manager">The manager to register the translators with.</param>
    public static void RegisterAll(SceneTranslatorManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);

        manager.Register(new PskTranslator());
        manager.Register(new PsaTranslator());
        manager.Register(new BvhTranslator());
        manager.Register(new CastTranslator());
        manager.Register(new GltfTranslator());
        manager.Register(new FbxTranslator());
        manager.Register(new MayaAsciiTranslator());
        manager.Register(new Md5MeshTranslator());
        manager.Register(new Md5AnimTranslator());
        manager.Register(new SeanimTranslator());
        manager.Register(new SemodelTranslator());
        manager.Register(new SmdTranslator());
        manager.Register(new ObjTranslator());
        manager.Register(new XAnimTranslator());
        manager.Register(new XModelTranslator());
    }
}
