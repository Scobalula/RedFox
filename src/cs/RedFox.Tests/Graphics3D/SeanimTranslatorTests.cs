// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Numerics;
using RedFox.Graphics3D;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.SEAnim;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Tests.Graphics3D;

public sealed class SeanimTranslatorTests
{
    [Fact]
    public void SeanimTranslator_RoundTripViaManager_PreservesAnimationStructure()
    {
        Scene sourceScene = CreateSeanimSampleScene();
        SceneTranslatorManager manager = CreateManagerWithSeanimTranslator();
        byte[] firstWrite = WriteSceneWithManager(manager, sourceScene, "sample.seanim");
        Scene firstLoad = ReadSceneWithManager(manager, firstWrite, "sample.seanim");
        byte[] secondWrite = WriteSceneWithManager(manager, firstLoad, "sample.seanim");
        Scene secondLoad = ReadSceneWithManager(manager, secondWrite, "sample.seanim");

        Assert.True(
            firstWrite.AsSpan().SequenceEqual(secondWrite),
            "SEAnim rewrite changed bytes after translator-manager roundtrip.");

        SkeletonAnimation firstAnimation = firstLoad.FirstOfType<SkeletonAnimation>();
        SkeletonAnimation secondAnimation = secondLoad.FirstOfType<SkeletonAnimation>();
        AssertSkeletonAnimationStructurallyEqual(firstAnimation, secondAnimation);
    }

    [Fact]
    public void SeanimSamples_RoundTripViaManager_DeterministicRewriteAndStructuralMatch()
    {
        string seanimDirectory = GetRequiredSeanimDirectory();
        if (string.IsNullOrWhiteSpace(seanimDirectory))
        {
            return;
        }

        string[] seanimFiles = Directory.GetFiles(seanimDirectory, "*.seanim", SearchOption.AllDirectories);
        if (seanimFiles.Length == 0)
        {
            return;
        }

        SceneTranslatorManager manager = CreateManagerWithSeanimTranslator();
        foreach (string seanimFile in seanimFiles)
        {
            using FileStream inputFileStream = File.OpenRead(seanimFile);
            Scene sourceScene = manager.Read(inputFileStream, seanimFile, CreateDefaultOptions(), token: null);
            byte[] firstWrite = WriteSceneWithManager(manager, sourceScene, seanimFile);
            Scene firstLoad = ReadSceneWithManager(manager, firstWrite, seanimFile);
            byte[] secondWrite = WriteSceneWithManager(manager, firstLoad, seanimFile);
            Scene secondLoad = ReadSceneWithManager(manager, secondWrite, seanimFile);

            Assert.True(
                firstWrite.AsSpan().SequenceEqual(secondWrite),
                $"SEAnim rewrite changed bytes for '{seanimFile}'.");

            SkeletonAnimation firstAnimation = firstLoad.FirstOfType<SkeletonAnimation>();
            SkeletonAnimation secondAnimation = secondLoad.FirstOfType<SkeletonAnimation>();
            AssertSkeletonAnimationStructurallyEqual(firstAnimation, secondAnimation);
        }
    }

    [Fact]
    public void SeanimTranslator_Write_Filter_ExportsSelectedAnimation()
    {
        Scene scene = new("FilteredSeanimScene");

        SkeletonAnimation selectedAnimation = scene.RootNode.AddNode(new SkeletonAnimation("SelectedAnim", null, 1, TransformType.Relative)
        {
            Framerate = 30f,
            TransformType = TransformType.Relative,
            Flags = SceneNodeFlags.Selected,
        });
        SkeletonAnimationTrack selectedTrack = new("selected_root")
        {
            TransformType = TransformType.Relative,
        };
        selectedTrack.AddTranslationFrame(0f, Vector3.Zero);
        selectedTrack.AddTranslationFrame(5f, new Vector3(1f, 2f, 3f));
        selectedAnimation.Tracks.Add(selectedTrack);

        SkeletonAnimation ignoredAnimation = scene.RootNode.AddNode(new SkeletonAnimation("IgnoredAnim", null, 1, TransformType.Relative)
        {
            Framerate = 24f,
            TransformType = TransformType.Relative,
        });
        SkeletonAnimationTrack ignoredTrack = new("ignored_root")
        {
            TransformType = TransformType.Relative,
        };
        ignoredTrack.AddTranslationFrame(0f, new Vector3(9f, 9f, 9f));
        ignoredAnimation.Tracks.Add(ignoredTrack);

        SceneTranslatorManager manager = CreateManagerWithSeanimTranslator();
        SceneTranslatorOptions options = new()
        {
            Filter = SceneNodeFlags.Selected,
        };

        byte[] data = WriteSceneWithManager(manager, scene, "filtered.seanim", options);
        Scene loaded = ReadSceneWithManager(manager, data, "filtered.seanim");
        SkeletonAnimation animation = loaded.FirstOfType<SkeletonAnimation>();

        Assert.Single(animation.Tracks);
        Assert.Equal("selected_root", animation.Tracks[0].Name);
        Assert.InRange(animation.Tracks[0].TranslationCurve!.GetVector3(1).X, 0.9999f, 1.0001f);
    }

