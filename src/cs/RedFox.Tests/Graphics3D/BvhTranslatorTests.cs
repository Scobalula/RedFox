//using System.Globalization;
//using System.Numerics;
//using System.Text;

//using RedFox.Graphics3D;
//using RedFox.Graphics3D.Bvh;
//using RedFox.Graphics3D.Buffers;
//using RedFox.Graphics3D.IO;
//using RedFox.Graphics3D.Skeletal;

//namespace RedFox.Tests.Graphics3D;

///// <summary>
///// Verifies BVH scene import and export behaviour.
///// </summary>
//public sealed class BvhTranslatorTests
//{
//    /// <summary>
//    /// Ensures a BVH round-trip preserves the bone hierarchy and names using the shared scene types.
//    /// </summary>
//    [Fact]
//    public void BvhTranslator_RoundTrip_PreservesHierarchyAndBoneNames()
//    {
//        Scene source = CreateAnimatedBvhScene();
//        SceneTranslatorManager manager = CreateManager();

//        byte[] data = WriteScene(manager, source, "anim.bvh");
//        Scene loaded = ReadScene(manager, data, "anim.bvh");

//        SkeletonBone[] loadedBones = loaded.GetDescendants<SkeletonBone>();
//        Assert.Equal(2, loadedBones.Length);
//        Assert.Equal("Root", loadedBones[0].Name);
//        Assert.Equal("Child", loadedBones[1].Name);
//        Assert.Same(loadedBones[0], loadedBones[1].Parent);
//    }

//    /// <summary>
//    /// Ensures a BVH round-trip preserves root translation samples, joint rotations, and animation timing.
//    /// </summary>
//    [Fact]
//    public void BvhTranslator_RoundTrip_PreservesAnimationSamples()
//    {
//        Scene source = CreateAnimatedBvhScene();
//        SceneTranslatorManager manager = CreateManager();

//        byte[] data = WriteScene(manager, source, "anim.bvh");
//        Scene loaded = ReadScene(manager, data, "anim.bvh");

//        SkeletonAnimation sourceAnimation = source.FirstOfType<SkeletonAnimation>();
//        SkeletonAnimation loadedAnimation = loaded.FirstOfType<SkeletonAnimation>();

//        Assert.Equal(sourceAnimation.Tracks.Count, loadedAnimation.Tracks.Count);
//        Assert.True(MathF.Abs(sourceAnimation.Framerate - loadedAnimation.Framerate) <= 0.01f, "Framerate was not preserved.");

//        SkeletonAnimationTrack sourceRootTrack = sourceAnimation.Tracks.Single(track => track.Name == "Root");
//        SkeletonAnimationTrack loadedRootTrack = loadedAnimation.Tracks.Single(track => track.Name == "Root");
//        SkeletonAnimationTrack sourceChildTrack = sourceAnimation.Tracks.Single(track => track.Name == "Child");
//        SkeletonAnimationTrack loadedChildTrack = loadedAnimation.Tracks.Single(track => track.Name == "Child");

//        for (int frame = 0; frame < 3; frame++)
//        {
//            AssertVector3Near(sourceRootTrack.TranslationCurve!.SampleVector3(frame), loadedRootTrack.TranslationCurve!.SampleVector3(frame), 1e-3f, $"root translation frame {frame}");
//            AssertQuaternionNear(sourceRootTrack.RotationCurve!.SampleQuaternion(frame), loadedRootTrack.RotationCurve!.SampleQuaternion(frame), 1e-3f, $"root rotation frame {frame}");
//            AssertQuaternionNear(sourceChildTrack.RotationCurve!.SampleQuaternion(frame), loadedChildTrack.RotationCurve!.SampleQuaternion(frame), 1e-3f, $"child rotation frame {frame}");
//        }
//    }

