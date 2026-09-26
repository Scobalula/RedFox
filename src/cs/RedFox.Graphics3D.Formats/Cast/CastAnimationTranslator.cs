using Cast.NET;
using Cast.NET.Nodes;
using RedFox.Graphics3D.Skeletal;
using System.Numerics;

namespace RedFox.Graphics3D.Formats.Cast;

internal static class CastAnimationTranslator
{
    private const string VisibilityCurveName = "visibility";

    private const string BlendShapeKeyProperty = "bs";

    private static readonly string[] TransformKeyProperties = ["rq", "tx", "ty", "tz", "sx", "sy", "sz"];

    public static void Read(Scene scene, AnimationNode animationNode, string name)
    {
        var skeletalAnimation = new SkeletonAnimation(name)
        {
            TransformType = TransformType.Relative,
            Framerate = animationNode.Framerate,
        };

        var curveNodes = Array.FindAll(animationNode.Curves, x => x.KeyPropertyName != BlendShapeKeyProperty);
        var morphAnimation = ReadMorphAnimation(animationNode, name);

        foreach (var curveName in curveNodes.Select(x => x.NodeName).Distinct())
        {
            var nodeCurves = Array.FindAll(curveNodes, x => x.NodeName == curveName);
            var rq = Array.Find(nodeCurves, x => x.KeyPropertyName == "rq");
            var tx = Array.Find(nodeCurves, x => x.KeyPropertyName == "tx");
            var ty = Array.Find(nodeCurves, x => x.KeyPropertyName == "ty");
            var tz = Array.Find(nodeCurves, x => x.KeyPropertyName == "tz");
            var sx = Array.Find(nodeCurves, x => x.KeyPropertyName == "sx");
            var sy = Array.Find(nodeCurves, x => x.KeyPropertyName == "sy");
            var sz = Array.Find(nodeCurves, x => x.KeyPropertyName == "sz");

            var track = new SkeletonAnimationTrack(curveName)
            {
                TransformSpace = TransformSpace.Local,
            };

            // Rotation
            if (rq is not null)
            {
                track.TransformType = CastTranslator.ConvertToTransformType(rq.Mode);

                var rqKeyFrames = rq.EnumerateKeyFrames().ToArray();
                var rqKeyValues = rq.EnumerateKeyValues<Vector4>().ToArray();

                for (int i = 0; i < rqKeyFrames.Length && i < rqKeyValues.Length; i++)
                {
                    var v = rqKeyValues[i];
                    track.AddRotationFrame((float)rqKeyFrames[i], new Quaternion(v.X, v.Y, v.Z, v.W));
                }
            }

            // Translation and scale — Cast stores each axis as a separate curve, we need to combine
            if (tx is not null || ty is not null || tz is not null)
            {
                track.TransformType = CastTranslator.ConvertToTransformType((tx ?? ty ?? tz)!.Mode);
                ReadVector3Curve(tx, ty, tz, 0.0f, track.AddTranslationFrame);
            }

            if (sx is not null || sy is not null || sz is not null)
                ReadVector3Curve(sx, sy, sz, 1.0f, track.AddScaleFrame);

            // Anything else is a single value curve, visibility being the only one Cast standardizes
            foreach (var customCurve in nodeCurves.Where(x => !TransformKeyProperties.Contains(x.KeyPropertyName)))
            {
                var curve = track.GetOrCreateCustomCurve(customCurve.KeyPropertyName == "vb" ? VisibilityCurveName : customCurve.KeyPropertyName, 1);
                var keyFrames = customCurve.EnumerateKeyFrames().ToArray();
                var keyValues = customCurve.KeyPropertyName == "vb" ? [.. customCurve.EnumerateKeyValues<byte>().Select(x => (float)x)] : customCurve.EnumerateKeyValues<float>().ToArray();

                for (int i = 0; i < keyFrames.Length && i < keyValues.Length; i++)
                    curve.Add((float)keyFrames[i], keyValues[i]);
            }

            skeletalAnimation.Tracks.Add(track);
        }

        // Notification tracks → Actions
        foreach (var noteTrack in animationNode.EnumerateNotificationTracks())
        {
            var action = skeletalAnimation.CreateAction(noteTrack.Name);

            foreach (var frame in noteTrack.EnumerateKeyFrames())
            {
                action.KeyFrames.Add(new((float)frame, null));
            }
        }

        if (skeletalAnimation.Tracks.Count > 0 || skeletalAnimation.Actions is { Count: > 0 } || morphAnimation is null)
            scene.RootNode.AddNode(skeletalAnimation);

        if (morphAnimation is not null)
            scene.RootNode.AddNode(morphAnimation);
    }