    [Fact]
    public void SeanimTranslator_Write_Filter_ThrowsWhenNoAnimationMatchesSelection()
    {
        Scene scene = new("FilteredOutSeanimScene");
        scene.RootNode.AddNode(new SkeletonAnimation("IgnoredAnim", null, 1, TransformType.Relative)
        {
            Framerate = 30f,
            TransformType = TransformType.Relative,
        });

        SceneTranslatorManager manager = CreateManagerWithSeanimTranslator();
        SceneTranslatorOptions options = new()
        {
            Filter = SceneNodeFlags.Selected,
        };

        InvalidDataException ex = Assert.Throws<InvalidDataException>(() =>
            WriteSceneWithManager(manager, scene, "filtered_out.seanim", options));

        Assert.Contains("matched the export selection", ex.Message);
        Assert.Contains(SceneNodeFlags.Selected.ToString(), ex.Message);
    }

    private static Scene CreateSeanimSampleScene()
    {
        Scene scene = new("SeanimSampleScene");
        SkeletonAnimation animation = scene.RootNode.AddNode(new SkeletonAnimation("SampleAnim", null, 2, TransformType.Relative)
        {
            Framerate = 30f,
            TransformType = TransformType.Relative,
        });

        SkeletonAnimationTrack rootTrack = new("root")
        {
            TransformType = TransformType.Relative,
        };
        rootTrack.AddTranslationFrame(0f, Vector3.Zero);
        rootTrack.AddTranslationFrame(10f, new Vector3(8f, 0f, 0f));
        rootTrack.AddRotationFrame(0f, Quaternion.Identity);
        rootTrack.AddRotationFrame(10f, Quaternion.Normalize(new Quaternion(0f, 0.70710677f, 0f, 0.70710677f)));
        rootTrack.AddScaleFrame(0f, Vector3.One);
        rootTrack.AddScaleFrame(10f, new Vector3(1.1f, 1f, 1f));

        SkeletonAnimationTrack spineTrack = new("spine")
        {
            TransformType = TransformType.Absolute,
        };
        spineTrack.AddTranslationFrame(0f, new Vector3(0f, 1f, 0f));
        spineTrack.AddTranslationFrame(10f, new Vector3(0f, 2f, 0f));
        spineTrack.AddRotationFrame(0f, Quaternion.Identity);
        spineTrack.AddRotationFrame(10f, Quaternion.Normalize(new Quaternion(0.25881904f, 0f, 0f, 0.9659258f)));

        animation.Tracks.Add(rootTrack);
        animation.Tracks.Add(spineTrack);

        AnimationAction action = animation.CreateAction("footstep");
        action.KeyFrames.Add(new AnimationKeyFrame<float, Action<Scene>?>(3f, null));
        action.KeyFrames.Add(new AnimationKeyFrame<float, Action<Scene>?>(9f, null));

        return scene;
    }

    private static void AssertSkeletonAnimationStructurallyEqual(SkeletonAnimation expected, SkeletonAnimation actual)
    {
        const float tolerance = 0.0001f;

        AssertFloatNear(expected.Framerate, actual.Framerate, tolerance);
        Assert.Equal(expected.TransformType, actual.TransformType);
        Assert.Equal(expected.Tracks.Count, actual.Tracks.Count);

        for (int i = 0; i < expected.Tracks.Count; i++)
        {
            SkeletonAnimationTrack expectedTrack = expected.Tracks[i];
            SkeletonAnimationTrack actualTrack = actual.Tracks[i];

            Assert.Equal(expectedTrack.Name, actualTrack.Name);
            Assert.Equal(expectedTrack.TransformType, actualTrack.TransformType);
            AssertAnimationCurveStructurallyEqual(expectedTrack.TranslationCurve, actualTrack.TranslationCurve, 3, tolerance);
            AssertAnimationCurveStructurallyEqual(expectedTrack.RotationCurve, actualTrack.RotationCurve, 4, tolerance);
            AssertAnimationCurveStructurallyEqual(expectedTrack.ScaleCurve, actualTrack.ScaleCurve, 3, tolerance);
        }

        int expectedActionCount = expected.Actions?.Count ?? 0;
        int actualActionCount = actual.Actions?.Count ?? 0;
        Assert.Equal(expectedActionCount, actualActionCount);

        if (expectedActionCount == 0)
        {
            return;
        }

        List<AnimationAction> expectedActions = expected.Actions!;
        List<AnimationAction> actualActions = actual.Actions!;
        for (int i = 0; i < expectedActions.Count; i++)
        {
            AnimationAction expectedAction = expectedActions[i];
            AnimationAction actualAction = actualActions[i];

            Assert.Equal(expectedAction.Name, actualAction.Name);
            Assert.Equal(expectedAction.KeyFrames.Count, actualAction.KeyFrames.Count);

            for (int keyFrameIndex = 0; keyFrameIndex < expectedAction.KeyFrames.Count; keyFrameIndex++)
            {
                AnimationKeyFrame<float, Action<Scene>?> expectedFrame = expectedAction.KeyFrames[keyFrameIndex];
                AnimationKeyFrame<float, Action<Scene>?> actualFrame = actualAction.KeyFrames[keyFrameIndex];
                AssertFloatNear(expectedFrame.Frame, actualFrame.Frame, tolerance);
            }
        }
    }