//    /// <summary>
//    /// Ensures the reader handles non-default BVH rotation orders without introducing format-specific scene node types.
//    /// </summary>
//    [Fact]
//    public void BvhTranslator_Read_CustomChannelOrder_ComposesRotationCorrectly()
//    {
//        const string bvhText =
//"""
//HIERARCHY
//ROOT Root
//{
//  OFFSET 0 0 0
//  CHANNELS 6 Xposition Yposition Zposition Xrotation Zrotation Yrotation
//  JOINT Child
//  {
//    OFFSET 0 5 0
//    CHANNELS 3 Zrotation Xrotation Yrotation
//  }
//}
//MOTION
//Frames: 1
//Frame Time: 0.033333
//1 2 3 10 30 20 5 15 25
//""";
//        SceneTranslatorManager manager = CreateManager();
//        Scene loaded = ReadScene(manager, Encoding.UTF8.GetBytes(bvhText), "custom-order.bvh");
//        SkeletonAnimation animation = loaded.FirstOfType<SkeletonAnimation>();
//        SkeletonAnimationTrack rootTrack = animation.Tracks.Single(track => track.Name == "Root");
//        SkeletonAnimationTrack childTrack = animation.Tracks.Single(track => track.Name == "Child");

//        AssertVector3Near(new Vector3(1.0f, 2.0f, 3.0f), rootTrack.TranslationCurve!.SampleVector3(0.0f), 1e-4f, "root translation");
//        AssertQuaternionNear(CreateExpectedBvhQuaternion(new Vector3(10.0f, 20.0f, 30.0f), [BvhChannelType.Xrotation, BvhChannelType.Zrotation, BvhChannelType.Yrotation]), rootTrack.RotationCurve!.SampleQuaternion(0.0f), 1e-4f, "root rotation");
//        AssertQuaternionNear(CreateExpectedBvhQuaternion(new Vector3(15.0f, 25.0f, 5.0f), [BvhChannelType.Zrotation, BvhChannelType.Xrotation, BvhChannelType.Yrotation]), childTrack.RotationCurve!.SampleQuaternion(0.0f), 1e-4f, "child rotation");
//    }

//    /// <summary>
//    /// Ensures the external <c>sample_move.bvh</c> sample imports root rotation using the BVH matrix convention before being saved to SEAnim.
//    /// </summary>
//    [Fact]
//    public void BvhTranslator_Read_SampleMove_ImportsFirstRootRotationCorrectly()
//    {
//        string? corpusDirectory = GetCorpusDirectory();
//        if (corpusDirectory is null)
//        {
//            return;
//        }

//        string samplePath = Path.Combine(corpusDirectory, "sample_move.bvh");
//        if (!File.Exists(samplePath))
//        {
//            return;
//        }

//        string[] lines = File.ReadAllLines(samplePath);
//        int frameTimeLineIndex = Array.FindIndex(lines, line => line.Contains("Frame Time:", StringComparison.OrdinalIgnoreCase));
//        Assert.True(frameTimeLineIndex >= 0 && frameTimeLineIndex + 1 < lines.Length, "The sample_move BVH file is missing motion data.");

//        string[] tokens = lines[frameTimeLineIndex + 1].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
//        Assert.True(tokens.Length >= 6, "The first BVH motion frame did not contain the expected root motion channels.");

//        Vector3 rootEulerDegrees = new(float.Parse(tokens[4], CultureInfo.InvariantCulture), float.Parse(tokens[5], CultureInfo.InvariantCulture), float.Parse(tokens[3], CultureInfo.InvariantCulture));
//        Quaternion expectedRotation = CreateExpectedBvhQuaternion(rootEulerDegrees, BvhFormat.DefaultRootChannelSequence);
//        SceneTranslatorManager manager = CreateManager();
//        Scene loaded = manager.Read(samplePath, CreateDefaultOptions(), token: null);
//        SkeletonAnimation animation = loaded.FirstOfType<SkeletonAnimation>();
//        SkeletonAnimationTrack rootTrack = animation.Tracks.Single(track => track.Name == "Hips");

//        AssertQuaternionNear(expectedRotation, rootTrack.RotationCurve!.SampleQuaternion(0.0f), 1e-4f, "sample_move root rotation frame 0");
//    }

