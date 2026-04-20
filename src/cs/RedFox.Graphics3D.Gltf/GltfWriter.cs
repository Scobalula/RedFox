using System.Buffers.Binary;
using System.Numerics;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.Gltf;

/// <summary>
/// Writes a <see cref="Scene"/> to a GLB binary container by converting the scene graph
/// into a <see cref="GltfDocument"/> and then serializing the JSON and binary payload.
/// <para>
/// Exports meshes with positions, normals, tangents, UVs, colors, face indices,
/// skinning data, morph targets, materials, skeletons, and animations.
/// </para>
/// </summary>
public sealed class GltfWriter
{
    private readonly SceneTranslatorOptions _options;
    private readonly string? _targetDirectoryPath;
    private readonly GltfDocument _doc = new();
    private readonly MemoryStream _binBuffer = new();

    /// <summary>
    /// Mapping from RedFox <see cref="Material"/> to glTF material index.
    /// </summary>
    private readonly Dictionary<Material, int> _materialIndices = [];

    /// <summary>
    /// Mapping from RedFox <see cref="SkeletonBone"/> to glTF node index.
    /// </summary>
    private readonly Dictionary<SkeletonBone, int> _boneNodeIndices = [];

    /// <summary>
    /// Initializes a new <see cref="GltfWriter"/> with the specified translation options.
    /// </summary>
    /// <param name="options">Options that control how the scene data is written.</param>
    public GltfWriter(SceneTranslatorOptions options, string? targetDirectoryPath = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _targetDirectoryPath = targetDirectoryPath;
    }

    /// <summary>
    /// Writes the specified scene to the output stream in GLB binary format.
    /// </summary>
    /// <param name="scene">The scene to export.</param>
    /// <param name="stream">The output stream for the GLB data.</param>
    /// <param name="name">The scene name.</param>
    public void Write(Scene scene, Stream stream, string name)
    {
        _doc.Scene = 0;

        // Collect scene data
        Material[] materials = scene.GetDescendants<Material>(SceneNodeFlags.NoExport);
        Mesh[] meshes = scene.GetDescendants<Mesh>(SceneNodeFlags.NoExport);
        Skeleton[] skeletons = scene.GetDescendants<Skeleton>(SceneNodeFlags.NoExport);
        SkeletonAnimation[] animations = scene.GetDescendants<SkeletonAnimation>(SceneNodeFlags.NoExport);

        // Write materials first
        foreach (Material mat in materials)
            WriteMaterial(mat);

        // Write skeletons (create joint nodes)
        Dictionary<Skeleton, int> skinIndices = [];
        foreach (Skeleton skeleton in skeletons)
        {
            int skinIdx = WriteSkeleton(skeleton);
            if (skinIdx >= 0)
                skinIndices[skeleton] = skinIdx;
        }

        // Write meshes
        List<int> meshNodeIndices = [];
        foreach (Mesh mesh in meshes)
        {
            int nodeIdx = WriteMesh(mesh, skinIndices);
            if (nodeIdx >= 0)
                meshNodeIndices.Add(nodeIdx);
        }

        // Write animations
        foreach (SkeletonAnimation anim in animations)
            WriteAnimation(anim);

        // Build scene with root node references
        List<int> sceneNodes = [.. meshNodeIndices];

        // Add skeleton root nodes (joint roots that aren't children of mesh nodes)
        foreach (GltfSkin skin in _doc.Skins)
        {
            if (skin.SkeletonRoot >= 0 && !sceneNodes.Contains(skin.SkeletonRoot))
                sceneNodes.Add(skin.SkeletonRoot);
        }

        // Also add orphaned joint nodes
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

        _doc.Scenes.Add(new GltfScene { Name = name, Nodes = [.. sceneNodes] });

        // Set up the single buffer
        byte[] binData = _binBuffer.ToArray();
        if (binData.Length > 0)
        {
            _doc.Buffers.Add(new GltfBuffer { ByteLength = binData.Length });
        }

        // Serialize to GLB
        WriteGlb(stream, binData);
    }

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

        // Write texture references
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
        _doc.Images.Add(new GltfImage { Uri = imagePath, Name = Path.GetFileNameWithoutExtension(imagePath) });

        int texIdx = _doc.Textures.Count;
        _doc.Textures.Add(new GltfTexture { Source = imageIdx });

