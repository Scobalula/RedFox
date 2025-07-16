using Cast.NET;
using Cast.NET.Nodes;
using RedFox.Graphics3D.Skeletal;
using System.Diagnostics;
using System.Numerics;
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

            var root = cast.RootNodes[0];

            foreach (var modelNode in root.EnumerateChildrenOfType<ModelNode>())
            {
                var model = scene.AddObject<Model>();
                var materialLookup = new Dictionary<ulong, Material>();

                if (modelNode.Skeleton is SkeletonNode skeletonNode)
                {
                    model.Skeleton = scene.AddObject<Skeleton>();

                    var boneNodes = modelNode.Skeleton.GetChildrenOfType<BoneNode>();

                    foreach (var boneNode in boneNodes)
                    {
                        model.Skeleton.AddBone(new SkeletonBone(boneNode.Name));
                    }

                    for (int i = 0; i < boneNodes.Length && i < model.Skeleton.Bones.Count; i++)
                    {
                        var boneNode = boneNodes[i];
                        var bone = model.Skeleton.Bones[i];
                        var parentIndex = boneNode.ParentIndex;

                        bone.Parent = parentIndex > -1 ? model.Skeleton.Bones[parentIndex] : null;

                        // TODO: Need to make properties of SkeletonBone Nullable<T> or have a "dirty/uncomputed" indicator 
                        // then we can know if values are missing dynamically and compute them post load
                        if (!boneNode.TryGetLocalPosition(out var localPosition))
                            throw new NotSupportedException("Cast bones without local positions are not yet supported.");
                        if (!boneNode.TryGetLocalRotation(out var localRotation))
                            throw new NotSupportedException("Cast bones without local rotations are not yet supported.");
                        if (!boneNode.TryGetWorldPosition(out var worldPosition))
                            throw new NotSupportedException("Cast bones without world positions are not yet supported.");
                        if (!boneNode.TryGetWorldRotation(out var worldRotation))
                            throw new NotSupportedException("Cast bones without world rotations are not yet supported.");

                        bone.BaseLocalTranslation = localPosition;
                        bone.BaseLocalRotation = localRotation;
                        bone.BaseWorldTranslation = worldPosition;
                        bone.BaseWorldRotation = worldRotation;
                    }
                }

                foreach (var materialNode in modelNode.Materials)
                {
                    var material = scene.AddObject<Material>();

                    material.Name = materialNode.Name;

                    materialLookup.Add(materialNode.Hash, material);
                    model.Materials.Add(material);
                }

                foreach (var meshNode in modelNode.Meshes)
                {
                    var uvLayerCount = meshNode.UVLayerCount;
                    var influences = meshNode.MaximumWeightInfluence;

                    var mesh = new Mesh();

                    var buffer = meshNode.VertexPositionBuffer;

                    mesh.Positions = new(buffer.ValueCount);

                    for (int i = 0; i < buffer.ValueCount; i++)
                    {
                        mesh.Positions.Add(buffer.Values[i]);
                    }

                    var normalsBuffer = meshNode.VertexNormalBuffer;

                    if (normalsBuffer is not null)
                    {
                        if (normalsBuffer.ValueCount != buffer.ValueCount)
                            throw new DataMisalignedException("Vertex normal buffer does not contain same count as positions buffer");

                        mesh.Normals = new(buffer.ValueCount);

                        for (int i = 0; i < buffer.ValueCount; i++)
                        {
                            mesh.Normals.Add(normalsBuffer.Values[i]);
                        }
                    }

                    if (uvLayerCount > 0)
                    {
                        mesh.UVLayers = new(mesh.Positions.Count * uvLayerCount);

                        for (int u = 0; u < uvLayerCount; u++)
                        {
                            var uvLayer = meshNode.GetUVLayer(u) as CastArrayProperty<Vector2> ?? throw new NullReferenceException(nameof(meshNode.GetUVLayer));

                            if (uvLayer.ValueCount != buffer.ValueCount)
                                throw new DataMisalignedException($"UV Layer does not contain same count as positions buffer");

                            for (int i = 0; i < buffer.ValueCount; i++)
                            {
                                mesh.UVLayers.Add(uvLayer.Values[i], i, u);
                            }
                        }
                    }

                    if (influences > 0)
                    {
                        var boneIndexBuffer = meshNode.VertexWeightBoneBuffer ?? throw new NullReferenceException(nameof(meshNode.VertexWeightBoneBuffer));
                        var boneWeightBuffer = meshNode.VertexWeightValueBuffer ?? throw new NullReferenceException(nameof(meshNode.VertexWeightValueBuffer));

                        if (boneIndexBuffer.ValueCount != (buffer.ValueCount * influences))
                            throw new DataMisalignedException($"Bone weight buffer does not contain same count as positions buffer");
                        if (boneWeightBuffer.ValueCount != (buffer.ValueCount * influences))
                            throw new DataMisalignedException($"Bone value buffer does not contain same count as positions buffer");

                        mesh.Influences = new(buffer.ValueCount, influences);

                        foreach (var (bone, weight) in meshNode.EnumerateBoneWeights())
                        {
                            mesh.Influences.Add(new(bone, weight));
                        }
                    }

                    var faceBuffer = meshNode.FaceBuffer;
                    var faceCount = faceBuffer.ValueCount / 3;

                    // TODO: Make this a helper function within Cast.NET like frames
                    if (faceBuffer is CastArrayProperty<byte> byteArray)
                    {
                        for (var f = 0; f < faceCount; f++)
                        {
                            var i0 = byteArray.Values[f * 3 + 0];
                            var i1 = byteArray.Values[f * 3 + 1];
                            var i2 = byteArray.Values[f * 3 + 2];

                            mesh.Faces.Add(((int)i0, (int)i1, (int)i2));
                        }
                    }
                    else if (faceBuffer is CastArrayProperty<ushort> ushortArray)
                    {
                        for (var f = 0; f < faceCount; f++)
                        {
                            var i0 = ushortArray.Values[f * 3 + 0];
                            var i1 = ushortArray.Values[f * 3 + 1];
                            var i2 = ushortArray.Values[f * 3 + 2];

                            mesh.Faces.Add(((int)i0, (int)i1, (int)i2));
                        }
                    }
                    else if (faceBuffer is CastArrayProperty<uint> intArray)
                    {
                        for (var f = 0; f < faceCount; f++)
                        {
                            var i0 = intArray.Values[f * 3 + 0];
                            var i1 = intArray.Values[f * 3 + 1];
                            var i2 = intArray.Values[f * 3 + 2];

                            mesh.Faces.Add(((int)i0, (int)i1, (int)i2));
                        }
                    }
                    else
                    {
                        throw new NotImplementedException($"Unimplemented face buffer type: {faceBuffer}");
                    }

                    mesh.Materials.Add(materialLookup[meshNode.MaterialHash]);

                    model.Meshes.Add(mesh);
                }
            }



            foreach (var animation in root.EnumerateChildrenOfType<AnimationNode>())
            {
                var skeletalAnimation = new SkeletonAnimation("john")
                {
                    TransformType = TransformType.Relative
                };
                var uniqueCurveNames = new HashSet<string>();
                var curveNodes = animation.Curves;

                foreach (var curveNode in curveNodes)
                {
                    uniqueCurveNames.Add(curveNode.NodeName);
                }

                skeletalAnimation.Tracks = [];

                foreach (var curveName in uniqueCurveNames)
                {
                    var tx = curveNodes.FirstOrDefault(x => x.NodeName == curveName && x.KeyPropertyName == "tx");
                    var ty = curveNodes.FirstOrDefault(x => x.NodeName == curveName && x.KeyPropertyName == "ty");
                    var tz = curveNodes.FirstOrDefault(x => x.NodeName == curveName && x.KeyPropertyName == "tz");
                    var rq = curveNodes.FirstOrDefault(x => x.NodeName == curveName && x.KeyPropertyName == "rq");

                    var track = new SkeletonAnimationTrack(curveName);

                    // Rotation
                    if (rq is not null)
                    {
                        track.RotationCurve = new(TransformSpace.Local, ConvertToTransformType(rq.Mode));

                        foreach (var (key, value) in rq.EnumerateKeys<Vector4>())
                        {
                            track.RotationCurve.KeyFrames.Add(new(key, new(value.X, value.Y, value.Z, value.W)));
                        }
                    }
                    // Translation X
                    if (tx is not null)
                    {
                        track.TranslationXCurve = new(TransformSpace.Local, ConvertToTransformType(tx.Mode));

                        foreach (var (key, value) in tx.EnumerateKeys<float>())
                        {
                            track.TranslationXCurve.KeyFrames.Add(new(key, value));
                        }
                    }
                    // Translation Y
                    if (ty is not null)
                    {
                        track.TranslationYCurve = new(TransformSpace.Local, ConvertToTransformType(ty.Mode));

                        foreach (var (key, value) in ty.EnumerateKeys<float>())
                        {
                            track.TranslationYCurve.KeyFrames.Add(new(key, value));
                        }
                    }
                    // Translation Y
                    if (tz is not null)
                    {
                        track.TranslationZCurve = new(TransformSpace.Local, ConvertToTransformType(tz.Mode));

                        foreach (var (key, value) in tz.EnumerateKeys<float>())
                        {
                            track.TranslationZCurve.KeyFrames.Add(new(key, value));
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
                        action.KeyFrames.Add(new(frame, null));
                    }
                }

                scene.Objects.Add(skeletalAnimation);
            }
        }

        /// <inheritdoc/>
        public override void Write(Stream stream, string filePath, Graphics3DScene scene)
        {
            var root = new CastNode(CastNodeIdentifier.Root);

            foreach (var sceneObject in scene.Objects)
            {
                if (sceneObject is SkeletonAnimation animation)
                {
                    var animationNode = root.AddNode<AnimationNode>();

                    animationNode.AddValue("fr", animation.Framerate);
                    animationNode.AddValue("lo", (byte)0);

                    if (animation.Tracks is not null)
                    {
                        foreach (var track in animation.Tracks)
                        {
                            if (track.RotationCurve is not null)
                            {
                                var curveNode = animationNode.AddNode<CurveNode>();

                                curveNode.Mode = ConvertFromTransformType(track.RotationCurve.Type);
                                curveNode.NodeName = track.Name;
                                curveNode.KeyPropertyName = "rq";

                                var keyValueBuffer = curveNode.AddArray<Vector4>("kv");
                                var keyFrameBuffer = curveNode.AddArray<uint>("kb");

                                foreach (var frame in track.RotationCurve.KeyFrames)
                                {
                                    keyValueBuffer.Add(new(frame.Value.X, frame.Value.Y, frame.Value.Z, frame.Value.W));
                                    keyFrameBuffer.Add((uint)frame.Frame);
                                }

                                curveNode.KeyValueBuffer = keyValueBuffer;
                                curveNode.KeyFrameBuffer = keyFrameBuffer;
                            }

                            if (track.TranslationXCurve is not null)
                            {
                                var curveNode = animationNode.AddNode<CurveNode>();

                                curveNode.Mode = ConvertFromTransformType(track.TranslationXCurve.Type);
                                curveNode.NodeName = track.Name;
                                curveNode.KeyPropertyName = "tx";

                                var keyValueBuffer = curveNode.AddArray<float>("kv");
                                var keyFrameBuffer = curveNode.AddArray<uint>("kb");

                                foreach (var frame in track.TranslationXCurve.KeyFrames)
                                {
                                    keyValueBuffer.Add(frame.Value);
                                    keyFrameBuffer.Add((uint)frame.Frame);
                                }

                                curveNode.KeyValueBuffer = keyValueBuffer;
                                curveNode.KeyFrameBuffer = keyFrameBuffer;
                            }

                            if (track.TranslationYCurve is not null)
                            {
                                var curveNode = animationNode.AddNode<CurveNode>();

                                curveNode.Mode = ConvertFromTransformType(track.TranslationYCurve.Type);
                                curveNode.NodeName = track.Name;
                                curveNode.KeyPropertyName = "ty";

                                var keyValueBuffer = curveNode.AddArray<float>("kv");
                                var keyFrameBuffer = curveNode.AddArray<uint>("kb");

                                foreach (var frame in track.TranslationYCurve.KeyFrames)
                                {
                                    keyValueBuffer.Add(frame.Value);
                                    keyFrameBuffer.Add((uint)frame.Frame);
                                }

                                curveNode.KeyValueBuffer = keyValueBuffer;
                                curveNode.KeyFrameBuffer = keyFrameBuffer;
                            }

                            if (track.TranslationZCurve is not null)
                            {
                                var curveNode = animationNode.AddNode<CurveNode>();

                                curveNode.Mode = ConvertFromTransformType(track.TranslationZCurve.Type);
                                curveNode.NodeName = track.Name;
                                curveNode.KeyPropertyName = "tz";

                                var keyValueBuffer = curveNode.AddArray<float>("kv");
                                var keyFrameBuffer = curveNode.AddArray<uint>("kb");

                                foreach (var frame in track.TranslationZCurve.KeyFrames)
                                {
                                    keyValueBuffer.Add(frame.Value);
                                    keyFrameBuffer.Add((uint)frame.Frame);
                                }

                                curveNode.KeyValueBuffer = keyValueBuffer;
                                curveNode.KeyFrameBuffer = keyFrameBuffer;
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
