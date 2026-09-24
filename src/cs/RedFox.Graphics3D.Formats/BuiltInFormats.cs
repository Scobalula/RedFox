using RedFox.Graphics3D.Formats.ActorX;
using RedFox.Graphics3D.Formats.BiovisionHierarchy;
using RedFox.Graphics3D.Formats.Cast;
using RedFox.Graphics3D.Formats.GLTransmissionFormat;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Formats.KaydaraFbx;
using RedFox.Graphics3D.Formats.MayaAscii;
using RedFox.Graphics3D.Formats.IdTech;
using RedFox.Graphics3D.Formats.SEAnim;
using RedFox.Graphics3D.Formats.SEModel;
using RedFox.Graphics3D.Formats.StudioMDL;
using RedFox.Graphics3D.Formats.WavefrontObj;
using RedFox.Graphics3D.Formats.IwEngine;

namespace RedFox.Graphics3D.Formats;

/// <summary>
/// Provides explicit composition methods for the built-in RedFox Graphics3D scene translators.
/// </summary>
/// <remarks>
/// The methods in this class register translators in a deterministic order and do not use reflection,
/// assembly scanning, or module initializers. Applications can add custom translators to the returned
/// <see cref="SceneTranslatorManager"/> instance after calling either composition method.
/// </remarks>
public static class BuiltInFormats
{
    /// <summary>
    /// Registers every built-in scene translator with the specified translator manager.
    /// </summary>
    /// <param name="manager">
    /// The translator manager that will receive the built-in ActorX, BVH, Cast, FBX, glTF, Maya ASCII,
    /// MD5, SEAnim, SEMODEL, SMD, Wavefront OBJ, and XAsset translators.
    /// </param>
    /// <returns>
    /// The supplied <paramref name="manager"/> after all built-in translators have been registered.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="manager"/> is <see langword="null"/>.
    /// </exception>
    public static SceneTranslatorManager AddBuiltInFormats(this SceneTranslatorManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);
        return manager
            .Register<PskTranslator>()
            .Register<PsaTranslator>()
            .Register<BvhTranslator>()
            .Register<CastTranslator>()
            .Register<GltfTranslator>()
            .Register<FbxTranslator>()
            .Register<MayaAsciiTranslator>()
            .Register<Md5MeshTranslator>()
            .Register<Md5AnimTranslator>()
            .Register<SeanimTranslator>()
            .Register<SemodelTranslator>()
            .Register<SmdTranslator>()
            .Register<ObjTranslator>()
            .Register<XAnimTranslator>()
            .Register<XModelTranslator>();
    }

    /// <summary>
    /// Creates a new translator manager configured with every built-in scene translator.
    /// </summary>
    /// <returns>
    /// A new <see cref="SceneTranslatorManager"/> containing the built-in translators in deterministic order.
    /// </returns>
    public static SceneTranslatorManager CreateDefaultManager() => new SceneTranslatorManager().AddBuiltInFormats();
}