    public static void Write(CastNode root, MorphAnimation animation)
    {
        var animationNode = root.AddNode<AnimationNode>();

        animationNode.AddValue("fr", animation.Framerate);
        animationNode.AddValue("lo", (byte)0);

        foreach (var track in animation.Tracks)
        {
            if (track.WeightCurve is not { KeyFrameCount: > 0 } weightCurve)
                continue;

            var curveNode = animationNode.AddNode<CurveNode>();

            curveNode.Mode = CastTranslator.ConvertFromTransformType(TransformType.Absolute);
            curveNode.NodeName = track.Name;
            curveNode.KeyPropertyName = BlendShapeKeyProperty;

            var keyValues = curveNode.AddArray<float>("kv", weightCurve.KeyFrameCount);
            var keyFrames = curveNode.AddArray<uint>("kb", weightCurve.KeyFrameCount);

            for (int i = 0; i < weightCurve.KeyFrameCount; i++)
            {
                keyValues.Add(weightCurve.GetScalar(i));
                keyFrames.Add((uint)weightCurve.GetKeyTime(i));
            }
        }
    }

    private static MorphAnimation? ReadMorphAnimation(AnimationNode animationNode, string name)
    {
        var curveNodes = Array.FindAll(animationNode.Curves, x => x.KeyPropertyName == BlendShapeKeyProperty);

        if (curveNodes.Length == 0)
            return null;

        var morphAnimation = new MorphAnimation($"{name}_Morph") { Framerate = animationNode.Framerate };

        foreach (var curveNode in curveNodes)
        {
            var track = morphAnimation.GetOrCreateTrack(curveNode.NodeName);
            var keyFrames = curveNode.EnumerateKeyFrames().ToArray();
            var keyValues = curveNode.EnumerateKeyValues<float>().ToArray();

            for (int i = 0; i < keyFrames.Length && i < keyValues.Length; i++)
                track.AddWeightFrame((float)keyFrames[i], keyValues[i]);
        }

        return morphAnimation;
    }

    public static void Write(CastNode root, SkeletonAnimation animation)
    {
        var animationNode = root.AddNode<AnimationNode>();

        animationNode.AddValue("fr", animation.Framerate);
        animationNode.AddValue("lo", (byte)0);

        foreach (var track in animation.Tracks)
        {
            // Rotation curve → single "rq" curve
            if (track.RotationCurve is { KeyFrameCount: > 0 } rotCurve)
            {
                var rCurve = animationNode.AddNode<CurveNode>();

                rCurve.Mode = CastTranslator.ConvertFromTransformType(GetMode(animation, rotCurve));
                rCurve.NodeName = track.Name;
                rCurve.KeyPropertyName = "rq";

                var rKeyValueBuffer = rCurve.AddArray<Vector4>("kv", rotCurve.KeyFrameCount);
                var rKeyFrameBuffer = rCurve.AddArray<uint>("kb", rotCurve.KeyFrameCount);

                for (int i = 0; i < rotCurve.KeyFrameCount; i++)
                {
                    rKeyValueBuffer.Add(CastHelpers.CreateVector4FromQuaternion(rotCurve.GetQuaternion(i)));
                    rKeyFrameBuffer.Add((uint)rotCurve.GetKeyTime(i));
                }
            }

            // Translation and scale curves → separate curves per axis
            if (track.TranslationCurve is { KeyFrameCount: > 0 } transCurve)
                WriteVector3Curve(animationNode, track.Name, transCurve, GetMode(animation, transCurve), "tx", "ty", "tz");
            if (track.ScaleCurve is { KeyFrameCount: > 0 } scaleCurve)
                WriteVector3Curve(animationNode, track.Name, scaleCurve, GetMode(animation, scaleCurve), "sx", "sy", "sz");

            // Custom curves → single value curves keyed by their name, visibility maps to Cast's "vb" curve
            if (track.CustomCurves is not null)
            {
                foreach (var (curveName, customCurve) in track.CustomCurves.Where(x => x.Value is { KeyFrameCount: > 0, ComponentCount: 1 }))
                    WriteCustomCurve(animationNode, track.Name, curveName, customCurve);
            }
        }

        // Actions → notification tracks
        if (animation.Actions is not null)
        {
            foreach (var action in animation.Actions)
            {
                var notetrackNode = animationNode.AddNode<NotificationTrackNode>();
                var keyFrameBuffer = new CastArrayProperty<uint>();

                foreach (var keyFrame in action.KeyFrames)
                {
                    keyFrameBuffer.Add((uint)keyFrame.Frame);
                }

                notetrackNode.Name = action.Name;
                notetrackNode.KeyFrameBuffer = keyFrameBuffer;
            }
        }
    }

