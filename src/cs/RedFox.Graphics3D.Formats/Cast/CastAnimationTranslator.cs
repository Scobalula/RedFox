using CastNet;
using CastNet.Nodes;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Skeletal;
using System.Numerics;
using System.Runtime.InteropServices;

namespace RedFox.Graphics3D.Formats.Cast;

internal static class CastAnimationTranslator
{
    private const string VisibilityCurveName = "visibility";

    private const string BlendShapeKeyProperty = "bs";

    public static void Read(Scene scene, AnimationNode animationNode, string name)
    {
        var skeletalAnimation = new SkeletonAnimation(name)
        {
            TransformType	= TransformType.Relative,
            Framerate		= animationNode.Framerate,
        };
        MorphAnimation? morphAnimation = null;

        foreach (var nodeCurves in animationNode.EnumerateCurves().GroupBy(curve => curve.NodeName))
        {
            var track		= new SkeletonAnimationTrack(nodeCurves.Key) { TransformSpace = TransformSpace.Local };
            var translation	= new CurveNode?[3];
            var scale		= new CurveNode?[3];

            foreach (var curveNode in nodeCurves)
            {
                switch (curveNode.KeyPropertyName)
                {
                    case BlendShapeKeyProperty:
                        morphAnimation ??= new MorphAnimation($"{name}_Morph")
                        {
                            Framerate = animationNode.Framerate,
                        };
                        morphAnimation.GetOrCreateTrack(nodeCurves.Key).WeightCurve = CreateCurve(curveNode, 1);
                        break;
                    case "rq":
                        track.RotationCurve = CreateCurve(curveNode, 4);
                        break;
                    case "tx":
                        translation[0] = curveNode;
                        break;
                    case "ty":
                        translation[1] = curveNode;
                        break;
                    case "tz":
                        translation[2] = curveNode;
                        break;
                    case "sx":
                        scale[0] = curveNode;
                        break;
                    case "sy":
                        scale[1] = curveNode;
                        break;
                    case "sz":
                        scale[2] = curveNode;
                        break;
                    default:
                        var customCurve = CreateCurve(curveNode, 1);
                        if (customCurve is not null)
                        {
                            track.CustomCurves ??= [];
                            string curveName = curveNode.KeyPropertyName == "vb" ? VisibilityCurveName : curveNode.KeyPropertyName;
                            track.CustomCurves[curveName] = customCurve;
                        }
                        break;
                }
            }

            track.TranslationCurve = CombineAxes(translation, 0.0f);
            track.ScaleCurve = CombineAxes(scale, 1.0f);
            track.TransformType = (track.TranslationCurve ?? track.RotationCurve ?? track.ScaleCurve)?.TransformType ?? TransformType.Relative;

            if (track.CurveCount > 0)
                skeletalAnimation.Tracks.Add(track);
        }

        foreach (var curveModeOverride in animationNode.EnumerateCurveModeOverrides())
        {
            var transformType	= CastTranslator.ConvertToTransformType(curveModeOverride.Mode);
            var nodeNames		= new HashSet<string> { curveModeOverride.NodeName };

            if (scene.RootNode.TryFindDescendant<SkeletonBone>(curveModeOverride.NodeName, out var bone))
                nodeNames.UnionWith(bone.EnumerateDescendants<SkeletonBone>().Select(descendant => descendant.Name));

            foreach (var track in skeletalAnimation.Tracks.Where(track => nodeNames.Contains(track.Name)))
            {
                if (curveModeOverride.OverrideTranslationCurves && track.TranslationCurve is AnimationCurve translationCurve)
                    translationCurve.TransformType = transformType;
                if (curveModeOverride.OverrideRotationCurves && track.RotationCurve is AnimationCurve rotationCurve)
                    rotationCurve.TransformType = transformType;
                if (curveModeOverride.OverrideScaleCurves && track.ScaleCurve is AnimationCurve scaleCurve)
                    scaleCurve.TransformType = transformType;
            }
        }

        foreach (var notificationTrack in animationNode.EnumerateNotificationTracks())
        {
            var action = skeletalAnimation.CreateAction(notificationTrack.Name);

            foreach (var frame in notificationTrack.KeyFrames?.ToArray<float>() ?? [])
                action.KeyFrames.Add(new(frame, null));
        }

        if (skeletalAnimation.Tracks.Count > 0 || skeletalAnimation.Actions is { Count: > 0 } || morphAnimation is null)
            scene.RootNode.AddNode(skeletalAnimation);

        if (morphAnimation is not null)
            scene.RootNode.AddNode(morphAnimation);
    }

