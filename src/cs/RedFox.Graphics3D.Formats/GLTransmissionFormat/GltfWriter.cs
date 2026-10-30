using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.Formats.GLTransmissionFormat;

/// <summary>
/// Writes scene data as glTF JSON or a GLB container.
/// </summary>
public sealed class GltfWriter
{
    private readonly string? _targetDirectoryPath;

    private readonly GltfDocument	_doc		= new();
    private readonly MemoryStream	_binBuffer	= new();

    private readonly Dictionary<Material, int>					_materialIndices	= [];
    private readonly Dictionary<SkeletonBone, int>				_boneNodeIndices	= [];
    private readonly Dictionary<SkeletonBone, SkeletonBone?>	_exportedParents	= [];

    /// <summary>
    /// Initializes a new <see cref="GltfWriter"/> with the specified translation options.
    /// </summary>
    /// <param name="options">Options that control how the scene data is written.</param>
    public GltfWriter(SceneTranslatorOptions options) : this(options, null)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="GltfWriter"/> with translation options and a target directory.
    /// </summary>
    /// <param name="options">Options that control how the scene data is written.</param>
    /// <param name="targetDirectoryPath">The directory used to make external texture paths relative, if available.</param>
    public GltfWriter(SceneTranslatorOptions options, string? targetDirectoryPath)
    {
        ArgumentNullException.ThrowIfNull(options);
        _targetDirectoryPath = targetDirectoryPath;
    }

    /// <summary>
    /// Writes the specified scene to the output stream in GLB binary format.
    /// </summary>
    /// <param name="scene">The scene to export.</param>
    /// <param name="stream">The output stream for the GLB data.</param>
    /// <param name="name">The scene name.</param>
    public void Write(Scene scene, Stream stream, string name) => Write(scene, stream, name, CancellationToken.None);

    /// <summary>
    /// Writes data with cancellation support.
    /// </summary>
    /// <param name="scene">The scene to write.</param>
    /// <param name="stream">The output stream.</param>
    /// <param name="name">The scene name.</param>
    /// <param name="token">The cancellation token.</param>
    public void Write(Scene scene, Stream stream, string name, CancellationToken token) => Write(new SceneTranslationSelection(scene, SceneNodeFlags.None), stream, name, token);

    /// <summary>
    /// Writes the specified scene selection to the output stream in GLB binary format.
    /// </summary>
    /// <param name="selection">The filtered scene selection to export.</param>
    /// <param name="stream">The output stream for the GLB data.</param>
    /// <param name="name">The scene name.</param>
    public void Write(SceneTranslationSelection selection, Stream stream, string name) => Write(selection, stream, name, CancellationToken.None);

    /// <summary>
    /// Writes data with cancellation support.
    /// </summary>
    /// <param name="selection">The selected scene data to write.</param>
    /// <param name="stream">The output stream.</param>
    /// <param name="name">The scene name.</param>
    /// <param name="token">The cancellation token.</param>
    public void Write(SceneTranslationSelection selection, Stream stream, string name, CancellationToken token) => Write(selection, stream, name, false, token);

    /// <summary>
    /// Writes a RedFox <see cref="Material"/> to the glTF document.
    /// </summary>
    /// <param name="mat">The material to write.</param>
    public void WriteMaterial(Material mat)
    {
        if (_materialIndices.ContainsKey(mat))
            return;

        GltfMaterial gltfMat = new()
        {
            Name = mat.Name,
            DoubleSided = mat.DoubleSided
        };

        if (mat.DiffuseColor.HasValue)
        {
            Vector4 dc = mat.DiffuseColor.Value;
            gltfMat.BaseColorFactor = [dc.X, dc.Y, dc.Z, dc.W];
        }

        if (mat.MetallicColor.HasValue)
            gltfMat.MetallicFactor = mat.MetallicColor.Value.X;

        if (mat.RoughnessColor.HasValue)
            gltfMat.RoughnessFactor = mat.RoughnessColor.Value.X;

        if (mat.EmissiveColor.HasValue)
        {
            Vector4 ec = mat.EmissiveColor.Value;
            gltfMat.EmissiveFactor = [ec.X, ec.Y, ec.Z];
        }

        if (TryResolveTexturePath(mat, mat.DiffuseMapName, out string? diffuseTexturePath) && diffuseTexturePath is not null)
            gltfMat.BaseColorTextureIndex = WriteImageTexture(diffuseTexturePath);

        if (TryResolveTexturePath(mat, mat.NormalMapName, out string? normalTexturePath) && normalTexturePath is not null)
            gltfMat.NormalTextureIndex = WriteImageTexture(normalTexturePath);

        if (TryResolveTexturePath(mat, mat.MetallicMapName, out string? metallicTexturePath) && metallicTexturePath is not null)
            gltfMat.MetallicRoughnessTextureIndex = WriteImageTexture(metallicTexturePath);

        if (TryResolveTexturePath(mat, mat.AmbientOcclusionMapName, out string? ambientOcclusionTexturePath) && ambientOcclusionTexturePath is not null)
            gltfMat.OcclusionTextureIndex = WriteImageTexture(ambientOcclusionTexturePath);

        if (TryResolveTexturePath(mat, mat.EmissiveMapName, out string? emissiveTexturePath) && emissiveTexturePath is not null)
            gltfMat.EmissiveTextureIndex = WriteImageTexture(emissiveTexturePath);

        _materialIndices[mat] = _doc.Materials.Count;
        _doc.Materials.Add(gltfMat);
    }

    /// <summary>
    /// Creates image and texture entries for the specified image path, returning the texture index.
    /// </summary>
    /// <param name="imagePath">The file path or URI of the image to reference.</param>
    /// <returns>The index of the newly created glTF texture entry.</returns>
    public int WriteImageTexture(string imagePath)
    {
        int imageIdx = _doc.Images.Count;
        _doc.Images.Add(new GltfImage
        {
            Uri = imagePath,
            Name = Path.GetFileNameWithoutExtension(imagePath),
        });

        int texIdx = _doc.Textures.Count;
        _doc.Textures.Add(new GltfTexture
        {
            Source = imageIdx,
        });

        return texIdx;
    }