//    /// <summary>
//    /// Ensures the BVH writer emits the required hierarchy and motion sections using the default export channel order.
//    /// </summary>
//    [Fact]
//    public void BvhTranslator_Write_ContainsRequiredSectionsAndDefaultChannels()
//    {
//        Scene source = CreateAnimatedBvhScene();
//        SceneTranslatorManager manager = CreateManager();

//        byte[] data = WriteScene(manager, source, "anim.bvh");
//        string text = Encoding.UTF8.GetString(data);

//        Assert.StartsWith("HIERARCHY", text.TrimStart(), StringComparison.Ordinal);
//        Assert.Contains("MOTION", text, StringComparison.Ordinal);
//        Assert.Contains("Frames: 3", text, StringComparison.Ordinal);
//        Assert.Contains("Frame Time: 0.033333", text, StringComparison.Ordinal);
//        Assert.Contains("CHANNELS 6 Xposition Yposition Zposition Zrotation Xrotation Yrotation", text, StringComparison.Ordinal);
//        Assert.Contains("CHANNELS 3 Zrotation Xrotation Yrotation", text, StringComparison.Ordinal);
//    }

//    /// <summary>
//    /// Ensures the BVH writer preserves Euler continuity across the +/-180-degree wrap boundary to avoid visible jitter in consumers that interpolate BVH angles directly.
//    /// </summary>
//    [Fact]
//    public void BvhTranslator_Write_RotationAnglesRemainContinuousAcrossWrapBoundary()
//    {
//        Scene source = CreateWrapBoundaryScene();
//        SceneTranslatorManager manager = CreateManager();

//        byte[] data = WriteScene(manager, source, "wrap.bvh");
//        string text = Encoding.UTF8.GetString(data).Replace("\r", string.Empty, StringComparison.Ordinal);
//        string[] lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
//        int motionStartIndex = Array.FindIndex(lines, line => line.StartsWith("Frame Time:", StringComparison.Ordinal));
//        Assert.True(motionStartIndex >= 0, "The exported BVH motion header did not contain a frame time declaration.");

//        string[] motionLines = lines[(motionStartIndex + 1)..];
//        Assert.Equal(5, motionLines.Length);

//        float[] expectedAngles = [170.0f, 175.0f, 180.0f, 185.0f, 190.0f];
//        for (int i = 0; i < motionLines.Length; i++)
//        {
//            string[] values = motionLines[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
//            Assert.True(values.Length >= 6, $"Expected at least 6 motion values on frame {i}, but found {values.Length}.");

//            float zRotation = float.Parse(values[3], CultureInfo.InvariantCulture);
//            Assert.True(MathF.Abs(zRotation - expectedAngles[i]) <= 0.01f, $"frame {i}: expected continuous Z rotation {expectedAngles[i]} but found {zRotation}.");
//        }
//    }

//    /// <summary>
//    /// Ensures a skeleton without an animation clip can still be exported as a one-frame BVH bind pose.
//    /// </summary>
//    [Fact]
//    public void BvhTranslator_Write_StaticSkeleton_WritesSingleFrame()
//    {
//        Scene source = CreateStaticBvhScene();
//        SceneTranslatorManager manager = CreateManager();

//        byte[] data = WriteScene(manager, source, "static.bvh");
//        string text = Encoding.UTF8.GetString(data);
//        Scene loaded = ReadScene(manager, data, "static.bvh");

//        Assert.Contains("Frames: 1", text, StringComparison.Ordinal);

//        SkeletonAnimation animation = loaded.FirstOfType<SkeletonAnimation>();
//        Assert.Equal(2, animation.Tracks.Count);

//        SkeletonBone[] loadedBones = loaded.GetDescendants<SkeletonBone>().Where(b => b.Parent is not SkeletonBone).ToArray();
//        Assert.Equal(2, loadedBones.Length);
//    }

//    /// <summary>
//    /// Ensures the writer rejects mesh content explicitly because BVH cannot represent geometry or skinning data.
//    /// </summary>
//    [Fact]
//    public void BvhTranslator_Write_WithMeshContent_ThrowsInvalidOperationException()
//    {
//        Scene scene = CreateMeshScene();
//        SceneTranslatorManager manager = CreateManager();
//        using MemoryStream stream = new();

