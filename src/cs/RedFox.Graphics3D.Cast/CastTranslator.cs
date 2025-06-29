using Cast.NET;
using Cast.NET.Nodes;
using RedFox.Graphics3D.Skeletal;
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

                foreach (var curveName in uniqueCurveNames)
                {
                    var tx = curveNodes.FirstOrDefault(x => x.NodeName == curveName && x.KeyPropertyName == "tx");
                    var ty = curveNodes.FirstOrDefault(x => x.NodeName == curveName && x.KeyPropertyName == "ty");
                    var tz = curveNodes.FirstOrDefault(x => x.NodeName == curveName && x.KeyPropertyName == "tz");
                    var rq = curveNodes.FirstOrDefault(x => x.NodeName == curveName && x.KeyPropertyName == "rq");

                    var mode = "relative";

                    if (!string.IsNullOrWhiteSpace(tx?.Mode))
                        mode = tx.Mode;

                    var target = new SkeletonAnimationTarget(curveName);

                    if (rq != null)
                    {
                        var rqFrames = rq.EnumerateKeyFrames().ToArray();
                        var rqValues = rq.EnumerateKeyValues<Vector4>().ToArray();

                        if (rqFrames.Length != rqValues.Length)
                            throw new DataMisalignedException("Rotation frame buffer and value buffer are different lengths.");

                        target.TransformType = mode switch
                        {
                            "absolute" => TransformType.Absolute,
                            "additive" => TransformType.Additive,
                            _ => TransformType.Relative,
                        };

                        for (int i = 0; i < rqFrames.Length; i++)
                        {
                            target.AddRotationFrame(rqFrames[i], CastHelpers.CreateQuaternionFromVector4(rqValues[i]));
                        }
                    }

                    if (tx != null || ty != null || tz != null)
                    {
                        // For now we require all translations until I refactor animation classes to be more like cast/maya/blender/etc
                        if (tx == null)
                            throw new NotSupportedException("Cast files with dropped translation curves not currently supported.");
                        if (ty == null)
                            throw new NotSupportedException("Cast files with dropped translation curves not currently supported.");
                        if (tz == null)
                            throw new NotSupportedException("Cast files with dropped translation curves not currently supported.");

                        var txFrames = tx.EnumerateKeyFrames().ToArray();
                        var txValues = tx.EnumerateKeyValues<float>().ToArray();
                        var tyValues = ty.EnumerateKeyValues<float>().ToArray();
                        var tzValues = tz.EnumerateKeyValues<float>().ToArray();

                        if (txFrames.Length != txValues.Length)
                            throw new DataMisalignedException("Translation frame buffer and value buffer are different lengths.");
                        if (txFrames.Length != tyValues.Length)
                            throw new NotSupportedException("Cast files with different length translation curves not currently supported.");
                        if (txFrames.Length != tzValues.Length)
                            throw new NotSupportedException("Cast files with different length translation curves not currently supported.");

                        for (int i = 0; i < txFrames.Length; i++)
                        {
                            target.AddTranslationFrame(txFrames[i], new(txValues[i], tyValues[i], tzValues[i]));
                        }
                    }

                    skeletalAnimation.Targets.Add(target);
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

                    foreach (var target in animation.Targets)
                    {
                        if (target.TranslationFrames is not null && target.TranslationFrames.Count > 0)
                        {
                            var xCurve = animationNode.AddNode<CurveNode>();
                            var yCurve = animationNode.AddNode<CurveNode>();
                            var zCurve = animationNode.AddNode<CurveNode>();

                            var mode = animation.TransformType;

                            // If parent we'll add override nodes.
                            if (target.TransformType != TransformType.Unknown && target.TransformType != TransformType.Parent)
                                mode = target.TransformType;

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

                            xCurve.NodeName = target.BoneName;
                            yCurve.NodeName = target.BoneName;
                            zCurve.NodeName = target.BoneName;

                            xCurve.KeyPropertyName = "tx";
                            yCurve.KeyPropertyName = "ty";
                            zCurve.KeyPropertyName = "tz";

                            var xKeyValueBuffer = xCurve.AddArray<float>("kv");
                            var yKeyValueBuffer = yCurve.AddArray<float>("kv");
                            var zKeyValueBuffer = zCurve.AddArray<float>("kv");
                            var xKeyFrameBuffer = xCurve.AddArray<uint>("kb");
                            var yKeyFrameBuffer = yCurve.AddArray<uint>("kb");
                            var zKeyFrameBuffer = zCurve.AddArray<uint>("kb");


                            foreach (var frame in target.TranslationFrames)
                            {
                                xKeyValueBuffer.Add(frame.Value.X);
                                yKeyValueBuffer.Add(frame.Value.Y);
                                zKeyValueBuffer.Add(frame.Value.Z);

                                xKeyFrameBuffer.Add((uint)frame.Frame);
                                yKeyFrameBuffer.Add((uint)frame.Frame);
                                zKeyFrameBuffer.Add((uint)frame.Frame);
                            }
                        }

                        if (target.RotationFrames is not null && target.RotationFrames.Count > 0)
                        {
                            var rCurve = animationNode.AddNode<CurveNode>();

                            var mode = animation.TransformType;

                            // If parent we'll add override nodes.
                            if (target.TransformType != TransformType.Unknown && target.TransformType != TransformType.Parent)
                                mode = target.TransformType;

                            rCurve.Mode = mode switch
                            {
                                TransformType.Absolute => "absolute",
                                TransformType.Additive => "additive",
                                _ => "relative",
                            };
                            rCurve.NodeName = target.BoneName;
                            rCurve.KeyPropertyName = "rq";

                            var rKeyValueBuffer = rCurve.AddArray<Vector4>("kv");
                            var rKeyFrameBuffer = rCurve.AddArray<uint>("kb");

                            foreach (var frame in target.RotationFrames)
                            {
                                rKeyValueBuffer.Add(CastHelpers.CreateVector4FromQuaternion(frame.Value));
                                rKeyFrameBuffer.Add((uint)frame.Frame);
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
    }
}