    /// <summary>
    /// Writes a <see cref="Mesh"/> to the glTF document, returning the node index.
    /// </summary>
    /// <param name="mesh">The mesh to write.</param>
    /// <param name="skinIndices">A mapping from skeleton names to their glTF skin indices.</param>
    /// <returns>The index of the glTF node created for this mesh.</returns>
    public int WriteMesh(Mesh mesh, Dictionary<string, int> skinIndices)
    {
        GltfMeshPrimitive prim = new();

        if (mesh.Positions is not null)
        {
            float[] positions = ReadBufferAsFloats(mesh.Positions, 0, 3);
            ComputeMinMax(positions, 3, out float[] min, out float[] max);
            prim.Attributes["POSITION"] = WriteAccessor(positions, GltfConstants.TypeVec3, GltfConstants.ComponentTypeFloat, min, max);
        }

        if (mesh.Normals is not null)
            prim.Attributes["NORMAL"] = WriteAccessor(ReadBufferAsFloats(mesh.Normals, 0, 3), GltfConstants.TypeVec3, GltfConstants.ComponentTypeFloat);

        if (mesh.Tangents is not null)
        {
            float[] tangents = new float[mesh.VertexCount * 4];
            for (int v = 0; v < mesh.VertexCount; v++)
            {
                Vector4 tangent = mesh.GetVertexTangent(v, true);
                tangents[v * 4]		= tangent.X;
                tangents[v * 4 + 1]	= tangent.Y;
                tangents[v * 4 + 2]	= tangent.Z;
                float handedness = tangent.W;
                if (mesh.Tangents.ComponentCount < 4 && mesh.Normals is not null && mesh.BiTangents is not null)
                    handedness = Vector3.Dot(Vector3.Cross(mesh.Normals.GetVector3(v, 0), new Vector3(tangent.X, tangent.Y, tangent.Z)), mesh.BiTangents.GetVector3(v, 0));
                tangents[v * 4 + 3] = handedness < 0 ? -1f : 1f;
            }
            prim.Attributes["TANGENT"] = WriteAccessor(tangents, GltfConstants.TypeVec4, GltfConstants.ComponentTypeFloat);
        }

        if (mesh.UVLayers is not null)
        {
            int uvLayers	= mesh.UVLayers.ValueCount;
            int vertexCount	= mesh.UVLayers.ElementCount;

            for (int layer = 0; layer < uvLayers; layer++)
            {
                float[] uvData = new float[vertexCount * 2];
                for (int v = 0; v < vertexCount; v++)
                {
                    uvData[v * 2] = mesh.UVLayers.Get<float>(v, layer, 0);
                    uvData[v * 2 + 1] = mesh.UVLayers.Get<float>(v, layer, 1);
                }
                prim.Attributes[$"TEXCOORD_{layer}"] = WriteAccessor(uvData, GltfConstants.TypeVec2, GltfConstants.ComponentTypeFloat);
            }
        }

        if (mesh.ColorLayers is not null)
        {
            int colorLayers	= mesh.ColorLayers.ValueCount;
            int vertexCount	= mesh.ColorLayers.ElementCount;
            int compCount	= mesh.ColorLayers.ComponentCount;

            for (int layer = 0; layer < colorLayers; layer++)
            {
                float[] colorData = new float[vertexCount * compCount];
                for (int v = 0; v < vertexCount; v++)
                {
                    for (int c = 0; c < compCount; c++)
                        colorData[v * compCount + c] = mesh.ColorLayers.Get<float>(v, layer, c);
                }
                prim.Attributes[$"COLOR_{layer}"] = WriteAccessor(colorData, compCount == 3 ? GltfConstants.TypeVec3 : GltfConstants.TypeVec4, GltfConstants.ComponentTypeFloat);
            }
        }

        if (mesh.FaceIndices is not null && mesh.MaterialIndexRanges is not { Count: > 0 })
            prim.Indices = WriteMeshIndices(mesh, 0, mesh.IndexCount);

        WriteSkinInfluences(mesh, prim);

        WriteMorphTargets(mesh, prim);

        if (mesh.Materials is { Count: > 0 })
        {
            Material material = mesh.Materials[0];
            if (!_materialIndices.TryGetValue(material, out int matIdx))
            {
                throw new InvalidDataException($"Cannot write glTF: mesh '{mesh.Name}' references material '{material.Name}' that is not included in the export selection.");
            }

            prim.Material = matIdx;
        }

        int gltfMeshIdx		= _doc.Meshes.Count;
        GltfMesh gltfMesh	= new()
        {
            Name = mesh.Name,
            Weights = mesh.Morph?.Weights.ToArray(),
            TargetNames = mesh.Morph?.TargetNames.ToArray(),
        };
        if (mesh.MaterialIndexRanges is { Count: > 0 } ranges)
        {
            if (mesh.Materials is null || ranges.Count != mesh.Materials.Count)
                throw new InvalidDataException($"Mesh '{mesh.Name}' has inconsistent material ranges.");
            int[] rangeOrder = Enumerable.Range(0, ranges.Count).ToArray();
            Array.Sort(rangeOrder, (a, b) => ranges[a].StartIndex.CompareTo(ranges[b].StartIndex));
            int cursor = 0;
            foreach (int i in rangeOrder)
            {
                (int start, int count) = ranges[i];
                if (start < cursor)
                    throw new InvalidDataException($"Mesh '{mesh.Name}' has overlapping material ranges.");
                if (start > cursor)
                    AddPrimitive(gltfMesh, mesh, prim, cursor, start - cursor, -1);
                if (!_materialIndices.TryGetValue(mesh.Materials[i], out int materialIndex))
                    throw new InvalidDataException($"Mesh '{mesh.Name}' references a filtered material.");
                AddPrimitive(gltfMesh, mesh, prim, start, count, materialIndex);
                cursor = checked(start + count);
            }
            if (cursor < mesh.IndexCount)
                AddPrimitive(gltfMesh, mesh, prim, cursor, mesh.IndexCount - cursor, -1);
        }
        else
        {
            gltfMesh.Primitives.Add(prim);
        }
        _doc.Meshes.Add(gltfMesh);

        int nodeIdx		= _doc.Nodes.Count;
        GltfNode node	= new()
        {
            Name = mesh.Name,
            Mesh = gltfMeshIdx
        };

        WriteNodeTransform(CreateWorldBindTransform(mesh), node);

        if (mesh.Skin is { Bones.Count: > 0 } skin)
        {
            Matrix4x4 meshBindWorld	= mesh.GetBindWorldMatrix();
            float[] matrices		= new float[checked(skin.Bones.Count * 16)];
            int[] joints			= new int[skin.Bones.Count];
            for (int i = 0; i < joints.Length; i++)
            {
                if (!_boneNodeIndices.TryGetValue(skin.Bones[i], out joints[i]))
                    throw new InvalidDataException($"Mesh '{mesh.Name}' references a filtered joint.");
                WriteMatrix4x4(skin.GetInverseBindMatrix(i, meshBindWorld), matrices, i * 16);
            }
            node.Skin = _doc.Skins.Count;
            _doc.Skins.Add(new GltfSkin
            {
                Name				= skin.Name,
                Joints				= joints,
                InverseBindMatrices	= WriteAccessor(matrices, GltfConstants.TypeMat4, GltfConstants.ComponentTypeFloat),
            });
        }

        _doc.Nodes.Add(node);
        return nodeIdx;
    }

