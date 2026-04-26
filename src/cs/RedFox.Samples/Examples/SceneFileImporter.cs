using RedFox.Graphics3D;
using RedFox.Graphics3D.Buffers;
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
using System.Numerics;

namespace RedFox.Samples.Examples;

internal sealed class SceneFileImporter
{
    public Scene Load(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count == 0)
        {
            return CreateFallbackScene("Avalonia Scene");
        }

        Scene scene = new(Path.GetFileName(paths[0]));
        SceneTranslatorManager manager = CreateTranslatorManager();
        for (int i = 0; i < paths.Count; i++)
        {
            string path = Path.GetFullPath(paths[i]);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Scene input file was not found.", path);
            }

            manager.Read(path, scene, new SceneTranslatorOptions());
        }

        scene.CreateAnimationPlayers();
        return scene;
    }

    public Scene CreateFallbackScene(string name)
    {
        Scene scene = new(name);
        Mesh mesh = scene.RootNode.AddNode<Mesh>(CreateTriangleMesh(GetUniqueChildName(scene.RootNode, "Triangle")));
        Material material = new("TriangleMaterial")
        {
            DiffuseColor = new Vector4(0.92f, 0.3f, 0.24f, 1.0f)
        };
        mesh.Materials = new List<Material> { material };
        return scene;
    }

    public Mesh CreateTriangleMesh(string name)
    {
        Mesh mesh = new()
        {
            Name = name,
            Positions = CreatePositions(),
            Normals = CreateNormals(),
            FaceIndices = CreateIndices()
        };
        Material material = new($"{name}Material")
        {
            DiffuseColor = new Vector4(0.92f, 0.3f, 0.24f, 1.0f)
        };
        mesh.Materials = new List<Material> { material };
        return mesh;
    }

    public string GetUniqueChildName(SceneNode parent, string baseName)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseName);
        if (!parent.TryFindChild(baseName, out _))
        {
            return baseName;
        }

        int suffix = 2;
        while (parent.TryFindChild($"{baseName}{suffix}", out _))
        {
            suffix++;
        }

        return $"{baseName}{suffix}";
    }

    private static SceneTranslatorManager CreateTranslatorManager()
    {
        SceneTranslatorManager manager = new();
        manager.Register<ObjTranslator>();
        manager.Register<GltfTranslator>();
        manager.Register<SemodelTranslator>();
        manager.Register<SmdTranslator>();
        manager.Register<MayaAsciiTranslator>();
        manager.Register<FbxTranslator>();
        manager.Register<CastTranslator>();
        manager.Register<BvhTranslator>();
        manager.Register<Md5MeshTranslator>();
        manager.Register<Md5AnimTranslator>();
        manager.Register<SeanimTranslator>();
        return manager;
    }

    private static DataBuffer<float> CreatePositions()
    {
        DataBuffer<float> positions = new(3, 1, 3);
        positions.Add(new Vector3(-0.9f, -0.7f, 0.0f));
        positions.Add(new Vector3(0.9f, -0.7f, 0.0f));
        positions.Add(new Vector3(0.0f, 0.85f, 0.0f));
        return positions;
    }

    private static DataBuffer<float> CreateNormals()
    {
        DataBuffer<float> normals = new(3, 1, 3);
        normals.Add(new Vector3(0.0f, 0.0f, 1.0f));
        normals.Add(new Vector3(0.0f, 0.0f, 1.0f));
        normals.Add(new Vector3(0.0f, 0.0f, 1.0f));
        return normals;
    }

    private static DataBuffer<uint> CreateIndices()
    {
        DataBuffer<uint> indices = new(3, 1, 1);
        indices.Add(0u);
        indices.Add(1u);
        indices.Add(2u);
        return indices;
    }
}
