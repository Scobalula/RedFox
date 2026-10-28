using System.Numerics;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Writes skeleton and morph animations as FBX animation stacks sampled once per frame.
/// </summary>
public static class FbxAnimationExporter
{
    private const int LinearInterpolation = 0x00000004;

    /// <summary>
    /// Returns the frame rate of the first animation in the selection, or 30 when there is none.
    /// </summary>
    /// <param name="selection">The exported selection.</param>
    /// <returns>The frame rate in frames per second.</returns>
    public static double GetFrameRate(SceneTranslationSelection selection)
    {
        foreach (Animation animation in selection.GetDescendants<Animation>())
        {
            if (animation.Framerate > 0.0f)
                return animation.Framerate;
        }

        return 30.0;
    }

    /// <summary>
    /// Writes every selected skeleton and morph animation.
    /// </summary>
    /// <param name="exporter">The active scene exporter.</param>
    public static void Export(FbxSceneExporter exporter)
    {
        Dictionary<string, SceneNode> targets = new(StringComparer.Ordinal);

        foreach (SceneNode node in exporter.ModelIds.Keys)
            targets.TryAdd(node.Name, node);

        foreach (SkeletonAnimation animation in exporter.Selection.GetDescendants<SkeletonAnimation>())
            ExportSkeletonAnimation(exporter, animation, targets);

        foreach (MorphAnimation animation in exporter.Selection.GetDescendants<MorphAnimation>())
            ExportMorphAnimation(exporter, animation);
    }

    private static long CreateStack(FbxSceneExporter exporter, Animation animation, out long firstFrame, out long lastFrame, out double frameRate)
    {
        (float start, float end) = animation.GetAnimationFrameRange();

        frameRate	= animation.Framerate > 0.0f ? animation.Framerate : 30.0;
        firstFrame	= (long)MathF.Floor(start);
        lastFrame	= Math.Max(firstFrame, (long)MathF.Ceiling(end));

        long	startTicks	= ToTicks(firstFrame, frameRate);
        long	stopTicks	= ToTicks(lastFrame, frameRate);
        long	stackId		= exporter.AddObject("AnimationStack", animation.Name, string.Empty, out FbxNode stack);
        long	layerId		= exporter.AddObject("AnimationLayer", "BaseLayer", string.Empty, out _);
        FbxNode	properties	= stack.AddChild("Properties70");

        FbxSceneExporter.AddTime(properties, "LocalStart", startTicks);
        FbxSceneExporter.AddTime(properties, "LocalStop", stopTicks);
        FbxSceneExporter.AddTime(properties, "ReferenceStart", startTicks);
        FbxSceneExporter.AddTime(properties, "ReferenceStop", stopTicks);

        exporter.Connect(layerId, stackId);
        exporter.Takes.Add(new FbxExportedTake(animation.Name, startTicks, stopTicks));

        return layerId;
    }

    private static long ToTicks(long frame, double frameRate)
    {
        return (long)Math.Round(frame * (FbxAnimationCurve.TicksPerSecond / frameRate));
    }

    private static bool IsAbsoluteLocal(AnimationCurve? curve)
    {
        return curve is null || (curve.TransformType is TransformType.Absolute or TransformType.Unknown && curve.TransformSpace == TransformSpace.Local);
    }

    private static void ExportSkeletonAnimation(FbxSceneExporter exporter, SkeletonAnimation animation, Dictionary<string, SceneNode> targets)
    {
        long	layerId		= CreateStack(exporter, animation, out long firstFrame, out long lastFrame, out double frameRate);
        int		frameCount	= (int)(lastFrame - firstFrame + 1);
        long[]	times		= new long[frameCount];

        for (int i = 0; i < frameCount; i++)
            times[i] = ToTicks(firstFrame + i, frameRate);

        SkeletonAnimationSampler							sampler		= new("FbxExport", animation, exporter.Selection.Scene);
        Dictionary<SkeletonBone, Transform>					saved		= new(ReferenceEqualityComparer.Instance);
        Dictionary<SkeletonAnimationTrack, FbxSampledTrack>	sampled		= [];
        HashSet<SkeletonAnimationTrack>						resolved	= [];

        foreach ((SkeletonBone bone, SkeletonAnimationTrack track) in sampler.Tracks)
        {
            if (!IsAbsoluteLocal(track.TranslationCurve) || !IsAbsoluteLocal(track.RotationCurve) || !IsAbsoluteLocal(track.ScaleCurve))
            {
                resolved.Add(track);
                saved[bone] = bone.LiveTransform.Clone();
            }
        }

        foreach (SkeletonAnimationTrack track in animation.Tracks)
        {
            if (targets.TryGetValue(track.Name, out SceneNode? node))
                sampled[track] = new FbxSampledTrack(node, frameCount);
        }

        for (int i = 0; i < frameCount; i++)
        {
            float frame = firstFrame + i;

            if (resolved.Count > 0)
            {
                foreach (SkeletonBone bone in saved.Keys)
                    bone.ResetLiveTransform();

                sampler.Update(frame, AnimationSampleType.AbsoluteFrameTime);
            }

            foreach ((SkeletonAnimationTrack track, FbxSampledTrack samples) in sampled)
            {
                if (resolved.Contains(track))
                {
                    FbxTransform.Decompose(samples.Node.GetActiveLocalMatrix(), out Vector3 translation, out Quaternion rotation, out Vector3 scale);
                    samples.Set(i, translation, rotation, scale);
                }
                else
                {
                    samples.Set(i, track.TranslationCurve?.SampleVector3(frame) ?? samples.Node.GetBindLocalPosition(), track.RotationCurve?.SampleQuaternion(frame) ?? samples.Node.GetBindLocalRotation(), track.ScaleCurve?.SampleVector3(frame) ?? samples.Node.GetBindLocalScale());
                }
            }
        }

        foreach ((SkeletonBone bone, Transform transform) in saved)
            transform.CopyTo(bone.LiveTransform);

        foreach ((SkeletonAnimationTrack track, FbxSampledTrack samples) in sampled)
        {
            long modelId = exporter.ModelIds[samples.Node];

            if (track.TranslationCurve is not null)
                WriteCurveNode(exporter, layerId, modelId, "T", "Lcl Translation", times, samples.Translations);

            if (track.RotationCurve is not null)
                WriteCurveNode(exporter, layerId, modelId, "R", "Lcl Rotation", times, samples.GetEulerRotations());

            if (track.ScaleCurve is not null)
                WriteCurveNode(exporter, layerId, modelId, "S", "Lcl Scaling", times, samples.Scales);
        }
    }