    /// <summary>
    /// Writes bone indices and weights for skinned meshes.
    /// </summary>
    /// <param name="mesh">The mesh containing bone index and weight buffers.</param>
    /// <param name="prim">The glTF mesh primitive to add the skin influence attributes to.</param>
    public void WriteSkinInfluences(Mesh mesh, GltfMeshPrimitive prim)
    {
        if (mesh.Skin is not { } skin)
            return;

        if (skin.Bones.Count == 0)
            throw new InvalidDataException($"Cannot write glTF: mesh '{mesh.Name}' contains skin weights but has no skinned bone table.");

        if (skin.Bones.Count > ushort.MaxValue + 1)
            throw new InvalidDataException("glTF joint indices exceed 16 bits.");

        int vertexCount		= skin.BoneIndices.ElementCount;
        int influenceCount	= skin.BoneIndices.ValueCount;
        int numSets			= (influenceCount + 3) / 4;

        for (int set = 0; set < numSets; set++)
        {
            int startInfluence	= set * 4;
            int setSize			= Math.Min(4, influenceCount - startInfluence);

            ushort[] joints = new ushort[vertexCount * 4];
            for (int v = 0; v < vertexCount; v++)
            {
                for (int c = 0; c < setSize; c++)
                {
                    int localBoneIndex = skin.BoneIndices.Get<int>(v, startInfluence + c, 0);
                    if ((uint)localBoneIndex >= (uint)skin.Bones.Count)
                    {
                        throw new InvalidDataException($"Cannot write glTF: mesh '{mesh.Name}' contains skin index {localBoneIndex} outside the exported skin table.");
                    }

                    joints[v * 4 + c] = checked((ushort)localBoneIndex);
                }
            }

            prim.Attributes[$"JOINTS_{set}"] = WriteAccessorShort(joints, GltfConstants.TypeVec4);

            float[] weights = new float[vertexCount * 4];
            for (int v = 0; v < vertexCount; v++)
            {
                for (int c = 0; c < setSize; c++)
                    weights[v * 4 + c] = skin.BoneWeights.Get<float>(v, startInfluence + c, 0);
            }

            prim.Attributes[$"WEIGHTS_{set}"] = WriteAccessor(weights, GltfConstants.TypeVec4, GltfConstants.ComponentTypeFloat);
        }
    }

    /// <summary>
    /// Writes morph targets from the mesh's <see cref="Morph"/>.
    /// </summary>
    /// <param name="mesh">The mesh containing the morph.</param>
    /// <param name="prim">The glTF mesh primitive to add the morph target data to.</param>
    public void WriteMorphTargets(Mesh mesh, GltfMeshPrimitive prim)
    {
        if (mesh.Morph is not { TargetCount: > 0 } morph)
            return;

        prim.Targets = [];

        for (int t = 0; t < morph.TargetCount; t++)
        {
            Dictionary<string, int> target = [];

            WriteMorphDeltas(target, "POSITION", morph.DeltaPositions, t);
            WriteMorphDeltas(target, "NORMAL", morph.DeltaNormals, t);
            WriteMorphDeltas(target, "TANGENT", morph.DeltaTangents, t);

            prim.Targets.Add(target);
        }
    }

    /// <summary>
    /// Writes the deltas of a single morph target attribute as an accessor.
    /// </summary>
    /// <param name="target">The glTF morph target to add the accessor to.</param>
    /// <param name="attribute">The attribute name, such as POSITION.</param>
    /// <param name="deltas">The delta buffer, or <see langword="null"/> to skip the attribute.</param>
    /// <param name="targetIndex">The morph target index.</param>
    public void WriteMorphDeltas(Dictionary<string, int> target, string attribute, DataBuffer? deltas, int targetIndex)
    {
        if (deltas is null)
            return;

        float[] data = new float[deltas.ElementCount * 3];

        for (int v = 0; v < deltas.ElementCount; v++)
        {
            data[v * 3] = deltas.Get<float>(v, targetIndex, 0);
            data[v * 3 + 1] = deltas.Get<float>(v, targetIndex, 1);
            data[v * 3 + 2] = deltas.Get<float>(v, targetIndex, 2);
        }

        target[attribute] = WriteAccessor(data, GltfConstants.TypeVec3, GltfConstants.ComponentTypeFloat);
    }