//        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => manager.Write(stream, "mesh.bvh", scene, CreateDefaultOptions(), token: null));

//        Assert.Contains("BVH export cannot represent scene node", exception.Message, StringComparison.Ordinal);
//    }

//    /// <summary>
//    /// Ensures malformed BVH text is rejected with an <see cref="InvalidDataException"/>.
//    /// </summary>
//    [Fact]
//    public void BvhTranslator_Read_InvalidInput_ThrowsInvalidDataException()
//    {
//        BvhTranslator translator = new();
//        Scene scene = new("InvalidScene");
//        using MemoryStream stream = new(Encoding.UTF8.GetBytes("NOT_A_VALID_BVH"));

//        Assert.Throws<InvalidDataException>(() => translator.Read(scene, stream, "invalid", CreateDefaultOptions(), token: null));
//    }

//    /// <summary>
//    /// Ensures the external BVH corpus can be parsed without throwing when the corpus is available on disk.
//    /// </summary>
//    [Fact]
//    public void BvhTranslator_Read_CorpusFiles_DoNotThrow()
//    {
//        string? corpusDirectory = GetCorpusDirectory();
//        if (corpusDirectory is null)
//        {
//            return;
//        }

//        string[] files = Directory.GetFiles(corpusDirectory, "*.bvh");
//        Assert.NotEmpty(files);

//        SceneTranslatorManager manager = CreateManager();
//        for (int i = 0; i < files.Length; i++)
//        {
//            Scene scene = manager.Read(files[i], CreateDefaultOptions(), token: null);
//            Assert.NotEmpty(scene.GetDescendants<SkeletonBone>());
//            Assert.NotNull(scene.TryGetFirstOfType<SkeletonAnimation>());
//        }
//    }

//    private static Scene CreateAnimatedBvhScene()
//    {
//        Scene scene = new("AnimatedBvhScene");
//        SkeletonBone skeleton = scene.RootNode.AddNode<SkeletonBone>("Skeleton");

//        SkeletonBone root = skeleton.AddNode(new SkeletonBone("Root"));
//        root.BindTransform.LocalPosition = new Vector3(0.0f, 10.0f, 0.0f);
//        root.BindTransform.LocalRotation = Quaternion.Identity;

//        SkeletonBone child = root.AddNode(new SkeletonBone("Child"));
//        child.BindTransform.LocalPosition = new Vector3(0.0f, 5.0f, 0.0f);
//        child.BindTransform.LocalRotation = Quaternion.Identity;

//        SkeletonAnimation animation = scene.RootNode.AddNode(new SkeletonAnimation("Walk", 2, TransformType.Absolute)
//        {
//            Framerate = 30.0f,
//            TransformSpace = TransformSpace.Local,
//            TransformType = TransformType.Absolute,
//        });

//        SkeletonAnimationTrack rootTrack = new("Root")
//        {
//            TransformSpace = TransformSpace.Local,
//            TransformType = TransformType.Absolute,
//        };

//        rootTrack.AddTranslationFrame(0.0f, new Vector3(0.0f, 10.0f, 0.0f));
//        rootTrack.AddTranslationFrame(1.0f, new Vector3(1.0f, 11.5f, 2.0f));
//        rootTrack.AddTranslationFrame(2.0f, new Vector3(2.0f, 13.0f, 4.0f));
//        rootTrack.AddRotationFrame(0.0f, BvhRotation.ComposeDegrees(new Vector3(10.0f, 20.0f, 30.0f), BvhFormat.DefaultRootChannelSequence));
//        rootTrack.AddRotationFrame(1.0f, BvhRotation.ComposeDegrees(new Vector3(25.0f, -15.0f, 45.0f), BvhFormat.DefaultRootChannelSequence));
//        rootTrack.AddRotationFrame(2.0f, BvhRotation.ComposeDegrees(new Vector3(-10.0f, 40.0f, 15.0f), BvhFormat.DefaultRootChannelSequence));

