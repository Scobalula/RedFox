using Cast.NET;
using Cast.NET.Nodes;
using RedFox.Graphics3D.Interpolators;
using RedFox.Graphics3D.Skeletal;
using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Xml;

namespace RedFox.Graphics3D.Cast
{
    /// <summary>
    /// A class to translate a Model to SEModel
    /// </summary>
    public sealed partial class CastTranslator : Graphics3DTranslator
    {
        /// <inheritdoc/>
        public override string Name => nameof(CastTranslator);

        /// <inheritdoc/>
        public override bool SupportsReading => true;

        /// <inheritdoc/>
        public override bool SupportsWriting => true;

        /// <inheritdoc/>
        public override string[] Extensions => [".cast"];

        /// <inheritdoc/>
        public override void Read(Stream stream, string filePath, Graphics3DScene scene)
        {
            var cast = CastReader.Load(stream);
            var fileName = Path.GetFileName(filePath);
            var root = cast.RootNodes[0];


            foreach (var modelNode in root.EnumerateChildrenOfType<ModelNode>())
            {
                scene.AddObject(CastModelTranslator.TranslateFrom(scene, modelNode, fileName + ".model." + modelNode.GetHashCode()));
            }

            foreach (var animation in root.EnumerateChildrenOfType<AnimationNode>())
            {
                var skeletalAnimation = new SkeletonAnimation("john")
                {
                    TransformType = TransformType.Relative,
                    Framerate = animation.Framerate,
                };
                var uniqueCurveNames = new HashSet<string>();
                var curveNodes = animation.Curves;

                foreach (var curveNode in curveNodes)
                {
                    uniqueCurveNames.Add(curveNode.NodeName);
                }

                skeletalAnimation.Tracks = [];

                var minFrame = double.MaxValue;
                var maxFrame = double.MinValue;

                // Calculate min/max key frames
                foreach (var curveNode in curveNodes)
                {
                    foreach (var frame in curveNode.EnumerateKeyFrames())
                    {
                        minFrame = Math.Min(minFrame, frame);
                        maxFrame = Math.Max(maxFrame, frame);
                    }
                }

                Console.WriteLine();

                foreach (var curveName in uniqueCurveNames)
                {
                    var tx = curveNodes.FirstOrDefault(x => x.NodeName == curveName && x.KeyPropertyName == "tx");
                    var ty = curveNodes.FirstOrDefault(x => x.NodeName == curveName && x.KeyPropertyName == "ty");
                    var tz = curveNodes.FirstOrDefault(x => x.NodeName == curveName && x.KeyPropertyName == "tz");
                    var rq = curveNodes.FirstOrDefault(x => x.NodeName == curveName && x.KeyPropertyName == "rq");

                    // All null
                    if (tx == null && ty == null && tz == null && rq == null)
                        continue;

                    var txKeyFrames = tx?.EnumerateKeys<float>().ToArray();
                    var tyKeyFrames = ty?.EnumerateKeys<float>().ToArray();
                    var tzKeyFrames = tz?.EnumerateKeys<float>().ToArray();
                    var rqKeyFrames = rq?.EnumerateKeys<Vector4>().ToArray();

                    var track = new SkeletonAnimationTrack(curveName);

                    // Rotation
                    if (rq is not null)
                    {
                        track.RotationCurve = new(TransformSpace.Local, ConvertToTransformType(rq.Mode));

                        foreach (var (key, value) in rq.EnumerateKeys<Vector4>())
                        {
                            track.RotationCurve.KeyFrames.Add(new((float)key, new(value.X, value.Y, value.Z, value.W)));
                        }
                    }

                    if (tx is not null || ty is not null || tz is not null)
                    {
                        var mode = TransformType.Relative;

                        if (tx is not null)
                            mode = ConvertToTransformType(tx.Mode);
                        if (tx is not null)
                            mode = ConvertToTransformType(tx.Mode);
                        if (tx is not null)
                            mode = ConvertToTransformType(tx.Mode);

                        track.TranslationCurve = new(TransformSpace.Local, mode);

                        for (var i = minFrame; i <= maxFrame; i += 1)
                        {
                            var txVal = Sample(txKeyFrames, 0, i, float.Lerp);
                            var tyVal = Sample(tyKeyFrames, 0, i, float.Lerp);
                            var tzVal = Sample(tzKeyFrames, 0, i, float.Lerp);

                            track.AddTranslationFrame((float)i, new(txVal, tyVal, tzVal));
                        }
                    }

                    skeletalAnimation.Tracks.Add(track);
                }

                foreach (var curveModeOverrideNode in animation.EnumerateCurveModeOverrides())
                {
                    Console.WriteLine(curveModeOverrideNode.NodeName);
                }

                foreach (var noteTrack in animation.EnumerateNotificationTracks())
                {
                    var action = skeletalAnimation.CreateAction(noteTrack.Name);

                    foreach (var frame in noteTrack.EnumerateKeyFrames())
                    {
                        action.KeyFrames.Add(new((float)frame, null));
                    }
                }

                scene.Objects.Add(skeletalAnimation);
            }
        }