    /// <summary>
    /// Writes a skeleton to the glTF document, creating nodes for each exported bone and a skin definition.
    /// </summary>
    /// <param name="skeletonName">The skeleton name to write.</param>
    /// <param name="bones">The exported bones belonging to the skeleton.</param>
    /// <returns>The index of the glTF skin, or -1 if the skeleton has no bones.</returns>
    public int WriteSkeleton(string skeletonName, SkeletonBone[] bones) => WriteSkeleton(skeletonName, bones, true);

    /// <summary>
    /// Writes a <see cref="SkeletonAnimation"/> to the glTF document.
    /// </summary>
    /// <param name="anim">The skeleton animation to write.</param>
    public void WriteAnimation(SkeletonAnimation anim)
    {
        GltfAnimation gltfAnim = new()
        {
            Name = anim.Name,
        };
        float frameRate = float.IsFinite(anim.Framerate) && anim.Framerate > 0f ? anim.Framerate : 30f;

        Dictionary<string, SkeletonAnimationTrack> tracksByName = new(StringComparer.OrdinalIgnoreCase);
        foreach (SkeletonAnimationTrack track in anim.Tracks)
            tracksByName.Add(track.Name, track);
        HashSet<string> targetNames		= new(StringComparer.OrdinalIgnoreCase);
        List<SkeletonBone> rebasedBones	= [];
        foreach ((SkeletonBone bone, int targetNodeIdx) in _boneNodeIndices)
        {
            tracksByName.TryGetValue(bone.Name, out SkeletonAnimationTrack? track);
            if (track is not null && !targetNames.Add(bone.Name))
                throw new InvalidDataException($"Animation '{anim.Name}' has an ambiguous target '{bone.Name}'.");
            SkeletonBone? parent = _exportedParents[bone];
            if (!ReferenceEquals(parent, bone.Parent))
            {
                bool animated = track is not null;
                for (SceneNode? ancestor = bone.Parent; ancestor is not null && !ReferenceEquals(ancestor, parent); ancestor = ancestor.Parent)
                    animated |= ancestor is SkeletonBone && tracksByName.ContainsKey(ancestor.Name);
                if (animated)
                    rebasedBones.Add(bone);
                continue;
            }
            if (track is null)
                continue;
            if (track.TranslationCurve is { KeyFrameCount: > 0 })
                WriteAnimationChannel(gltfAnim, targetNodeIdx, GltfConstants.PathTranslation, track.TranslationCurve, 3, frameRate);
            if (track.RotationCurve is { KeyFrameCount: > 0 })
                WriteAnimationChannel(gltfAnim, targetNodeIdx, GltfConstants.PathRotation, track.RotationCurve, 4, frameRate);
            if (track.ScaleCurve is { KeyFrameCount: > 0 })
                WriteAnimationChannel(gltfAnim, targetNodeIdx, GltfConstants.PathScale, track.ScaleCurve, 3, frameRate);
        }
        if (rebasedBones.Count > 0)
            WriteRebasedAnimation(gltfAnim, rebasedBones, tracksByName, frameRate);

        if (gltfAnim.Channels.Count > 0)
            _doc.Animations.Add(gltfAnim);
    }

    /// <summary>
    /// Writes a single animation channel (sampler + channel) to the glTF animation.
    /// </summary>
    /// <param name="anim">The glTF animation to add the channel to.</param>
    /// <param name="targetNode">The index of the target glTF node.</param>
    /// <param name="path">The animation target path (e.g., translation, rotation, scale).</param>
    /// <param name="curve">The animation curve containing keyframe data.</param>
    /// <param name="componentCount">The number of components per keyframe value (e.g., 3 for vec3, 4 for quaternion).</param>
    public void WriteAnimationChannel(GltfAnimation anim, int targetNode, string path, AnimationCurve curve, int componentCount)
    {
        WriteAnimationChannel(anim, targetNode, path, curve, componentCount, 30f);
    }

    /// <summary>
    /// Writes a float array to the binary buffer and creates an accessor.
    /// </summary>
    /// <param name="data">The float data to write.</param>
    /// <param name="type">The glTF accessor type (e.g., VEC3, SCALAR).</param>
    /// <param name="componentType">The glTF component type constant (e.g., FLOAT).</param>
    /// <returns>The index of the newly created glTF accessor.</returns>
    public int WriteAccessor(float[] data, string type, int componentType)
    {
        return WriteAccessor(data, type, componentType, null, null);
    }

    /// <summary>
    /// Writes a float array to the binary buffer and creates an accessor with optional min/max bounds.
    /// </summary>
    /// <param name="data">The float data to write.</param>
    /// <param name="type">The glTF accessor type (e.g., VEC3, SCALAR).</param>
    /// <param name="componentType">The glTF component type constant (e.g., FLOAT).</param>
    /// <param name="min">Optional per-component minimum values for the accessor.</param>
    /// <param name="max">Optional per-component maximum values for the accessor.</param>
    /// <returns>The index of the newly created glTF accessor.</returns>
    public int WriteAccessor(float[] data, string type, int componentType, float[]? min, float[]? max)
    {
        int componentCount	= GltfConstants.GetComponentCount(type);
        int elementCount	= data.Length / componentCount;

        AlignBinBuffer(4);

        int byteOffset				= (int)_binBuffer.Position;
        ReadOnlySpan<byte> bytes	= MemoryMarshal.AsBytes(data.AsSpan());
        _binBuffer.Write(bytes);

        int viewIdx = _doc.BufferViews.Count;
        _doc.BufferViews.Add(new GltfBufferView
        {
            Buffer = 0,
            ByteOffset = byteOffset,
            ByteLength = bytes.Length
        });

        int accIdx = _doc.Accessors.Count;
        _doc.Accessors.Add(new GltfAccessor
        {
            BufferView = viewIdx,
            ComponentType = componentType,
            Count = elementCount,
            Type = type,
            Min = min,
            Max = max
        });

        return accIdx;
    }