    public static void Write(RootNode root, SkeletonAnimation animation)
    {
        var animationNode = root.AddNode(new AnimationNode
        {
            Name		= animation.Name,
            Framerate	= animation.Framerate,
        });

        foreach (var track in animation.Tracks)
        {
            if (track.RotationCurve is { KeyFrameCount: > 0 } rotationCurve)
            {
                var rotations = new Quaternion[rotationCurve.KeyFrameCount];

                for (var i = 0; i < rotations.Length; i++)
                    rotations[i] = rotationCurve.GetQuaternion(i);

                var rotationNode = animationNode.AddNode(new CurveNode
                {
                    NodeName		= track.Name,
                    KeyPropertyName	= "rq",
                    Mode			= CastTranslator.ConvertFromTransformType(GetMode(animation, rotationCurve)),
                    KeyFrames		= CreateKeyFrames(rotationCurve),
                    KeyValues		= CastArrayProperty.Create<Quaternion>(rotations),
                });

                if (rotationCurve.BlendWeight != 1.0f)
                    rotationNode.AdditiveBlendWeight = rotationCurve.BlendWeight;
            }

            if (track.TranslationCurve is { KeyFrameCount: > 0 } translationCurve)
                WriteVector3Curve(animationNode, track.Name, translationCurve, GetMode(animation, translationCurve), "tx", "ty", "tz");
            if (track.ScaleCurve is { KeyFrameCount: > 0 } scaleCurve)
                WriteVector3Curve(animationNode, track.Name, scaleCurve, GetMode(animation, scaleCurve), "sx", "sy", "sz");

            foreach (var (curveName, customCurve) in track.CustomCurves?.Where(entry => entry.Value is { KeyFrameCount: > 0, ComponentCount: 1 }) ?? [])
            {
                var isVisibility	= curveName == VisibilityCurveName;
                var values			= new CastArrayProperty(isVisibility ? CastPropertyType.Byte : CastPropertyType.Float, customCurve.KeyFrameCount);

                for (var i = 0; i < customCurve.KeyFrameCount; i++)
                {
                    if (isVisibility)
                        values.Add(customCurve.GetScalar(i) >= 1.0f ? (byte)1 : (byte)0);
                    else
                        values.Add(customCurve.GetScalar(i));
                }

                animationNode.AddNode(new CurveNode
                {
                    NodeName			= track.Name,
                    KeyPropertyName		= isVisibility ? "vb" : curveName,
                    Mode				= CastTranslator.ConvertFromTransformType(GetMode(animation, customCurve)),
                    KeyFrames			= CreateKeyFrames(customCurve),
                    KeyValues			= values,
                    AdditiveBlendWeight	= customCurve.BlendWeight,
                });
            }
        }

        foreach (var action in animation.Actions ?? [])
            animationNode.AddNode(new NotificationTrackNode
            {
                Name		= action.Name,
                KeyFrames	= CastArrayProperty.CreateIndices<int>([.. action.KeyFrames.Select(keyFrame => ToFrame(keyFrame.Frame))]),
            });
    }

    public static void Write(RootNode root, MorphAnimation animation)
    {
        var animationNode = root.AddNode(new AnimationNode
        {
            Name		= animation.Name,
            Framerate	= animation.Framerate,
        });

        foreach (var track in animation.Tracks)
        {
            if (track.WeightCurve is not { KeyFrameCount: > 0 } weightCurve)
                continue;

            var weights = new float[weightCurve.KeyFrameCount];

            for (var i = 0; i < weights.Length; i++)
                weights[i] = weightCurve.GetScalar(i);

            animationNode.AddNode(new CurveNode
            {
                NodeName		= track.Name,
                KeyPropertyName	= BlendShapeKeyProperty,
                Mode			= "absolute",
                KeyFrames		= CreateKeyFrames(weightCurve),
                KeyValues		= CastArrayProperty.Create<float>(weights),
            });
        }
    }

    private static AnimationCurve? CreateCurve(CurveNode curveNode, int componentCount)
    {
        if (curveNode.KeyFrames is not CastArrayProperty keyFrames || curveNode.KeyValues is not CastArrayProperty keyValues)
            return null;
        if (componentCount == 4 && keyValues.Type != CastPropertyType.Vector4)
            throw new InvalidDataException("Invalid Cast quaternion curve type.");
        if (componentCount == 1 && keyValues.Type is CastPropertyType.Vector2 or CastPropertyType.Vector3 or CastPropertyType.Vector4)
            throw new InvalidDataException("Invalid Cast scalar curve type.");

        if (keyFrames.Count != keyValues.Count)
            throw new InvalidDataException("Inconsistent Cast curve array lengths.");
        var count = keyFrames.Count;

        if (count == 0)
            return null;

        var frames = keyFrames.ToArray<float>();
        float[] values;
        if (componentCount == 4)
            values = MemoryMarshal.Cast<byte, float>(keyValues.AsBytes()).ToArray();
        else
            values = keyValues.ToArray<float>();

        return new AnimationCurve(TransformSpace.Local, CastTranslator.ConvertToTransformType(curveNode.Mode))
        {
            Keys		= new DataBuffer<float>(frames, 1, 1),
            Values		= new DataBuffer<float>(values, 1, componentCount),
            BlendWeight	= curveNode.AdditiveBlendWeight,
        };
    }