//        SkeletonAnimationTrack childTrack = new("Child")
//        {
//            TransformSpace = TransformSpace.Local,
//            TransformType = TransformType.Absolute,
//        };

//        childTrack.AddRotationFrame(0.0f, BvhRotation.ComposeDegrees(new Vector3(0.0f, 5.0f, 10.0f), BvhFormat.DefaultJointChannelSequence));
//        childTrack.AddRotationFrame(1.0f, BvhRotation.ComposeDegrees(new Vector3(15.0f, -5.0f, 35.0f), BvhFormat.DefaultJointChannelSequence));
//        childTrack.AddRotationFrame(2.0f, BvhRotation.ComposeDegrees(new Vector3(-20.0f, 12.0f, 5.0f), BvhFormat.DefaultJointChannelSequence));

//        animation.Tracks.Add(rootTrack);
//        animation.Tracks.Add(childTrack);

//        return scene;
//    }

//    private static Scene CreateStaticBvhScene()
//    {
//        Scene scene = new("StaticBvhScene");
//        SkeletonBone skeleton = scene.RootNode.AddNode<SkeletonBone>("Skeleton");

//        SkeletonBone root = skeleton.AddNode(new SkeletonBone("Root"));
//        root.BindTransform.LocalPosition = Vector3.Zero;
//        root.BindTransform.LocalRotation = Quaternion.Identity;

//        SkeletonBone child = root.AddNode(new SkeletonBone("Child"));
//        child.BindTransform.LocalPosition = new Vector3(0.0f, 3.0f, 0.0f);
//        child.BindTransform.LocalRotation = Quaternion.Identity;

//        return scene;
//    }

//    private static Scene CreateWrapBoundaryScene()
//    {
//        Scene scene = new("WrapBoundaryScene");
//        SkeletonBone skeleton = scene.RootNode.AddNode<SkeletonBone>("Skeleton");

//        SkeletonBone root = skeleton.AddNode(new SkeletonBone("Root"));
//        root.BindTransform.LocalPosition = Vector3.Zero;
//        root.BindTransform.LocalRotation = Quaternion.Identity;

//        SkeletonAnimation animation = scene.RootNode.AddNode(new SkeletonAnimation("Wrap", 1, TransformType.Absolute)
//        {
//            Framerate = 30.0f,
//            TransformSpace = TransformSpace.Local,
//            TransformType = TransformType.Absolute,
//        });

//        SkeletonAnimationTrack rootTrack = new("Root")
//        {
//            TransformSpace = TransformSpace.Local,
//            TransformType = TransformType.Absolute,
//        };

//        rootTrack.AddRotationFrame(0.0f, BvhRotation.ComposeDegrees(new Vector3(0.0f, 0.0f, 170.0f), BvhFormat.DefaultRootChannelSequence));
//        rootTrack.AddRotationFrame(1.0f, BvhRotation.ComposeDegrees(new Vector3(0.0f, 0.0f, 175.0f), BvhFormat.DefaultRootChannelSequence));
//        rootTrack.AddRotationFrame(2.0f, BvhRotation.ComposeDegrees(new Vector3(0.0f, 0.0f, 180.0f), BvhFormat.DefaultRootChannelSequence));
//        rootTrack.AddRotationFrame(3.0f, BvhRotation.ComposeDegrees(new Vector3(0.0f, 0.0f, 185.0f), BvhFormat.DefaultRootChannelSequence));
//        rootTrack.AddRotationFrame(4.0f, BvhRotation.ComposeDegrees(new Vector3(0.0f, 0.0f, 190.0f), BvhFormat.DefaultRootChannelSequence));

//        animation.Tracks.Add(rootTrack);
//        return scene;
//    }

//    private static Scene CreateMeshScene()
//    {
//        Scene scene = new("MeshScene");
//        Model model = scene.RootNode.AddNode<Model>("Model");
//        Mesh mesh = model.AddNode<Mesh>("Mesh");

//        mesh.Positions = MakeFloat3Buffer(
//        [
//            new Vector3(0.0f, 0.0f, 0.0f),
//            new Vector3(1.0f, 0.0f, 0.0f),
//            new Vector3(0.0f, 1.0f, 0.0f),
//        ]);
//        mesh.FaceIndices = MakeIntBuffer([0, 1, 2]);