    /// <summary>
    /// Writes an unsigned short array to the binary buffer and creates an accessor.
    /// </summary>
    /// <param name="data">The unsigned short data to write.</param>
    /// <param name="type">The glTF accessor type (e.g., VEC4, SCALAR).</param>
    /// <returns>The index of the newly created glTF accessor.</returns>
    public int WriteAccessorShort(ushort[] data, string type)
    {
        int componentCount	= GltfConstants.GetComponentCount(type);
        int elementCount	= data.Length / componentCount;

        AlignBinBuffer(4);

        int byteOffset				= (int)_binBuffer.Position;
        ReadOnlySpan<byte> bytes	= MemoryMarshal.AsBytes(data.AsSpan());
        _binBuffer.Write(bytes);

        int viewIdx = _doc.BufferViews.Count;
        _doc.BufferViews.Add(new GltfBufferView
        {
            Buffer = 0,
            ByteOffset = byteOffset,
            ByteLength = bytes.Length
        });

        int accIdx = _doc.Accessors.Count;
        _doc.Accessors.Add(new GltfAccessor
        {
            BufferView = viewIdx,
            ComponentType = GltfConstants.ComponentTypeUnsignedShort,
            Count = elementCount,
            Type = type
        });

        return accIdx;
    }

    /// <summary>
    /// Writes an integer array to the binary buffer and creates an accessor.
    /// </summary>
    /// <param name="data">The integer data to write.</param>
    /// <param name="type">The glTF accessor type (e.g., SCALAR).</param>
    /// <returns>The index of the newly created glTF accessor.</returns>
    public int WriteAccessorInt(int[] data, string type)
    {
        int componentCount	= GltfConstants.GetComponentCount(type);
        int elementCount	= data.Length / componentCount;

        AlignBinBuffer(4);

        int byteOffset				= (int)_binBuffer.Position;
        ReadOnlySpan<byte> bytes	= MemoryMarshal.AsBytes(data.AsSpan());
        _binBuffer.Write(bytes);

        int viewIdx = _doc.BufferViews.Count;
        _doc.BufferViews.Add(new GltfBufferView
        {
            Buffer = 0,
            ByteOffset = byteOffset,
            ByteLength = bytes.Length
        });

        int accIdx = _doc.Accessors.Count;
        _doc.Accessors.Add(new GltfAccessor
        {
            BufferView = viewIdx,
            ComponentType = GltfConstants.ComponentTypeUnsignedInt,
            Count = elementCount,
            Type = type
        });

        return accIdx;
    }

    /// <summary>
    /// Aligns the binary buffer to the specified byte boundary with zero padding.
    /// </summary>
    /// <param name="alignment">The byte alignment boundary (e.g., 4 for 4-byte alignment).</param>
    public void AlignBinBuffer(int alignment)
    {
        long remainder = _binBuffer.Position % alignment;
        if (remainder != 0)
        {
            int padding = alignment - (int)remainder;
            for (int i = 0; i < padding; i++)
                _binBuffer.WriteByte(0);
        }
    }

    /// <summary>
    /// Reads a DataBuffer's contents as a float array with the specified value and component counts.
    /// </summary>
    /// <param name="buffer">The data buffer to read from.</param>
    /// <param name="valueIndex">The value index within each element to read.</param>
    /// <param name="componentCount">The number of components to read per element.</param>
    /// <returns>A float array containing the extracted buffer data.</returns>
    public static float[] ReadBufferAsFloats(DataBuffer buffer, int valueIndex, int componentCount)
    {
        int elementCount	= buffer.ElementCount;
        float[] result		= new float[elementCount * componentCount];

        if (valueIndex == 0 && buffer.ValueCount == 1 && componentCount == buffer.ComponentCount && buffer.TryGetReadOnlySpan<float>(out ReadOnlySpan<float> values))
        {
            values.CopyTo(result);
            return result;
        }

        for (int e = 0; e < elementCount; e++)
        {
            for (int c = 0; c < componentCount; c++)
                result[e * componentCount + c] = buffer.Get<float>(e, valueIndex, c);
        }

        return result;
    }

    /// <summary>
    /// Computes per-component minimum and maximum values for a float array.
    /// </summary>
    /// <param name="data">The float array to analyze.</param>
    /// <param name="componentCount">The number of components per element.</param>
    /// <param name="min">When this method returns, contains the per-component minimum values.</param>
    /// <param name="max">When this method returns, contains the per-component maximum values.</param>
    public static void ComputeMinMax(float[] data, int componentCount, out float[] min, out float[] max)
    {
        min = new float[componentCount];
        max = new float[componentCount];

        for (int c = 0; c < componentCount; c++)
        {
            min[c] = float.MaxValue;
            max[c] = float.MinValue;
        }

        int elementCount = data.Length / componentCount;
        for (int e = 0; e < elementCount; e++)
        {
            for (int c = 0; c < componentCount; c++)
            {
                float v = data[e * componentCount + c];
                if (v < min[c]) min[c] = v;
                if (v > max[c]) max[c] = v;
            }
        }
    }

    /// <summary>
    /// Applies a RedFox <see cref="Transform"/> to a glTF node as TRS components.
    /// </summary>
    /// <param name="transform">The transform containing translation, rotation, and scale values.</param>
    /// <param name="node">The glTF node to apply the transform to.</param>
    public static void WriteNodeTransform(Transform transform, GltfNode node)
    {
        if (transform.LocalPosition.HasValue)
        {
            Vector3 p = transform.LocalPosition.Value;
            node.Translation = [p.X, p.Y, p.Z];
        }

        if (transform.LocalRotation.HasValue)
        {
            Quaternion r = transform.LocalRotation.Value;
            node.Rotation = [r.X, r.Y, r.Z, r.W];
        }

        if (transform.Scale.HasValue)
        {
            Vector3 s = transform.Scale.Value;
            node.Scale = [s.X, s.Y, s.Z];
        }
    }