    private static AnimationCurve? CombineAxes(CurveNode?[] axes, float fallback)
    {
        var channels = new (AnimationCurve Curve, DataBuffer<float> Keys, DataBuffer<float> Values)?[axes.Length];
        (AnimationCurve Curve, DataBuffer<float> Keys, DataBuffer<float> Values)? first = null;
        bool sameKeys = true;

        for (int axis = 0; axis < axes.Length; axis++)
        {
            if (axes[axis] is not CurveNode node)
                continue;
            AnimationCurve? curve = CreateCurve(node, 1);
            if (curve is null)
                continue;
            if (curve.Keys is not DataBuffer<float> keys || curve.Values is not DataBuffer<float> values)
                throw new InvalidDataException("Invalid Cast axis buffers.");

            channels[axis] = (curve, keys, values);
            if (first is not { } reference)
            {
                first = channels[axis];
                continue;
            }
            if (curve.TransformType != reference.Curve.TransformType || curve.BlendWeight != reference.Curve.BlendWeight)
                throw new InvalidDataException("Cast vector axes have incompatible transform modes.");
            sameKeys &= keys.AsSpan().SequenceEqual(reference.Keys.AsSpan());
        }

        if (first is not { } firstAxis)
            return null;

        DataBuffer<float> combinedKeys = firstAxis.Keys;
        if (!sameKeys)
        {
            SortedSet<float> times = [];
            foreach (var channel in channels)
            {
                if (channel is not { } axis)
                    continue;
                foreach (float time in axis.Keys.AsSpan())
                    times.Add(time);
            }
            combinedKeys = new DataBuffer<float>([.. times], 1, 1);
        }

        float[] combinedValues	= new float[checked(combinedKeys.ElementCount * 3)];
        int[] cursors			= new int[axes.Length];
        for (int i = 0; i < combinedKeys.ElementCount; i++)
        {
            float time = combinedKeys.Get<float>(i, 0, 0);
            for (int axis = 0; axis < axes.Length; axis++)
            {
                float value = fallback;

                if (channels[axis] is { } channel)
                    value = SampleAxis(channel.Keys.AsSpan(), channel.Values.AsSpan(), time, ref cursors[axis]);
                combinedValues[i * 3 + axis] = value;
            }
        }

        return new AnimationCurve(TransformSpace.Local, firstAxis.Curve.TransformType)
        {
            Keys		= combinedKeys,
            Values		= new DataBuffer<float>(combinedValues, 1, 3),
            BlendWeight	= firstAxis.Curve.BlendWeight,
        };
    }

    private static float SampleAxis(ReadOnlySpan<float> keys, ReadOnlySpan<float> values, float time, ref int cursor)
    {
        while (cursor + 1 < keys.Length && keys[cursor + 1] <= time)
            cursor++;

        if (time <= keys[0])
            return values[0];
        if (cursor + 1 >= keys.Length)
            return values[cursor];

        return float.Lerp(values[cursor], values[cursor + 1], (time - keys[cursor]) / (keys[cursor + 1] - keys[cursor]));
    }

    private static void WriteVector3Curve(AnimationNode animationNode, string nodeName, AnimationCurve curve, TransformType mode, string xProperty, string yProperty, string zProperty)
    {
        DataBuffer valuesBuffer = curve.Values ?? throw new InvalidDataException("Missing Cast curve values.");
        if (valuesBuffer.ElementCount != curve.KeyFrameCount || valuesBuffer.ComponentCount != 3)
            throw new InvalidDataException("Invalid Cast vector curve dimensions.");
        string[] properties = [xProperty, yProperty, zProperty];

        for (var axis = 0; axis < properties.Length; axis++)
        {
            var values = new float[curve.KeyFrameCount];

            for (var i = 0; i < values.Length; i++)
                values[i] = valuesBuffer.Get<float>(i, 0, axis);

            var axisNode = animationNode.AddNode(new CurveNode
            {
                NodeName		= nodeName,
                KeyPropertyName	= properties[axis],
                Mode			= CastTranslator.ConvertFromTransformType(mode),
                KeyFrames		= CreateKeyFrames(curve),
                KeyValues		= CastArrayProperty.Create<float>(values),
            });

            if (curve.BlendWeight != 1.0f)
                axisNode.AdditiveBlendWeight = curve.BlendWeight;
        }
    }

    private static CastArrayProperty CreateKeyFrames(AnimationCurve curve)
    {
        var frames = new int[curve.KeyFrameCount];

        for (var i = 0; i < frames.Length; i++)
            frames[i] = ToFrame(curve.GetKeyTime(i));

        return CastArrayProperty.CreateIndices<int>(frames);
    }

    private static int ToFrame(float time) => Math.Max(0, (int)MathF.Round(time));

    private static TransformType GetMode(SkeletonAnimation animation, AnimationCurve curve)
    {
        if (curve.TransformType != TransformType.Unknown && curve.TransformType != TransformType.Parent)
            return curve.TransformType;

        return animation.TransformType;
    }
}
