using Cast.NET;
using Cast.NET.Nodes;
using RedFox.Graphics3D.Skeletal;
using System.Numerics;

namespace RedFox.Graphics3D.Cast;

internal static class CastAnimationTranslator
{
    public static void Read(Scene scene, AnimationNode animationNode, string name)
    {
        var skeletalAnimation = new SkeletonAnimation(name)
        {
            TransformType = TransformType.Relative,
            Framerate = animationNode.Framerate,
        };

        var curveNodes = animationNode.Curves;
        var uniqueCurveNames = new HashSet<string>();

        foreach (var curveNode in curveNodes)
        {
            uniqueCurveNames.Add(curveNode.NodeName);
        }

        foreach (var curveName in uniqueCurveNames)
        {
            var tx = Array.Find(curveNodes, x => x.NodeName == curveName && x.KeyPropertyName == "tx");
            var ty = Array.Find(curveNodes, x => x.NodeName == curveName && x.KeyPropertyName == "ty");
            var tz = Array.Find(curveNodes, x => x.NodeName == curveName && x.KeyPropertyName == "tz");
            var rq = Array.Find(curveNodes, x => x.NodeName == curveName && x.KeyPropertyName == "rq");

            if (tx is null && ty is null && tz is null && rq is null)
                continue;

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

            // Translation — Cast stores tx/ty/tz as separate curves, we need to combine
            if (tx is not null || ty is not null || tz is not null)
            {
                var mode = TransformType.Relative;
                if (tx is not null) mode = CastTranslator.ConvertToTransformType(tx.Mode);
                else if (ty is not null) mode = CastTranslator.ConvertToTransformType(ty.Mode);
                else if (tz is not null) mode = CastTranslator.ConvertToTransformType(tz.Mode);

                track.TransformType = mode;

                var txFrames = tx?.EnumerateKeyFrames().Select(f => (float)f).ToArray();
                var tyFrames = ty?.EnumerateKeyFrames().Select(f => (float)f).ToArray();
                var tzFrames = tz?.EnumerateKeyFrames().Select(f => (float)f).ToArray();

                var txValues = tx?.EnumerateKeyValues<float>().ToArray();
                var tyValues = ty?.EnumerateKeyValues<float>().ToArray();
                var tzValues = tz?.EnumerateKeyValues<float>().ToArray();

                // Collect all unique frame times
                var allFrameTimes = new SortedSet<float>();
                if (txFrames is not null) foreach (var f in txFrames) allFrameTimes.Add(f);
                if (tyFrames is not null) foreach (var f in tyFrames) allFrameTimes.Add(f);
                if (tzFrames is not null) foreach (var f in tzFrames) allFrameTimes.Add(f);

                foreach (var time in allFrameTimes)
                {
                    float xVal = SampleChannel(txFrames, txValues, time);
                    float yVal = SampleChannel(tyFrames, tyValues, time);
                    float zVal = SampleChannel(tzFrames, tzValues, time);

                    track.AddTranslationFrame(time, new Vector3(xVal, yVal, zVal));
                }
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

        scene.RootNode.AddNode(skeletalAnimation);
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

                var mode = animation.TransformType;
                if (rotCurve.TransformType != TransformType.Unknown && rotCurve.TransformType != TransformType.Parent)
                    mode = rotCurve.TransformType;

                rCurve.Mode = CastTranslator.ConvertFromTransformType(mode);
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

            // Translation curve → separate tx/ty/tz curves
            if (track.TranslationCurve is { KeyFrameCount: > 0 } transCurve)
            {
                var xCurve = animationNode.AddNode<CurveNode>();
                var yCurve = animationNode.AddNode<CurveNode>();
                var zCurve = animationNode.AddNode<CurveNode>();

                var mode = animation.TransformType;
                if (transCurve.TransformType != TransformType.Unknown && transCurve.TransformType != TransformType.Parent)
                    mode = transCurve.TransformType;

                var modeStr = CastTranslator.ConvertFromTransformType(mode);
                xCurve.Mode = modeStr;
                yCurve.Mode = modeStr;
                zCurve.Mode = modeStr;

                xCurve.NodeName = track.Name;
                yCurve.NodeName = track.Name;
                zCurve.NodeName = track.Name;

                xCurve.KeyPropertyName = "tx";
                yCurve.KeyPropertyName = "ty";
                zCurve.KeyPropertyName = "tz";

                var xKeyValues = xCurve.AddArray<float>("kv", transCurve.KeyFrameCount);
                var yKeyValues = yCurve.AddArray<float>("kv", transCurve.KeyFrameCount);
                var zKeyValues = zCurve.AddArray<float>("kv", transCurve.KeyFrameCount);
                var xKeyFrames = xCurve.AddArray<uint>("kb", transCurve.KeyFrameCount);
                var yKeyFrames = yCurve.AddArray<uint>("kb", transCurve.KeyFrameCount);
                var zKeyFrames = zCurve.AddArray<uint>("kb", transCurve.KeyFrameCount);

                for (int i = 0; i < transCurve.KeyFrameCount; i++)
                {
                    var vec = transCurve.GetVector3(i);
                    var frame = (uint)transCurve.GetKeyTime(i);

                    xKeyValues.Add(vec.X);
                    yKeyValues.Add(vec.Y);
                    zKeyValues.Add(vec.Z);

                    xKeyFrames.Add(frame);
                    yKeyFrames.Add(frame);
                    zKeyFrames.Add(frame);
                }
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

    private static float SampleChannel(float[]? frames, float[]? values, float time)
    {
        if (frames is null || values is null || frames.Length == 0)
            return 0f;
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