    private static void ReadVector3Curve(CurveNode? x, CurveNode? y, CurveNode? z, float fallback, Action<float, Vector3> addFrame)
    {
        var xFrames = x?.EnumerateKeyFrames().Select(f => (float)f).ToArray();
        var yFrames = y?.EnumerateKeyFrames().Select(f => (float)f).ToArray();
        var zFrames = z?.EnumerateKeyFrames().Select(f => (float)f).ToArray();

        var xValues = x?.EnumerateKeyValues<float>().ToArray();
        var yValues = y?.EnumerateKeyValues<float>().ToArray();
        var zValues = z?.EnumerateKeyValues<float>().ToArray();

        // Collect all unique frame times
        var allFrameTimes = new SortedSet<float>((xFrames ?? []).Concat(yFrames ?? []).Concat(zFrames ?? []));

        foreach (var time in allFrameTimes)
            addFrame(time, new Vector3(SampleChannel(xFrames, xValues, time, fallback), SampleChannel(yFrames, yValues, time, fallback), SampleChannel(zFrames, zValues, time, fallback)));
    }

    private static void WriteVector3Curve(AnimationNode animationNode, string nodeName, AnimationCurve curve, TransformType mode, string xProperty, string yProperty, string zProperty)
    {
        string[] properties = [xProperty, yProperty, zProperty];

        for (int axis = 0; axis < properties.Length; axis++)
        {
            var axisCurve = animationNode.AddNode<CurveNode>();

            axisCurve.Mode = CastTranslator.ConvertFromTransformType(mode);
            axisCurve.NodeName = nodeName;
            axisCurve.KeyPropertyName = properties[axis];

            var keyValues = axisCurve.AddArray<float>("kv", curve.KeyFrameCount);
            var keyFrames = axisCurve.AddArray<uint>("kb", curve.KeyFrameCount);

            for (int i = 0; i < curve.KeyFrameCount; i++)
            {
                keyValues.Add(curve.Values!.Get<float>(i, 0, axis));
                keyFrames.Add((uint)curve.GetKeyTime(i));
            }
        }
    }

    private static void WriteCustomCurve(AnimationNode animationNode, string nodeName, string curveName, AnimationCurve curve)
    {
        var customCurve = animationNode.AddNode<CurveNode>();
        var isVisibility = curveName == VisibilityCurveName;

        customCurve.Mode = CastTranslator.ConvertFromTransformType(TransformType.Absolute);
        customCurve.NodeName = nodeName;
        customCurve.KeyPropertyName = isVisibility ? "vb" : curveName;

        var keyFrames = customCurve.AddArray<uint>("kb", curve.KeyFrameCount);

        if (isVisibility)
        {
            var keyValues = customCurve.AddArray<byte>("kv", curve.KeyFrameCount);

            for (int i = 0; i < curve.KeyFrameCount; i++)
                keyValues.Add(curve.GetScalar(i) >= 1.0f ? (byte)1 : (byte)0);
        }
        else
        {
            var keyValues = customCurve.AddArray<float>("kv", curve.KeyFrameCount);

            for (int i = 0; i < curve.KeyFrameCount; i++)
                keyValues.Add(curve.GetScalar(i));
        }

        for (int i = 0; i < curve.KeyFrameCount; i++)
            keyFrames.Add((uint)curve.GetKeyTime(i));
    }

    private static TransformType GetMode(SkeletonAnimation animation, AnimationCurve curve)
    {
        if (curve.TransformType != TransformType.Unknown && curve.TransformType != TransformType.Parent)
            return curve.TransformType;

        return animation.TransformType;
    }

    private static float SampleChannel(float[]? frames, float[]? values, float time, float fallback)
    {
        if (frames is null || values is null || frames.Length == 0)
            return fallback;
        if (frames.Length == 1)
            return values[0];
        if (time <= frames[0])
            return values[0];
        if (time >= frames[^1])
            return values[^1];

        for (int i = 1; i < frames.Length; i++)
        {
            if (time <= frames[i])
            {
                float t = (time - frames[i - 1]) / (frames[i] - frames[i - 1]);
                return float.Lerp(values[i - 1], values[i], t);
            }
        }

        return values[^1];
    }
}