        return texIdx;
    }

    private bool TryResolveTexturePath(Material material, string? mapName, out string? imagePath)
    {
        imagePath = null;
        if (string.IsNullOrWhiteSpace(mapName))
            return false;

        if (material.TryFindTexture(mapName, StringComparison.OrdinalIgnoreCase, out Texture? texture))
        {
            imagePath = GetPortableTexturePath(texture);
            return !string.IsNullOrWhiteSpace(imagePath);
        }

        imagePath = NormalizePath(mapName);
        return true;
    }

    private string GetPortableTexturePath(Texture texture)
    {
        string effectivePath = texture.EffectiveFilePath;
        if (string.IsNullOrWhiteSpace(effectivePath))
            return NormalizePath(texture.FilePath);

        if (Path.IsPathRooted(effectivePath) && !string.IsNullOrWhiteSpace(_targetDirectoryPath))
            return NormalizePath(Path.GetRelativePath(_targetDirectoryPath, effectivePath));

        return NormalizePath(texture.FilePath);
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    /// <summary>
    /// Writes a <see cref="Mesh"/> to the glTF document, returning the node index.
    /// </summary>
    /// <param name="mesh">The mesh to write.</param>
    /// <param name="skinIndices">A mapping from <see cref="Skeleton"/> instances to their glTF skin indices.</param>
    /// <returns>The index of the glTF node created for this mesh.</returns>
    public int WriteMesh(Mesh mesh, Dictionary<Skeleton, int> skinIndices)
    {
        GltfMeshPrimitive prim = new();

        // Positions
        if (mesh.Positions is not null)
        {
            float[] positions = ReadBufferAsFloats(mesh.Positions, 0, 3);
            ComputeMinMax(positions, 3, out float[] min, out float[] max);
            prim.Attributes["POSITION"] = WriteAccessor(positions, GltfConstants.TypeVec3, GltfConstants.ComponentTypeFloat, min, max);
        }

        // Normals
        if (mesh.Normals is not null)
            prim.Attributes["NORMAL"] = WriteAccessor(ReadBufferAsFloats(mesh.Normals, 0, 3), GltfConstants.TypeVec3, GltfConstants.ComponentTypeFloat);

        // Tangents
        if (mesh.Tangents is not null)
        {
            int tangentComponents = mesh.Tangents.ComponentCount;
            prim.Attributes["TANGENT"] = WriteAccessor(
                ReadBufferAsFloats(mesh.Tangents, 0, tangentComponents),
                tangentComponents == 4 ? GltfConstants.TypeVec4 : GltfConstants.TypeVec3,
                GltfConstants.ComponentTypeFloat);
        }

        // UV Layers
        if (mesh.UVLayers is not null)
        {
            int uvLayers = mesh.UVLayers.ValueCount;
            int vertexCount = mesh.UVLayers.ElementCount;

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

        // Color Layers
        if (mesh.ColorLayers is not null)
        {
            int colorLayers = mesh.ColorLayers.ValueCount;
            int vertexCount = mesh.ColorLayers.ElementCount;
            int compCount = mesh.ColorLayers.ComponentCount;

            for (int layer = 0; layer < colorLayers; layer++)
            {
                float[] colorData = new float[vertexCount * compCount];
                for (int v = 0; v < vertexCount; v++)
                {
                    for (int c = 0; c < compCount; c++)
                        colorData[v * compCount + c] = mesh.ColorLayers.Get<float>(v, layer, c);
                }
                prim.Attributes[$"COLOR_{layer}"] = WriteAccessor(
                    colorData,
                    compCount == 3 ? GltfConstants.TypeVec3 : GltfConstants.TypeVec4,
                    GltfConstants.ComponentTypeFloat);
            }
        }

        // Face Indices
        if (mesh.FaceIndices is not null)
        {
            int indexCount = mesh.FaceIndices.ElementCount;
            int[] indices = new int[indexCount];
            for (int i = 0; i < indexCount; i++)
                indices[i] = mesh.FaceIndices.Get<int>(i, 0, 0);

            // Use ushort if possible
            bool canUseShort = true;
            foreach (int idx in indices)
            {
                if (idx > ushort.MaxValue)
                {
                    canUseShort = false;
                    break;
                }
            }

            if (canUseShort)
            {
                ushort[] shortIndices = new ushort[indexCount];
                for (int i = 0; i < indexCount; i++)
                    shortIndices[i] = (ushort)indices[i];
                prim.Indices = WriteAccessorShort(shortIndices, GltfConstants.TypeScalar);
            }
            else
            {
                prim.Indices = WriteAccessorInt(indices, GltfConstants.TypeScalar);
            }
        }

        // Bone indices and weights
        WriteSkinInfluences(mesh, prim);

        // Morph targets
        WriteMorphTargets(mesh, prim);

        // Material reference
        if (mesh.Materials is { Count: > 0 } && _materialIndices.TryGetValue(mesh.Materials[0], out int matIdx))
            prim.Material = matIdx;

        // Create glTF mesh
        int gltfMeshIdx = _doc.Meshes.Count;
        GltfMesh gltfMesh = new() { Name = mesh.Name };
        gltfMesh.Primitives.Add(prim);
        _doc.Meshes.Add(gltfMesh);

        // Create glTF node
        int nodeIdx = _doc.Nodes.Count;
        GltfNode node = new()
        {
            Name = mesh.Name,
            Mesh = gltfMeshIdx
        };

        // Apply transform
        WriteNodeTransform(mesh.BindTransform, node);

        // Skinning reference
        if (mesh.SkinBindingName is not null)
        {
            foreach (KeyValuePair<Skeleton, int> kvp in skinIndices)
            {
                if (kvp.Key.Name.Equals(mesh.SkinBindingName, StringComparison.OrdinalIgnoreCase))
                {
                    node.Skin = kvp.Value;
                    break;
                }
            }
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
        if (mesh.BoneIndices is null || mesh.BoneWeights is null)
            return;

        int vertexCount = mesh.BoneIndices.ElementCount;
        int influenceCount = mesh.BoneIndices.ValueCount;
        int numSets = (influenceCount + 3) / 4;

        for (int set = 0; set < numSets; set++)
        {
            int startInfluence = set * 4;
            int setSize = Math.Min(4, influenceCount - startInfluence);

            // Joints
            int[] joints = new int[vertexCount * 4];
            for (int v = 0; v < vertexCount; v++)
            {
                for (int c = 0; c < setSize; c++)
                    joints[v * 4 + c] = mesh.BoneIndices.Get<int>(v, startInfluence + c, 0);
            }

            // Use unsigned short for joints
            ushort[] shortJoints = new ushort[vertexCount * 4];
            for (int i = 0; i < joints.Length; i++)
                shortJoints[i] = (ushort)joints[i];

            prim.Attributes[$"JOINTS_{set}"] = WriteAccessorShort(shortJoints, GltfConstants.TypeVec4);

            // Weights
            float[] weights = new float[vertexCount * 4];
            for (int v = 0; v < vertexCount; v++)
            {
                for (int c = 0; c < setSize; c++)
                    weights[v * 4 + c] = mesh.BoneWeights.Get<float>(v, startInfluence + c, 0);
            }

            prim.Attributes[$"WEIGHTS_{set}"] = WriteAccessor(weights, GltfConstants.TypeVec4, GltfConstants.ComponentTypeFloat);
        }
    }

    /// <summary>
    /// Writes morph targets from the mesh's delta buffers.
    /// </summary>
    /// <param name="mesh">The mesh containing delta position, normal, and tangent buffers.</param>
    /// <param name="prim">The glTF mesh primitive to add the morph target data to.</param>
    public void WriteMorphTargets(Mesh mesh, GltfMeshPrimitive prim)
    {
        if (mesh.DeltaPositions is null && mesh.DeltaNormals is null && mesh.DeltaTangents is null)
            return;

        int targetCount = mesh.DeltaPositions?.ValueCount ?? mesh.DeltaNormals?.ValueCount ?? mesh.DeltaTangents?.ValueCount ?? 0;
        if (targetCount == 0) return;

        int vertexCount = mesh.VertexCount;
        prim.Targets = [];

        for (int t = 0; t < targetCount; t++)
        {
            Dictionary<string, int> target = [];

            if (mesh.DeltaPositions is not null)
            {
                float[] data = new float[vertexCount * 3];
                for (int v = 0; v < vertexCount; v++)
                {
                    data[v * 3] = mesh.DeltaPositions.Get<float>(v, t, 0);
                    data[v * 3 + 1] = mesh.DeltaPositions.Get<float>(v, t, 1);
                    data[v * 3 + 2] = mesh.DeltaPositions.Get<float>(v, t, 2);
                }
                target["POSITION"] = WriteAccessor(data, GltfConstants.TypeVec3, GltfConstants.ComponentTypeFloat);
            }

            if (mesh.DeltaNormals is not null)
            {
                float[] data = new float[vertexCount * 3];
                for (int v = 0; v < vertexCount; v++)
                {
                    data[v * 3] = mesh.DeltaNormals.Get<float>(v, t, 0);
                    data[v * 3 + 1] = mesh.DeltaNormals.Get<float>(v, t, 1);
                    data[v * 3 + 2] = mesh.DeltaNormals.Get<float>(v, t, 2);
                }
                target["NORMAL"] = WriteAccessor(data, GltfConstants.TypeVec3, GltfConstants.ComponentTypeFloat);
            }

            if (mesh.DeltaTangents is not null)
            {
                float[] data = new float[vertexCount * 3];
                for (int v = 0; v < vertexCount; v++)
                {
                    data[v * 3] = mesh.DeltaTangents.Get<float>(v, t, 0);
                    data[v * 3 + 1] = mesh.DeltaTangents.Get<float>(v, t, 1);
                    data[v * 3 + 2] = mesh.DeltaTangents.Get<float>(v, t, 2);
                }
                target["TANGENT"] = WriteAccessor(data, GltfConstants.TypeVec3, GltfConstants.ComponentTypeFloat);
            }

            prim.Targets.Add(target);
        }
    }

    /// <summary>
    /// Writes a <see cref="Skeleton"/> to the glTF document, creating nodes for each bone
    /// and a skin definition. Returns the skin index.
    /// </summary>
    /// <param name="skeleton">The skeleton to write.</param>
    /// <returns>The index of the glTF skin, or -1 if the skeleton has no bones.</returns>
    public int WriteSkeleton(Skeleton skeleton)
    {
        SkeletonBone[] bones = skeleton.GetBones();
        if (bones.Length == 0) return -1;

        List<int> jointNodeIndices = [];

        // Create a node for each bone
        foreach (SkeletonBone bone in bones)
        {
            int nodeIdx = _doc.Nodes.Count;
            GltfNode node = new() { Name = bone.Name };
            WriteNodeTransform(bone.BindTransform, node);
            _doc.Nodes.Add(node);
            _boneNodeIndices[bone] = nodeIdx;
            jointNodeIndices.Add(nodeIdx);
        }

        // Set up parent-child relationships
        foreach (SkeletonBone bone in bones)
        {
            int parentIdx = _boneNodeIndices[bone];
            GltfNode parentNode = _doc.Nodes[parentIdx];

            if (bone.Children is not null)
            {
                List<int> childIndices = [];
                foreach (SceneNode child in bone.Children)
                {
                    if (child is SkeletonBone childBone && _boneNodeIndices.TryGetValue(childBone, out int childIdx))
                        childIndices.Add(childIdx);
                }
                if (childIndices.Count > 0)
                    parentNode.Children = [.. childIndices];
            }
        }

        // Write inverse bind matrices
        float[] ibmData = new float[bones.Length * 16];
        for (int i = 0; i < bones.Length; i++)
            WriteMatrix4x4(Matrix4x4.Identity, ibmData, i * 16);

        int ibmAccessor = WriteAccessor(ibmData, GltfConstants.TypeMat4, GltfConstants.ComponentTypeFloat);

        // Find skeleton root (first root bone node)
        int skeletonRoot = -1;
        foreach (SkeletonBone bone in bones)
        {
            if (bone.Parent == skeleton && _boneNodeIndices.TryGetValue(bone, out int rootIdx))
            {
                skeletonRoot = rootIdx;
                break;
            }
        }

        int skinIdx = _doc.Skins.Count;
        _doc.Skins.Add(new GltfSkin
        {
            Name = skeleton.Name,
            Joints = [.. jointNodeIndices],
            InverseBindMatrices = ibmAccessor,
            SkeletonRoot = skeletonRoot
        });

        return skinIdx;
    }

    /// <summary>
    /// Writes a <see cref="SkeletonAnimation"/> to the glTF document.
    /// </summary>
    /// <param name="anim">The skeleton animation to write.</param>
    public void WriteAnimation(SkeletonAnimation anim)
    {
        GltfAnimation gltfAnim = new() { Name = anim.Name };

        foreach (SkeletonAnimationTrack track in anim.Tracks)
        {
            // Find the node index for this bone
            int targetNodeIdx = -1;
            foreach (KeyValuePair<SkeletonBone, int> kvp in _boneNodeIndices)
            {
                if (kvp.Key.Name.Equals(track.Name, StringComparison.OrdinalIgnoreCase))
                {
                    targetNodeIdx = kvp.Value;
                    break;
                }
            }

            if (targetNodeIdx < 0)
                continue;

            // Translation
            if (track.TranslationCurve is { KeyFrameCount: > 0 })
                WriteAnimationChannel(gltfAnim, targetNodeIdx, GltfConstants.PathTranslation, track.TranslationCurve, 3);

            // Rotation
            if (track.RotationCurve is { KeyFrameCount: > 0 })
                WriteAnimationChannel(gltfAnim, targetNodeIdx, GltfConstants.PathRotation, track.RotationCurve, 4);

            // Scale
            if (track.ScaleCurve is { KeyFrameCount: > 0 })
                WriteAnimationChannel(gltfAnim, targetNodeIdx, GltfConstants.PathScale, track.ScaleCurve, 3);
        }

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
        int frameCount = curve.KeyFrameCount;

        // Write times
        float[] times = new float[frameCount];
        for (int i = 0; i < frameCount; i++)
            times[i] = curve.GetKeyTime(i);

        float[] minTime = [times.Min()];
        float[] maxTime = [times.Max()];
        int inputAccessor = WriteAccessor(times, GltfConstants.TypeScalar, GltfConstants.ComponentTypeFloat, minTime, maxTime);

        // Write values
        float[] values = new float[frameCount * componentCount];
        for (int i = 0; i < frameCount; i++)
        {
            for (int c = 0; c < componentCount; c++)
                values[i * componentCount + c] = curve.Values!.Get<float>(i, 0, c);
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
        int componentCount = GltfConstants.GetComponentCount(type);
        int elementCount = data.Length / componentCount;

        // Align to 4 bytes
        AlignBinBuffer(4);

        int byteOffset = (int)_binBuffer.Position;
        byte[] bytes = new byte[data.Length * 4];
        System.Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
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
        int componentCount = GltfConstants.GetComponentCount(type);
        int elementCount = data.Length / componentCount;

        // Align to 4 bytes
        AlignBinBuffer(4);

        int byteOffset = (int)_binBuffer.Position;
        byte[] bytes = new byte[data.Length * 2];
        System.Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
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
        int componentCount = GltfConstants.GetComponentCount(type);
        int elementCount = data.Length / componentCount;

        AlignBinBuffer(4);

        int byteOffset = (int)_binBuffer.Position;
        byte[] bytes = new byte[data.Length * 4];
        System.Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
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
        int elementCount = buffer.ElementCount;
        float[] result = new float[elementCount * componentCount];

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
        // .NET Matrix4x4 is row-major; glTF expects column-major
        data[offset + 0] = matrix.M11;
        data[offset + 1] = matrix.M21;
        data[offset + 2] = matrix.M31;
        data[offset + 3] = matrix.M41;
        data[offset + 4] = matrix.M12;
        data[offset + 5] = matrix.M22;
        data[offset + 6] = matrix.M32;
        data[offset + 7] = matrix.M42;
        data[offset + 8] = matrix.M13;
        data[offset + 9] = matrix.M23;
        data[offset + 10] = matrix.M33;
        data[offset + 11] = matrix.M43;
        data[offset + 12] = matrix.M14;
        data[offset + 13] = matrix.M24;
        data[offset + 14] = matrix.M34;
        data[offset + 15] = matrix.M44;
    }

    /// <summary>
    /// Serializes the glTF document as a GLB binary container to the output stream.
    /// </summary>
    /// <param name="stream">The output stream to write the GLB data to.</param>
    /// <param name="binData">The binary payload to include in the GLB container.</param>
    public void WriteGlb(Stream stream, byte[] binData)
    {
        byte[] jsonBytes = GltfJsonWriter.Write(_doc);

        // Pad JSON to 4-byte alignment with spaces
        int jsonPadding = (4 - jsonBytes.Length % 4) % 4;
        int paddedJsonLength = jsonBytes.Length + jsonPadding;

        // Pad BIN to 4-byte alignment with zeros
        int binPadding = binData.Length > 0 ? (4 - binData.Length % 4) % 4 : 0;
        int paddedBinLength = binData.Length + binPadding;

        int totalLength = GltfConstants.GlbHeaderSize;
        totalLength += GltfConstants.GlbChunkHeaderSize + paddedJsonLength;
        if (binData.Length > 0)
            totalLength += GltfConstants.GlbChunkHeaderSize + paddedBinLength;

        Span<byte> header = stackalloc byte[GltfConstants.GlbHeaderSize];
        BinaryPrimitives.WriteUInt32LittleEndian(header, GltfConstants.GlbMagic);
        BinaryPrimitives.WriteUInt32LittleEndian(header[4..], GltfConstants.GltfVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(header[8..], (uint)totalLength);
        stream.Write(header);

        // JSON chunk
        Span<byte> chunkHeader = stackalloc byte[GltfConstants.GlbChunkHeaderSize];
        BinaryPrimitives.WriteUInt32LittleEndian(chunkHeader, (uint)paddedJsonLength);
        BinaryPrimitives.WriteUInt32LittleEndian(chunkHeader[4..], GltfConstants.ChunkTypeJson);
        stream.Write(chunkHeader);
        stream.Write(jsonBytes);
        for (int i = 0; i < jsonPadding; i++)
            stream.WriteByte(0x20); // space

        // BIN chunk
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
}