//        return scene;
//    }

//    private static DataBuffer<float> MakeFloat3Buffer(IReadOnlyList<Vector3> values)
//    {
//        DataBuffer<float> buffer = new(values.Count, 1, 3);
//        for (int i = 0; i < values.Count; i++)
//            buffer.Add(values[i]);

//        return buffer;
//    }

//    private static DataBuffer<int> MakeIntBuffer(IReadOnlyList<int> values)
//    {
//        DataBuffer<int> buffer = new(values.Count, 1, 1);
//        for (int i = 0; i < values.Count; i++)
//            buffer.Add(values[i]);

//        return buffer;
//    }

//    private static SceneTranslatorManager CreateManager()
//    {
//        SceneTranslatorManager manager = new();
//        manager.Register(new BvhTranslator());
//        return manager;
//    }

//    private static SceneTranslatorOptions CreateDefaultOptions()
//    {
//        return new SceneTranslatorOptions();
//    }

//    private static byte[] WriteScene(SceneTranslatorManager manager, Scene scene, string fileName)
//    {
//        using MemoryStream stream = new();
//        manager.Write(stream, fileName, scene, CreateDefaultOptions(), token: null);
//        return stream.ToArray();
//    }

//    private static Scene ReadScene(SceneTranslatorManager manager, byte[] data, string fileName)
//    {
//        using MemoryStream stream = new(data);
//        return manager.Read(stream, fileName, CreateDefaultOptions(), token: null);
//    }

//    private static string? GetCorpusDirectory()
//    {
//        string[] candidates =
//        [
//            @"Z:\Tests\RedFox\Input\Bvh",
//            @"Z:\Tests\RedFox\Input\Bhv",
//        ];

//        for (int i = 0; i < candidates.Length; i++)
//        {
//            if (Directory.Exists(candidates[i]))
//            {
//                return candidates[i];
//            }
//        }

//        return null;
//    }

//    private static Quaternion CreateExpectedBvhQuaternion(Vector3 eulerDegrees, IReadOnlyList<BvhChannelType> channels)
//    {
//        Matrix4x4 rotation = Matrix4x4.Identity;
//        for (int i = channels.Count - 1; i >= 0; i--)
//        {
//            BvhChannelType channel = channels[i];
//            if (!BvhFormat.IsRotationChannel(channel))
//            {
//                continue;
//            }

//            float radians = BvhRotation.GetAxisComponent(eulerDegrees, channel) * MathF.PI / 180.0f;
//            Matrix4x4 axisRotation = channel switch
//            {
//                BvhChannelType.Xrotation => Matrix4x4.CreateRotationX(radians),
//                BvhChannelType.Yrotation => Matrix4x4.CreateRotationY(radians),
//                BvhChannelType.Zrotation => Matrix4x4.CreateRotationZ(radians),
//                _ => Matrix4x4.Identity,
//            };

//            rotation *= axisRotation;
//        }

//        return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(rotation));
//    }

//    private static void AssertVector3Near(Vector3 expected, Vector3 actual, float tolerance, string label)
//    {
//        Assert.True(
//            MathF.Abs(expected.X - actual.X) <= tolerance &&
//            MathF.Abs(expected.Y - actual.Y) <= tolerance &&
//            MathF.Abs(expected.Z - actual.Z) <= tolerance,
//            $"{label}: expected ({expected.X}, {expected.Y}, {expected.Z}) but received ({actual.X}, {actual.Y}, {actual.Z}).");
//    }

//    private static void AssertQuaternionNear(Quaternion expected, Quaternion actual, float tolerance, string label)
//    {
//        Quaternion left = Quaternion.Normalize(expected);
//        Quaternion right = Quaternion.Normalize(actual);
//        float dot = MathF.Abs(Quaternion.Dot(left, right));
//        Assert.True(dot >= 1.0f - tolerance, $"{label}: expected {left} but received {right}.");
//    }
//}