    private static void ExportMorphAnimation(FbxSceneExporter exporter, MorphAnimation animation)
    {
        long	layerId		= CreateStack(exporter, animation, out long firstFrame, out long lastFrame, out double frameRate);
        int		frameCount	= (int)(lastFrame - firstFrame + 1);
        long[]	times		= new long[frameCount];

        for (int i = 0; i < frameCount; i++)
            times[i] = ToTicks(firstFrame + i, frameRate);

        foreach (MorphAnimationTrack track in animation.Tracks)
        {
            if (!exporter.BlendShapeChannels.TryGetValue(track.Name, out List<long>? channels))
                continue;

            float[] weights = new float[frameCount];

            for (int i = 0; i < frameCount; i++)
                weights[i] = track.SampleWeight(firstFrame + i) * 100.0f;

            long curveNodeId = exporter.AddObject("AnimationCurveNode", "DeformPercent", string.Empty, out FbxNode curveNode);

            FbxSceneExporter.AddNumber(curveNode.AddChild("Properties70"), "d|DeformPercent", "A", weights[0]);
            exporter.Connect(curveNodeId, layerId);
            exporter.Connect(WriteCurve(exporter, times, weights), curveNodeId, "d|DeformPercent");

            foreach (long channelId in channels)
                exporter.Connect(curveNodeId, channelId, "DeformPercent");
        }
    }

    private static void WriteCurveNode(FbxSceneExporter exporter, long layerId, long modelId, string name, string property, long[] times, Vector3[] values)
    {
        long	curveNodeId	= exporter.AddObject("AnimationCurveNode", name, string.Empty, out FbxNode curveNode);
        FbxNode	properties	= curveNode.AddChild("Properties70");

        FbxSceneExporter.AddNumber(properties, "d|X", "A", values[0].X);
        FbxSceneExporter.AddNumber(properties, "d|Y", "A", values[0].Y);
        FbxSceneExporter.AddNumber(properties, "d|Z", "A", values[0].Z);

        exporter.Connect(curveNodeId, layerId);
        exporter.Connect(curveNodeId, modelId, property);
        exporter.Connect(WriteCurve(exporter, times, Array.ConvertAll(values, static value => value.X)), curveNodeId, "d|X");
        exporter.Connect(WriteCurve(exporter, times, Array.ConvertAll(values, static value => value.Y)), curveNodeId, "d|Y");
        exporter.Connect(WriteCurve(exporter, times, Array.ConvertAll(values, static value => value.Z)), curveNodeId, "d|Z");
    }

    private static long WriteCurve(FbxSceneExporter exporter, long[] times, float[] values)
    {
        bool	constant	= Array.TrueForAll(values, value => value == values[0]);
        long[]	keyTimes	= constant ? [times[0]] : times;
        float[]	keyValues	= constant ? [values[0]] : values;
        long	curveId		= exporter.AddObject("AnimationCurve", string.Empty, string.Empty, out FbxNode curve);

        curve.AddChild("Default", new FbxProperty((double)keyValues[0]));
        curve.AddChild("KeyVer", new FbxProperty(4009));
        curve.AddChild("KeyTime", new FbxProperty(keyTimes));
        curve.AddChild("KeyValueFloat", new FbxProperty(keyValues));
        curve.AddChild("KeyAttrFlags", new FbxProperty([LinearInterpolation]));
        curve.AddChild("KeyAttrDataFloat", new FbxProperty([0.0f, 0.0f, BitConverter.Int32BitsToSingle(218434821), 0.0f]));
        curve.AddChild("KeyAttrRefCount", new FbxProperty([keyTimes.Length]));

        return curveId;
    }
}
