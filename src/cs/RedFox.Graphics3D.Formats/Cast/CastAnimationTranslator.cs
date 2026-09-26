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
        var skeletalAnimation = new SkeletonAnimation(name) { TransformType = TransformType.Relative, Framerate = animationNode.Framerate };
        MorphAnimation? morphAnimation = null;

        foreach (var nodeCurves in animationNode.EnumerateCurves().GroupBy(curve => curve.NodeName))
        {
            var track = new SkeletonAnimationTrack(nodeCurves.Key) { TransformSpace = TransformSpace.Local };
            var translation = new CurveNode?[3];
            var scale = new CurveNode?[3];

            foreach (var curveNode in nodeCurves)
            {
                switch (curveNode.KeyPropertyName)
                {
                    case BlendShapeKeyProperty:
                        morphAnimation ??= new MorphAnimation($"{name}_Morph") { Framerate = animationNode.Framerate };
                        morphAnimation.GetOrCreateTrack(nodeCurves.Key).WeightCurve = CreateCurve(curveNode, 1);
                        break;
                    case "rq":
                        track.RotationCurve = CreateCurve(curveNode, 4);
                        break;
                    case "tx" or "ty" or "tz":
                        translation[curveNode.KeyPropertyName[1] - 'x'] = curveNode;
                        break;
                    case "sx" or "sy" or "sz":
                        scale[curveNode.KeyPropertyName[1] - 'x'] = curveNode;
                        break;
                    default:
                        if (CreateCurve(curveNode, 1) is AnimationCurve customCurve)
                            (track.CustomCurves ??= [])[curveNode.KeyPropertyName == "vb" ? VisibilityCurveName : curveNode.KeyPropertyName] = customCurve;
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
            var transformType = CastTranslator.ConvertToTransformType(curveModeOverride.Mode);
            var nodeNames = new HashSet<string> { curveModeOverride.NodeName };

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
        var animationNode = root.AddNode(new AnimationNode { Name = animation.Name, Framerate = animation.Framerate });

        foreach (var track in animation.Tracks)
        {
            if (track.RotationCurve is { KeyFrameCount: > 0 } rotationCurve)
            {
                var rotations = new Quaternion[rotationCurve.KeyFrameCount];

                for (var i = 0; i < rotations.Length; i++)
                    rotations[i] = rotationCurve.GetQuaternion(i);

                var rotationNode = animationNode.AddNode(new CurveNode { NodeName = track.Name, KeyPropertyName = "rq", Mode = CastTranslator.ConvertFromTransformType(GetMode(animation, rotationCurve)), KeyFrames = CreateKeyFrames(rotationCurve), KeyValues = CastArrayProperty.Create<Quaternion>(rotations) });

                if (rotationCurve.BlendWeight != 1.0f)
                    rotationNode.AdditiveBlendWeight = rotationCurve.BlendWeight;
            }

            if (track.TranslationCurve is { KeyFrameCount: > 0 } translationCurve)
                WriteVector3Curve(animationNode, track.Name, translationCurve, GetMode(animation, translationCurve), "tx", "ty", "tz");
            if (track.ScaleCurve is { KeyFrameCount: > 0 } scaleCurve)
                WriteVector3Curve(animationNode, track.Name, scaleCurve, GetMode(animation, scaleCurve), "sx", "sy", "sz");

            foreach (var (curveName, customCurve) in track.CustomCurves?.Where(entry => entry.Value is { KeyFrameCount: > 0, ComponentCount: 1 }) ?? [])
            {
                var isVisibility = curveName == VisibilityCurveName;
                var values = new CastArrayProperty(isVisibility ? CastPropertyType.Byte : CastPropertyType.Float, customCurve.KeyFrameCount);

                for (var i = 0; i < customCurve.KeyFrameCount; i++)
                {
                    if (isVisibility)
                        values.Add(customCurve.GetScalar(i) >= 1.0f ? (byte)1 : (byte)0);
                    else
                        values.Add(customCurve.GetScalar(i));
                }

                animationNode.AddNode(new CurveNode { NodeName = track.Name, KeyPropertyName = isVisibility ? "vb" : curveName, Mode = "absolute", KeyFrames = CreateKeyFrames(customCurve), KeyValues = values });
            }
        }

        foreach (var action in animation.Actions ?? [])
            animationNode.AddNode(new NotificationTrackNode { Name = action.Name, KeyFrames = CastArrayProperty.CreateIndices<int>([.. action.KeyFrames.Select(keyFrame => ToFrame(keyFrame.Frame))]) });
    }

    public static void Write(RootNode root, MorphAnimation animation)
    {
        var animationNode = root.AddNode(new AnimationNode { Name = animation.Name, Framerate = animation.Framerate });

        foreach (var track in animation.Tracks)
        {
            if (track.WeightCurve is not { KeyFrameCount: > 0 } weightCurve)
                continue;

            var weights = new float[weightCurve.KeyFrameCount];

            for (var i = 0; i < weights.Length; i++)
                weights[i] = weightCurve.GetScalar(i);

            animationNode.AddNode(new CurveNode { NodeName = track.Name, KeyPropertyName = BlendShapeKeyProperty, Mode = "absolute", KeyFrames = CreateKeyFrames(weightCurve), KeyValues = CastArrayProperty.Create<float>(weights) });
        }
    }

    private static AnimationCurve? CreateCurve(CurveNode curveNode, int componentCount)
    {
        if (curveNode.KeyFrames is not CastArrayProperty keyFrames || curveNode.KeyValues is not CastArrayProperty keyValues)
            return null;
        if (componentCount == 4 && keyValues.Type != CastPropertyType.Vector4)
            return null;
        if (componentCount == 1 && keyValues.Type is CastPropertyType.Vector2 or CastPropertyType.Vector3 or CastPropertyType.Vector4)
            return null;

        var count = Math.Min(keyFrames.Count, keyValues.Count);

        if (count == 0)
            return null;

        var frames = keyFrames.ToArray<float>()[..count];
        var values = componentCount == 4 ? MemoryMarshal.Cast<byte, float>(keyValues.AsBytes())[..(count * 4)].ToArray() : keyValues.ToArray<float>()[..count];

        return new AnimationCurve(TransformSpace.Local, CastTranslator.ConvertToTransformType(curveNode.Mode)) { Keys = new DataBuffer<float>(frames, 1, 1), Values = new DataBuffer<float>(values, 1, componentCount), BlendWeight = curveNode.AdditiveBlendWeight };
    }

    private static AnimationCurve? CombineAxes(CurveNode?[] axes, float fallback)
    {
        var curves = Array.ConvertAll(axes, axis => axis is null ? null : CreateCurve(axis, 1));

        if (Array.Find(curves, curve => curve is not null) is not AnimationCurve first)
            return null;

        var keys = Array.ConvertAll(curves, curve => curve?.Keys is DataBuffer<float> buffer ? buffer.AsSpan().ToArray() : null);
        var values = Array.ConvertAll(curves, curve => curve?.Values is DataBuffer<float> buffer ? buffer.AsSpan().ToArray() : null);
        var times = Array.TrueForAll(keys, axisKeys => axisKeys is not null && axisKeys.AsSpan().SequenceEqual(keys[0])) ? keys[0]! : [.. keys.SelectMany(axisKeys => axisKeys ?? []).Distinct().Order()];
        var combined = new float[times.Length * 3];
        var cursors = new int[3];

        for (var i = 0; i < times.Length; i++)
        {
            for (var axis = 0; axis < 3; axis++)
                combined[i * 3 + axis] = keys[axis] is float[] axisKeys ? SampleAxis(axisKeys, values[axis]!, times[i], ref cursors[axis]) : fallback;
        }

        return new AnimationCurve(TransformSpace.Local, first.TransformType) { Keys = new DataBuffer<float>(times, 1, 1), Values = new DataBuffer<float>(combined, 1, 3), BlendWeight = first.BlendWeight };
    }

    private static float SampleAxis(float[] keys, float[] values, float time, ref int cursor)
    {
        while (cursor + 1 < keys.Length && keys[cursor + 1] <= time)
            cursor++;

        if (time <= keys[0] || cursor + 1 >= keys.Length)
            return values[time <= keys[0] ? 0 : cursor];

        return float.Lerp(values[cursor], values[cursor + 1], (time - keys[cursor]) / (keys[cursor + 1] - keys[cursor]));
    }

    private static void WriteVector3Curve(AnimationNode animationNode, string nodeName, AnimationCurve curve, TransformType mode, string xProperty, string yProperty, string zProperty)
    {
        string[] properties = [xProperty, yProperty, zProperty];

        for (var axis = 0; axis < properties.Length; axis++)
        {
            var values = new float[curve.KeyFrameCount];

            for (var i = 0; i < values.Length; i++)
                values[i] = curve.Values!.Get<float>(i, 0, axis);

            var axisNode = animationNode.AddNode(new CurveNode { NodeName = nodeName, KeyPropertyName = properties[axis], Mode = CastTranslator.ConvertFromTransformType(mode), KeyFrames = CreateKeyFrames(curve), KeyValues = CastArrayProperty.Create<float>(values) });

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