    /// <summary>
    /// Writes a <see cref="Matrix4x4"/> into a float array in glTF column-major order.
    /// </summary>
    /// <param name="matrix">The matrix to write.</param>
    /// <param name="data">The destination float array.</param>
    /// <param name="offset">The starting offset in the array.</param>
    public static void WriteMatrix4x4(Matrix4x4 matrix, float[] data, int offset)
    {
        data[offset + 0]	= matrix.M11;
        data[offset + 1]	= matrix.M12;
        data[offset + 2]	= matrix.M13;
        data[offset + 3]	= matrix.M14;
        data[offset + 4]	= matrix.M21;
        data[offset + 5]	= matrix.M22;
        data[offset + 6]	= matrix.M23;
        data[offset + 7]	= matrix.M24;
        data[offset + 8]	= matrix.M31;
        data[offset + 9]	= matrix.M32;
        data[offset + 10]	= matrix.M33;
        data[offset + 11]	= matrix.M34;
        data[offset + 12]	= matrix.M41;
        data[offset + 13]	= matrix.M42;
        data[offset + 14]	= matrix.M43;
        data[offset + 15]	= matrix.M44;
    }

    /// <summary>
    /// Serializes the glTF document as a GLB binary container to the output stream.
    /// </summary>
    /// <param name="stream">The output stream to write the GLB data to.</param>
    /// <param name="binData">The binary payload to include in the GLB container.</param>
    public void WriteGlb(Stream stream, byte[] binData) => WriteGlb(stream, binData.AsSpan());