        /// <inheritdoc/>
        public override void Write(Stream stream, string filePath, Graphics3DScene scene)
        {
            var root = new CastNode(CastNodeIdentifier.Root);

            var metaDataNode = root.AddNode<MetadataNode>();

            metaDataNode.UpAxis = "z";
            metaDataNode.Software = Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty;

            foreach (var sceneObject in scene.Objects)
            {
                if (sceneObject is Model model)
                {
                    var modelNode = root.AddNode<ModelNode>();

                    if (model.Skeleton is not null)
                    {
                        var skeleton = modelNode.AddNode<SkeletonNode>();
                        var bones = new List<BoneNode>(model.Skeleton.Bones.Count);

                        foreach (var bone in model.Skeleton.Bones)
                        {
                            var boneNode = skeleton.AddNode<BoneNode>();

                            boneNode.Name = bone.Name;

                            if (bone.BaseTransform.LocalPosition.HasValue)
                                boneNode.LocalPosition = bone.BaseTransform.LocalPosition.Value;
                            if (bone.BaseTransform.LocalRotation.HasValue)
                                boneNode.LocalRotation = bone.BaseTransform.LocalRotation.Value;
                            if (bone.BaseTransform.WorldPosition.HasValue)
                                boneNode.WorldPosition = bone.BaseTransform.WorldPosition.Value;
                            if (bone.BaseTransform.WorldRotation.HasValue)
                                boneNode.WorldRotation = bone.BaseTransform.WorldRotation.Value;

                            bones.Add(boneNode);
                        }

                        // Final Pass to build incides, this is because we can technically
                        // have our bone table out of order (parents are below children)
                        for (var i = 0; i < model.Skeleton.Bones.Count; i++)
                        {
                            bones[i].ParentIndex = bones.FindIndex(x => x.Name.Equals(model.Skeleton.Bones[i].Parent?.Name));
                        }
                    }

                    foreach (var mesh in model.Meshes)
                    {
                        var meshNode = modelNode.AddNode<MeshNode>();

                        meshNode.AddArray("vp", mesh.Positions);

                        if (mesh.Normals is not null)
                        {
                            meshNode.AddArray("vn", mesh.Normals);
                        }
                        if (mesh.Tangents is not null)
                        {
                            meshNode.AddArray("vt", mesh.Tangents);
                        }
                        
                        if (mesh.UVLayers is not null)
                        {
                            meshNode.UVLayerCount = mesh.UVLayers.Dimension;

                            for (var i = 0; i < mesh.UVLayers.Dimension; i++)
                            {
                                var uvLayer = meshNode.AddUVLayer(i);

                                for (var j = 0; j < mesh.UVLayers.ElementCount; j++)
                                {
                                    uvLayer.Add(mesh.UVLayers[j, i]);
                                }
                            }
                        }

                        if (mesh.Influences is not null)
                        {
                            meshNode.MaximumWeightInfluence = mesh.Influences.Dimension;

                            var boneIndices = meshNode.AddArray<uint>("wb", mesh.Influences.Count);
                            var boneWeights = meshNode.AddArray<float>("wv", mesh.Influences.Count);

                            foreach (var (index, weight) in mesh.Influences)
                            {
                                boneIndices.Add((uint)index);
                                boneWeights.Add(weight);
                            }
                        }

                        var faces = meshNode.AddArray<uint>("f");

                        foreach (var (f0, f1, f2) in mesh.Faces)
                        {
                            faces.Add((uint)f0);
                            faces.Add((uint)f1);
                            faces.Add((uint)f2);
                        }

                        meshNode.MaterialHash = CastHasher.Compute(mesh.Materials[0].Name);
                    }

                    foreach (var material in model.Materials)
                    {
                        var materialNode = modelNode.AddNode<MaterialNode>();

                        materialNode.Name = material.Name;
                        materialNode.Hash = CastHasher.Compute(material.Name);
                        materialNode.Type = "pbr";
                    }
                }
                else if (sceneObject is SkeletonAnimation animation)
                {
                    var animationNode = root.AddNode<AnimationNode>();

                    animationNode.AddValue("fr", animation.Framerate);
                    animationNode.AddValue("lo", (byte)0);

                    if (animation.Tracks is not null)
                    {
                        foreach (var track in animation.Tracks)
                        {
                            if (track.RotationCurve is not null && track.RotationCurve.KeyFrames.Count > 0)
                            {
                                var rCurve = animationNode.AddNode<CurveNode>();

                                var mode = animation.TransformType;

                                // If parent we'll add override nodes.
                                if (track.RotationCurve.TransformType != TransformType.Unknown && track.RotationCurve.TransformType != TransformType.Parent)
                                    mode = track.RotationCurve.TransformType;

                                rCurve.Mode = mode switch
                                {
                                    TransformType.Absolute => "absolute",
                                    TransformType.Additive => "additive",
                                    _ => "relative",
                                };
                                rCurve.NodeName = track.Name;
                                rCurve.KeyPropertyName = "rq";

                                var rKeyValueBuffer = rCurve.AddArray<Vector4>("kv");
                                var rKeyFrameBuffer = rCurve.AddArray<byte>("kb");

                                foreach (var frame in track.RotationCurve.KeyFrames)
                                {
                                    rKeyValueBuffer.Add(CastHelpers.CreateVector4FromQuaternion(frame.Value));
                                    rKeyFrameBuffer.Add((byte)frame.Frame);
                                }
                            }

                            if (track.TranslationCurve is not null && track.TranslationCurve.KeyFrames.Count > 0)
                            {
                                var xCurve = animationNode.AddNode<CurveNode>();
                                var yCurve = animationNode.AddNode<CurveNode>();
                                var zCurve = animationNode.AddNode<CurveNode>();

                                var mode = animation.TransformType;

                                //// If parent we'll add override nodes.
                                if (track.TranslationCurve.TransformType != TransformType.Unknown && track.TranslationCurve.TransformType != TransformType.Parent)
                                    mode = track.TranslationCurve.TransformType;

                                switch (mode)
                                {
                                    case TransformType.Absolute:
                                        xCurve.Mode = "absolute";
                                        yCurve.Mode = "absolute";
                                        zCurve.Mode = "absolute";
                                        break;
                                    case TransformType.Additive:
                                        xCurve.Mode = "additive";
                                        yCurve.Mode = "additive";
                                        zCurve.Mode = "additive";
                                        break;
                                    default:
                                        xCurve.Mode = "relative";
                                        yCurve.Mode = "relative";
                                        zCurve.Mode = "relative";
                                        break;
                                }

                                xCurve.NodeName = track.Name;
                                yCurve.NodeName = track.Name;
                                zCurve.NodeName = track.Name;

                                xCurve.KeyPropertyName = "tx";
                                yCurve.KeyPropertyName = "ty";
                                zCurve.KeyPropertyName = "tz";

                                var xKeyValueBuffer = xCurve.AddArray<float>("kv");
                                var yKeyValueBuffer = yCurve.AddArray<float>("kv");
                                var zKeyValueBuffer = zCurve.AddArray<float>("kv");
                                var xKeyFrameBuffer = xCurve.AddArray<uint>("kb");
                                var yKeyFrameBuffer = yCurve.AddArray<uint>("kb");
                                var zKeyFrameBuffer = zCurve.AddArray<uint>("kb");


                                foreach (var frame in track.TranslationCurve.KeyFrames)
                                {
                                    xKeyValueBuffer.Add(frame.Value.X);
                                    yKeyValueBuffer.Add(frame.Value.Y);
                                    zKeyValueBuffer.Add(frame.Value.Z);

                                    xKeyFrameBuffer.Add((uint)frame.Frame);
                                    yKeyFrameBuffer.Add((uint)frame.Frame);
                                    zKeyFrameBuffer.Add((uint)frame.Frame);
                                }
                            }
                        }

                    }

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
            }

            CastWriter.Save(stream, root);
        }

        public static TransformType ConvertToTransformType(string? transformType)
        {
            return transformType switch
            {
                "absolute" => TransformType.Absolute,
                "relative" => TransformType.Relative,
                "additive" => TransformType.Additive,
                _          => TransformType.Unknown,
            };
        }

        public static T Sample<T>((double, T)[]? data, T defaultValue, double time, Func<T, T, float, T> interpolation)
        {
            if (data is null || data.Length == 0)
                return defaultValue;
            if (data.Length == 1)
                return data[0].Item2;
            if (time <= data[0].Item1)
                return data[0].Item2;
            var last = data[^1].Item1;
            if (time >= data[^1].Item1)
                return data[^1].Item2;

            for (int i = 1; i < data.Length; i++)
            {
                if (time < data[i].Item1)
                {
                    var frame0 = data[i - 1].Item1;
                    var frame1 = data[i].Item1;
                    var weight = (time - frame0) / (frame1 - frame0);


                    return interpolation(data[i - 1].Item2, data[i].Item2, (float)weight);
                }
            }

            return data[^1].Item2;
        }
        public static string ConvertFromTransformType(TransformType type)
        {
            return type switch
            {
                TransformType.Absolute => "absolute",
                TransformType.Additive => "additive",
                _                      => "relative",
            };
        }
    }
}
