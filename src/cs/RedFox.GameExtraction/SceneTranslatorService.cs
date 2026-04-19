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
/// Provides access to scene translation functionality and manages the registration of scene translators.
/// </summary>
public class SceneTranslatorService
{
    /// <summary>
    /// Gets the manager responsible for handling scene translation operations.
    /// </summary>
    public SceneTranslatorManager Manager { get; } = new SceneTranslatorManager();

    /// <summary>
    /// Initializes a new instance of the SceneTranslatorService class.
    /// </summary>
    public SceneTranslatorService()
    {
        Manager.Register<FbxTranslator>();
        Manager.Register<MayaAsciiTranslator>();
        Manager.Register<ObjTranslator>();
        Manager.Register<SmdTranslator>();
        Manager.Register<SemodelTranslator>();
        Manager.Register<SeanimTranslator>();
        Manager.Register<Md5AnimTranslator>();
        Manager.Register<Md5MeshTranslator>();
        Manager.Register<CastTranslator>();
        Manager.Register<GltfTranslator>();
        Manager.Register<BvhTranslator>();
    }
}
