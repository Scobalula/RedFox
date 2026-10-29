using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Formats.BiovisionHierarchy;
using RedFox.Graphics3D.Formats.GLTransmissionFormat;
using RedFox.Graphics3D.Formats.SEAnim;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Benchmarks;

internal static class Graphics3DBenchmarks
{
    public static void Run()
    {
        Console.WriteLine($"Graphics3D, .NET {Environment.Version}, {Environment.ProcessorCount} threads");
        Measure("mesh-heavy GLB", CreateMeshes(1, 30000), new GltfTranslator(), ".glb", SceneNodeFlags.None);
        Measure("small meshes GLB", CreateMeshes(200, 30), new GltfTranslator(), ".glb", SceneNodeFlags.None);
        MeasureAccessor("packed accessor read", 12);
        MeasureAccessor("strided accessor read", 16);

        foreach (int boneCount in new[] { 32, 128, 512 })
        {
            Scene scene = CreateAnimation(boneCount);
            Measure($"animation {boneCount} SEAnim", scene, new SeanimTranslator(), ".seanim", SceneNodeFlags.None);
            Measure($"filtered {boneCount} BVH", scene, new BvhTranslator(), ".bvh", SceneNodeFlags.Selected);
        }
    }

    private static void Measure(string name, Scene scene, SceneTranslator translator, string extension, SceneNodeFlags filter)
    {
        SceneTranslatorManager manager = new();
        manager.Register(translator);
        SceneTranslatorOptions options = new() { Filter = filter, WriteRawVertices = true };
        using MemoryStream output = new();
        for (int i = 0; i < 3; i++)
        {
            output.SetLength(0);
            manager.Write(output, "benchmark" + extension, scene, options);
        }

        GC.Collect();
        long allocated	= GC.GetAllocatedBytesForCurrentThread();
        Stopwatch timer	= Stopwatch.StartNew();
        for (int i = 0; i < 5; i++)
        {
            output.SetLength(0);
            manager.Write(output, "benchmark" + extension, scene, options);
        }
        timer.Stop();
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Console.WriteLine($"{name,-28} {timer.Elapsed.TotalMilliseconds / 5,10:F3} ms {allocated / 5,12:N0} bytes");
    }

    private static Scene CreateMeshes(int meshCount, int vertexCount)
    {
        Scene scene = new("meshes");
        for (int i = 0; i < meshCount; i++)
        {
            Material material	= scene.RootNode.AddNode(new Material($"material_{i}"));
            Mesh mesh			= scene.RootNode.AddNode(new Mesh { Name = $"mesh_{i}", Materials = [material] });
            float[] positions	= new float[vertexCount * 3];
            for (int v = 0; v < vertexCount; v++)
            {
                positions[v * 3]		= v % 100;
                positions[v * 3 + 1]	= v / 100;
            }
            mesh.Positions		= new DataBuffer<float>(positions, 1, 3);
            mesh.FaceIndices	= new DataBuffer<int>(Enumerable.Range(0, vertexCount).ToArray(), 1, 1);
        }
        return scene;
    }

    private static void MeasureAccessor(string name, int stride)
    {
        float[] values = new float[30000 * stride / sizeof(float)];
        for (int i = 0; i < values.Length; i++)
            values[i] = i * 0.01f;
        byte[] data				= MemoryMarshal.AsBytes(values.AsSpan()).ToArray();
        GltfDocument document	= new()
        {
            Buffers = [new GltfBuffer
            {
                Data		= data,
                ByteLength	= data.Length,
            }],
            BufferViews = [new GltfBufferView
            {
                Buffer		= 0,
                ByteLength	= data.Length,
                ByteStride	= stride,
            }],
            Accessors = [new GltfAccessor
            {
                BufferView		= 0,
                Count			= 30000,
                ComponentType	= GltfConstants.ComponentTypeFloat,
                Type			= GltfConstants.TypeVec3,
            }],
        };
        for (int i = 0; i < 3; i++)
            GC.KeepAlive(document.ReadAccessorAsFloats(0));

        GC.Collect();
        long allocated	= GC.GetAllocatedBytesForCurrentThread();
        Stopwatch timer	= Stopwatch.StartNew();
        for (int i = 0; i < 5; i++)
            GC.KeepAlive(document.ReadAccessorAsFloats(0));
        timer.Stop();
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Console.WriteLine($"{name,-28} {timer.Elapsed.TotalMilliseconds / 5,10:F3} ms {allocated / 5,12:N0} bytes");
    }

    private static Scene CreateAnimation(int boneCount)
    {
        Scene scene					= new("animation");
        Skeleton skeleton			= scene.RootNode.AddNode(new Skeleton("skeleton") { Flags = SceneNodeFlags.Selected });
        SkeletonAnimation animation	= scene.RootNode.AddNode(new SkeletonAnimation("clip") { Framerate = 30, Flags = SceneNodeFlags.Selected });
        SceneNode parent			= skeleton;
        for (int i = 0; i < boneCount; i++)
        {
            SkeletonBone bone = parent.AddNode(new SkeletonBone($"bone_{i}") { Flags = i % 2 == 0 ? SceneNodeFlags.Selected : SceneNodeFlags.None });
            bone.BindTransform.LocalPosition = Vector3.UnitY;
            SkeletonAnimationTrack track = new(bone.Name);
            for (int frame = 0; frame < 30; frame++)
            {
                track.AddTranslationFrame(frame, Vector3.UnitY);
                track.AddRotationFrame(frame, Quaternion.CreateFromYawPitchRoll(frame * 0.01f, frame * 0.005f, 0));
            }
            animation.Tracks.Add(track);
            parent = bone;
        }
        return scene;
    }
}