    private static void AssertAnimationCurveStructurallyEqual(AnimationCurve? expected, AnimationCurve? actual, int componentCount, float tolerance)
    {
        int expectedKeyCount = expected?.KeyFrameCount ?? 0;
        int actualKeyCount = actual?.KeyFrameCount ?? 0;
        Assert.Equal(expectedKeyCount, actualKeyCount);

        if (expectedKeyCount == 0)
        {
            return;
        }

        Assert.NotNull(actual);
        Assert.NotNull(expected);
        Assert.Equal(componentCount, actual!.ComponentCount);

        for (int i = 0; i < expectedKeyCount; i++)
        {
            AssertFloatNear(expected!.GetKeyTime(i), actual.GetKeyTime(i), tolerance);

            if (componentCount == 3)
            {
                AssertVector3Near(expected.GetVector3(i), actual.GetVector3(i), tolerance);
                continue;
            }

            if (componentCount == 4)
            {
                AssertQuaternionNear(expected.GetQuaternion(i), actual.GetQuaternion(i), tolerance);
            }
        }
    }

    private static void AssertVector3Near(Vector3 expected, Vector3 actual, float tolerance)
    {
        AssertFloatNear(expected.X, actual.X, tolerance);
        AssertFloatNear(expected.Y, actual.Y, tolerance);
        AssertFloatNear(expected.Z, actual.Z, tolerance);
    }

    private static void AssertQuaternionNear(Quaternion expected, Quaternion actual, float tolerance)
    {
        AssertFloatNear(expected.X, actual.X, tolerance);
        AssertFloatNear(expected.Y, actual.Y, tolerance);
        AssertFloatNear(expected.Z, actual.Z, tolerance);
        AssertFloatNear(expected.W, actual.W, tolerance);
    }

    private static void AssertFloatNear(float expected, float actual, float tolerance)
    {
        Assert.InRange(actual, expected - tolerance, expected + tolerance);
    }

    private static SceneTranslatorManager CreateManagerWithSeanimTranslator()
    {
        SceneTranslatorManager manager = new();
        manager.Register(new SeanimTranslator());
        return manager;
    }

    private static SceneTranslatorOptions CreateDefaultOptions()
    {
        return new SceneTranslatorOptions();
    }

    private static byte[] WriteSceneWithManager(SceneTranslatorManager manager, Scene scene, string sourcePath)
    {
        return WriteSceneWithManager(manager, scene, sourcePath, CreateDefaultOptions());
    }

    private static byte[] WriteSceneWithManager(SceneTranslatorManager manager, Scene scene, string sourcePath, SceneTranslatorOptions options)
    {
        using MemoryStream stream = new();
        manager.Write(stream, sourcePath, scene, options, token: null);
        return stream.ToArray();
    }

    private static Scene ReadSceneWithManager(SceneTranslatorManager manager, byte[] data, string sourcePath)
    {
        using MemoryStream stream = new(data, writable: false);
        return manager.Read(stream, sourcePath, CreateDefaultOptions(), token: null);
    }

    private static string GetRequiredSeanimDirectory()
    {
        string? testsRoot = Environment.GetEnvironmentVariable("REDFOX_TESTS_DIR");
        if (string.IsNullOrWhiteSpace(testsRoot))
        {
            return string.Empty;
        }

        string inputDirectory = Path.Combine(testsRoot, "Input", "SEAnim");
        if (Directory.Exists(inputDirectory))
        {
            return inputDirectory;
        }

        string legacyDirectory = Path.Combine(testsRoot, "SEAnim");
        if (Directory.Exists(legacyDirectory))
        {
            return legacyDirectory;
        }

        return string.Empty;
    }
}