    internal void Write(SceneTranslationSelection selection, Stream stream, string name, bool json, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(selection);
        token.ThrowIfCancellationRequested();

        _doc.Scene = 0;

        Material[] materials			= selection.GetDescendants<Material>();
        Mesh[] meshes					= selection.GetDescendants<Mesh>();
        SkeletonAnimation[] animations	= selection.GetDescendants<SkeletonAnimation>();
        IReadOnlyList<(string Name, SkeletonBone[] Bones)> skeletons = GetSelectedSkeletons(selection);

        foreach (Material mat in materials)
            WriteMaterial(mat);

        Dictionary<string, int> skinIndices = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string skeletonName, SkeletonBone[] bones) in skeletons)
        {
            WriteSkeleton(skeletonName, bones, false);
        }

        List<int> meshNodeIndices = [];
        foreach (Mesh mesh in meshes)
        {
            token.ThrowIfCancellationRequested();
            int nodeIdx = WriteMesh(mesh, skinIndices);
            if (nodeIdx >= 0)
                meshNodeIndices.Add(nodeIdx);
        }

        foreach (SkeletonAnimation anim in animations)
            WriteAnimation(anim);

        List<int> sceneNodes = [.. meshNodeIndices];

        foreach (GltfSkin skin in _doc.Skins)
        {
            if (skin.SkeletonRoot >= 0 && !sceneNodes.Contains(skin.SkeletonRoot))
                sceneNodes.Add(skin.SkeletonRoot);
        }

        HashSet<int> childNodes = [];
        foreach (GltfNode node in _doc.Nodes)
        {
            if (node.Children is not null)
            {
                foreach (int c in node.Children)
                    childNodes.Add(c);
            }
        }

        foreach (int boneNodeIdx in _boneNodeIndices.Values)
        {
            if (!childNodes.Contains(boneNodeIdx) && !sceneNodes.Contains(boneNodeIdx))
                sceneNodes.Add(boneNodeIdx);
        }

        _doc.Scenes.Add(new GltfScene
        {
            Name	= name,
            Nodes	= [.. sceneNodes],
        });

        ReadOnlySpan<byte> binData = _binBuffer.GetBuffer().AsSpan(0, checked((int)_binBuffer.Length));
        if (binData.Length > 0)
        {
            _doc.Buffers.Add(new GltfBuffer
            {
                ByteLength = binData.Length,
            });
        }

        token.ThrowIfCancellationRequested();
        if (json)
        {
            if (_doc.Buffers.Count > 0)
                _doc.Buffers[0].Uri = "data:application/octet-stream;base64," + Convert.ToBase64String(binData);
            stream.Write(GltfJsonWriter.Write(_doc));
        }
        else
        {
            WriteGlb(stream, binData);
        }
    }

    private int WriteSkeleton(string skeletonName, SkeletonBone[] bones, bool writeSkin)
    {
        if (bones.Length == 0) return -1;

        List<int> jointNodeIndices		= [];
        SceneNode[] exportedBoneNodes	= Array.ConvertAll(bones, static bone => (SceneNode)bone);

        Dictionary<SkeletonBone, SkeletonBone?> parents = SkeletonExportTransforms.BuildExportedParentMap(bones, exportedBoneNodes);

        for (int i = 0; i < bones.Length; i++)
        {
            SkeletonBone bone	= bones[i];
            int nodeIdx			= _doc.Nodes.Count;
            GltfNode node		= new()
            {
                Name = bone.Name,
            };
            WriteNodeTransform(SkeletonExportTransforms.DecomposeMatrix(SkeletonExportTransforms.GetRelativeBindMatrix(bone, parents[bone])), node);
            _doc.Nodes.Add(node);
            _boneNodeIndices[bone]	= nodeIdx;
            _exportedParents[bone]	= parents[bone];
            jointNodeIndices.Add(nodeIdx);
        }

        Dictionary<SkeletonBone, List<int>> children = bones.ToDictionary(static bone => bone, static _ => new List<int>());
        foreach (SkeletonBone bone in bones)
            if (parents[bone] is { } parent)
                children[parent].Add(_boneNodeIndices[bone]);
        foreach (SkeletonBone bone in bones)
            if (children[bone].Count > 0)
                _doc.Nodes[_boneNodeIndices[bone]].Children = [.. children[bone]];

        if (!writeSkin)
            return -1;

        float[] ibmData = new float[bones.Length * 16];
        for (int i = 0; i < bones.Length; i++)
        {
            if (!Matrix4x4.Invert(bones[i].GetBindWorldMatrix(), out Matrix4x4 inverseBindMatrix))
                throw new InvalidDataException("Singular joint bind matrix.");
            WriteMatrix4x4(inverseBindMatrix, ibmData, i * 16);
        }

        int ibmAccessor = WriteAccessor(ibmData, GltfConstants.TypeMat4, GltfConstants.ComponentTypeFloat);

        int skeletonRoot = -1;
        foreach (SkeletonBone bone in bones)
        {
            if (parents[bone] is null && _boneNodeIndices.TryGetValue(bone, out int rootIdx))
            {
                skeletonRoot = rootIdx;
                break;
            }
        }

        int skinIdx = _doc.Skins.Count;
        _doc.Skins.Add(new GltfSkin
        {
            Name				= skeletonName,
            Joints				= [.. jointNodeIndices],
            InverseBindMatrices	= ibmAccessor,
            SkeletonRoot		= skeletonRoot
        });

        return skinIdx;
    }

    private void WriteGlb(Stream stream, ReadOnlySpan<byte> binData)
    {
        byte[] jsonBytes = GltfJsonWriter.Write(_doc);

        int jsonPadding			= (4 - jsonBytes.Length % 4) % 4;
        int paddedJsonLength	= jsonBytes.Length + jsonPadding;

        int binPadding		= binData.Length > 0 ? (4 - binData.Length % 4) % 4 : 0;
        int paddedBinLength	= binData.Length + binPadding;

        int totalLength = GltfConstants.GlbHeaderSize;
        totalLength += GltfConstants.GlbChunkHeaderSize + paddedJsonLength;
        if (binData.Length > 0)
            totalLength += GltfConstants.GlbChunkHeaderSize + paddedBinLength;

        Span<byte> header = stackalloc byte[GltfConstants.GlbHeaderSize];
        BinaryPrimitives.WriteUInt32LittleEndian(header, GltfConstants.GlbMagic);
        BinaryPrimitives.WriteUInt32LittleEndian(header[4..], GltfConstants.GltfVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(header[8..], (uint)totalLength);
        stream.Write(header);

        Span<byte> chunkHeader = stackalloc byte[GltfConstants.GlbChunkHeaderSize];
        BinaryPrimitives.WriteUInt32LittleEndian(chunkHeader, (uint)paddedJsonLength);
        BinaryPrimitives.WriteUInt32LittleEndian(chunkHeader[4..], GltfConstants.ChunkTypeJson);
        stream.Write(chunkHeader);
        stream.Write(jsonBytes);
        for (int i = 0; i < jsonPadding; i++)
            stream.WriteByte(0x20);

        if (binData.Length > 0)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(chunkHeader, (uint)paddedBinLength);
            BinaryPrimitives.WriteUInt32LittleEndian(chunkHeader[4..], GltfConstants.ChunkTypeBin);
            stream.Write(chunkHeader);
            stream.Write(binData);
            for (int i = 0; i < binPadding; i++)
                stream.WriteByte(0);
        }
    }

    private bool TryResolveTexturePath(Material material, string? mapName, out string? imagePath)
    {
        imagePath = null;
        if (string.IsNullOrWhiteSpace(mapName))
            return false;

        if (material.TryGetTexture(mapName, out Texture? texture))
        {
            imagePath = texture.GetPortableFilePath(_targetDirectoryPath);
            return !string.IsNullOrWhiteSpace(imagePath);
        }

        imagePath = NormalizePath(mapName);
        return true;
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private static IReadOnlyList<(string Name, SkeletonBone[] Bones)> GetSelectedSkeletons(SceneTranslationSelection selection)
    {
        Dictionary<SceneNode, List<SkeletonBone>> bonesBySkeleton = [];

        foreach (SkeletonBone bone in selection.GetDescendants<SkeletonBone>())
        {
            SceneNode skeleton = GetSkeletonRoot(bone);

            if (!bonesBySkeleton.TryGetValue(skeleton, out List<SkeletonBone>? bones))
            {
                bones = [];
                bonesBySkeleton.Add(skeleton, bones);
            }

            bones.Add(bone);
        }

        List<(string Name, SkeletonBone[] Bones)> result = [];
        foreach ((SceneNode skeleton, List<SkeletonBone> bones) in bonesBySkeleton)
            result.Add((skeleton.Name, [.. bones]));

        return result;
    }

    private static SceneNode GetSkeletonRoot(SkeletonBone bone)
    {
        SceneNode root = bone;

        for (SceneNode? current = bone.Parent; current is not null; current = current.Parent)
        {
            if (current is Skeleton)
                return current;

            if (current is SkeletonBone)
                root = current;
        }

        return root;
    }

    private static Transform CreateWorldBindTransform(SceneNode node) => SkeletonExportTransforms.DecomposeMatrix(node.GetBindWorldMatrix());

    private void WriteRebasedAnimation(GltfAnimation animation, List<SkeletonBone> bones, Dictionary<string, SkeletonAnimationTrack> tracks, float frameRate)
    {
        SortedSet<float> times = [];
        foreach (SkeletonAnimationTrack track in tracks.Values)
            foreach (AnimationCurve? curve in new[] { track.TranslationCurve, track.RotationCurve, track.ScaleCurve })
                if (curve is not null)
                    for (int i = 0; i < curve.KeyFrameCount; i++)
                        times.Add(curve.GetKeyTime(i));
        if (times.Count == 0)
            return;
        if (!float.IsFinite(times.Min) || !float.IsFinite(times.Max) || times.Max >= int.MaxValue)
            throw new InvalidDataException("Invalid glTF animation frame range.");
        for (int frame = checked((int)MathF.Ceiling(times.Min)); frame < times.Max; frame++)
            times.Add(frame);
        float[] frames			= [.. times];
        DataBuffer<float> keys	= new(frames, 1, 1);
        float[][] positions		= new float[bones.Count][];
        float[][] rotations		= new float[bones.Count][];
        float[][] scales		= new float[bones.Count][];
        for (int b = 0; b < bones.Count; b++)
        {
            positions[b]	= new float[checked(frames.Length * 3)];
            rotations[b]	= new float[checked(frames.Length * 4)];
            scales[b]		= new float[checked(frames.Length * 3)];
        }
        Dictionary<SkeletonBone, Matrix4x4> transforms = [];
        for (int f = 0; f < frames.Length; f++)
        {
            transforms.Clear();
            for (int b = 0; b < bones.Count; b++)
            {
                SkeletonBone bone	= bones[b];
                Matrix4x4 matrix	= SkeletonExportTransforms.GetRelativeAnimatedMatrix(bone, _exportedParents[bone], tracks, frames[f], transforms);
                SkeletonExportTransforms.DecomposeMatrix(matrix, out Vector3 scale, out Quaternion rotation, out Vector3 position);
                position.CopyTo(positions[b], f * 3);
                new Vector4(rotation.X, rotation.Y, rotation.Z, rotation.W).CopyTo(rotations[b], f * 4);
                scale.CopyTo(scales[b], f * 3);
            }
        }
        for (int b = 0; b < bones.Count; b++)
        {
            int target = _boneNodeIndices[bones[b]];
            WriteAnimationChannel(animation, target, GltfConstants.PathTranslation, new AnimationCurve
            {
                Keys	= keys,
                Values	= new DataBuffer<float>(positions[b], 1, 3),
            }, 3, frameRate);
            WriteAnimationChannel(animation, target, GltfConstants.PathRotation, new AnimationCurve
            {
                Keys	= keys,
                Values	= new DataBuffer<float>(rotations[b], 1, 4),
            }, 4, frameRate);
            WriteAnimationChannel(animation, target, GltfConstants.PathScale, new AnimationCurve
            {
                Keys	= keys,
                Values	= new DataBuffer<float>(scales[b], 1, 3),
            }, 3, frameRate);
        }
    }

    private void AddPrimitive(GltfMesh output, Mesh mesh, GltfMeshPrimitive attributes, int start, int count, int materialIndex)
    {
        int indices = WriteMeshIndices(mesh, start, count);
        if (count == 0)
            return;
        output.Primitives.Add(new GltfMeshPrimitive
        {
            Attributes	= attributes.Attributes,
            Targets		= attributes.Targets,
            Indices		= indices,
            Material	= materialIndex,
        });
    }

    private int WriteMeshIndices(Mesh mesh, int start, int count)
    {
        if (mesh.FaceIndices is null || start < 0 || count < 0 || start % 3 != 0 || count % 3 != 0 || (long)start + count > mesh.IndexCount)
            throw new InvalidDataException($"Mesh '{mesh.Name}' has an invalid triangle range.");
        int vertexCount	= mesh.VertexCount;
        int[] indices	= new int[count];
        bool useShort	= true;
        for (int i = 0; i < count; i++)
        {
            int index = mesh.FaceIndices.Get<int>(start + i, 0, 0);
            if ((uint)index >= (uint)vertexCount)
                throw new InvalidDataException($"Mesh '{mesh.Name}' has an invalid vertex index.");
            indices[i] = index;
            useShort &= index < ushort.MaxValue;
        }
        return useShort ? WriteAccessorShort(Array.ConvertAll(indices, static index => (ushort)index), GltfConstants.TypeScalar) : WriteAccessorInt(indices, GltfConstants.TypeScalar);
    }

    private void WriteAnimationChannel(GltfAnimation anim, int targetNode, string path, AnimationCurve curve, int componentCount, float frameRate)
    {
        DataBuffer keys			= curve.Keys ?? throw new InvalidDataException("Missing glTF animation keys.");
        DataBuffer valuesBuffer	= curve.Values ?? throw new InvalidDataException("Missing glTF animation values.");
        int frameCount			= keys.ElementCount;
        if (keys.ValueCount != 1 || keys.ComponentCount != 1 || valuesBuffer.ElementCount != frameCount || valuesBuffer.ValueCount != 1 || valuesBuffer.ComponentCount != componentCount)
            throw new InvalidDataException("Invalid glTF animation curve dimensions.");

        if (frameCount == 0)
            throw new InvalidDataException("An animated glTF channel requires keys.");
        float[] times		= new float[frameCount];
        float minimumTime	= float.PositiveInfinity;
        float maximumTime	= float.NegativeInfinity;
        for (int i = 0; i < frameCount; i++)
        {
            float time = curve.GetKeyTime(i) / frameRate;
            times[i]	= time;
            minimumTime	= MathF.Min(minimumTime, time);
            maximumTime	= MathF.Max(maximumTime, time);
        }
        float[] minTime		= [minimumTime];
        float[] maxTime		= [maximumTime];
        int inputAccessor	= WriteAccessor(times, GltfConstants.TypeScalar, GltfConstants.ComponentTypeFloat, minTime, maxTime);

        float[] values = new float[frameCount * componentCount];
        for (int i = 0; i < frameCount; i++)
        {
            for (int c = 0; c < componentCount; c++)
                values[i * componentCount + c] = valuesBuffer.Get<float>(i, 0, c);
        }

        string accessorType = componentCount switch
        {
            1 => GltfConstants.TypeScalar,
            3 => GltfConstants.TypeVec3,
            4 => GltfConstants.TypeVec4,
            _ => GltfConstants.TypeScalar
        };

        int outputAccessor = WriteAccessor(values, accessorType, GltfConstants.ComponentTypeFloat);

        int samplerIdx = anim.Samplers.Count;
        anim.Samplers.Add(new GltfAnimationSampler
        {
            Input = inputAccessor,
            Output = outputAccessor,
            Interpolation = GltfConstants.InterpolationLinear
        });

        anim.Channels.Add(new GltfAnimationChannel
        {
            Sampler = samplerIdx,
            TargetNode = targetNode,
            TargetPath = path
        });
    }
}
