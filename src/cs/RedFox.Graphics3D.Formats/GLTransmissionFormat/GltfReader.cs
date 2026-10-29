using System.Buffers.Binary;
using System.Numerics;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Groups;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.Formats.GLTransmissionFormat;

/// <summary>
/// Reads a glTF 2.0 asset (.gltf or .glb) and populates a <see cref="Scene"/> with models, meshes, materials, skeletons, and animations.
/// </summary>
public sealed class GltfReader
{
    private CancellationToken _cancellationToken;

    private readonly GltfDocument	_doc;
    private readonly string			_name;
    private readonly string?		_baseDirectory;

    private readonly Dictionary<int, int>		_parentByNode		= [];
    private readonly Dictionary<int, Matrix4x4>	_nodeWorldMatrices	= [];

    private readonly Dictionary<int, SkeletonBone>	_jointBones		= [];
    private readonly HashSet<int>					_jointIndices	= [];

    /// <summary>
    /// Initializes a new <see cref="GltfReader"/> with a pre-parsed <see cref="GltfDocument"/>.
    /// </summary>
    /// <param name="doc">The parsed glTF document with buffer data loaded.</param>
    /// <param name="name">The scene/file name used for the root model node.</param>
    /// <param name="options">Translation options.</param>
    public GltfReader(GltfDocument doc, string name, SceneTranslatorOptions options)
        : this(doc, name, options, null)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="GltfReader"/> with a pre-parsed <see cref="GltfDocument"/> and base directory.
    /// </summary>
    /// <param name="doc">The parsed glTF document with buffer data loaded.</param>
    /// <param name="name">The scene/file name used for the root model node.</param>
    /// <param name="options">Translation options.</param>
    /// <param name="baseDirectory">The directory used to resolve relative external asset paths, if available.</param>
    public GltfReader(GltfDocument doc, string name, SceneTranslatorOptions options, string? baseDirectory)
    {
        _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        _name = name ?? throw new ArgumentNullException(nameof(name));
        ArgumentNullException.ThrowIfNull(options);
        _baseDirectory = baseDirectory;

        for (int parentIndex = 0; parentIndex < _doc.Nodes.Count; parentIndex++)
            if (_doc.Nodes[parentIndex].Children is { } children)
                foreach (int childIndex in children)
                {
                    if ((uint)childIndex >= (uint)_doc.Nodes.Count || !_parentByNode.TryAdd(childIndex, parentIndex))
                        throw new InvalidDataException("Invalid glTF child or multiple parents.");
                }

        byte[] states = new byte[_doc.Nodes.Count];
        for (int i = 0; i < states.Length; i++)
        {
            int current = i;
            while (states[current] == 0)
            {
                states[current] = 1;
                if (!_parentByNode.TryGetValue(current, out current))
                {
                    current = -1;
                    break;
                }
            }
            if (current >= 0 && states[current] == 1)
                throw new InvalidDataException("Cyclic glTF hierarchy.");
            current = i;
            while (states[current] == 1)
            {
                states[current] = 2;
                if (!_parentByNode.TryGetValue(current, out current))
                    break;
            }
        }
    }

    /// <summary>
    /// Parses a GLB binary container from the specified stream and returns a fully loaded <see cref="GltfDocument"/> with buffer data resolved.
    /// </summary>
    /// <param name="stream">The GLB input stream.</param>
    /// <returns>A <see cref="GltfDocument"/> with all buffer data populated.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the GLB header or chunks are invalid.</exception>
    public static GltfDocument ParseGlb(Stream stream)
    {
        long start			= stream.Position;
        Span<byte> header	= stackalloc byte[GltfConstants.GlbHeaderSize];
        ReadExact(stream, header);
        if (BinaryPrimitives.ReadUInt32LittleEndian(header) != GltfConstants.GlbMagic || BinaryPrimitives.ReadUInt32LittleEndian(header[4..]) != GltfConstants.GltfVersion)
            throw new InvalidOperationException("Invalid GLB header or version.");

        uint declaredLength = BinaryPrimitives.ReadUInt32LittleEndian(header[8..]);
        if (declaredLength < 20 || declaredLength != stream.Length - start)
            throw new InvalidDataException("Invalid GLB declared length.");

        long end				= start + declaredLength;
        Span<byte> chunkHeader	= stackalloc byte[GltfConstants.GlbChunkHeaderSize];
        GltfDocument? doc		= null;
        bool hasBinary			= false;
        int chunkIndex			= 0;
        while (stream.Position < end)
        {
            if (end - stream.Position < chunkHeader.Length)
                throw new InvalidDataException("Truncated GLB chunk header.");
            ReadExact(stream, chunkHeader);
            uint length	= BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader);
            uint type	= BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader[4..]);
            if (length % 4 != 0 || length > int.MaxValue || length > end - stream.Position)
                throw new InvalidDataException("Invalid GLB chunk boundary.");
            if (chunkIndex == 0 && type != GltfConstants.ChunkTypeJson)
                throw new InvalidOperationException("The first GLB chunk must contain JSON.");

            if (type == GltfConstants.ChunkTypeJson)
            {
                if (doc is not null)
                    throw new InvalidDataException("Duplicate GLB JSON chunk.");
                byte[] json = new byte[(int)length];
                ReadExact(stream, json);
                doc = GltfJsonParser.Parse(json);
            }
            else if (type == GltfConstants.ChunkTypeBin)
            {
                if (hasBinary || chunkIndex != 1 || doc is null || doc.Buffers.Count == 0 || doc.Buffers[0].Uri is not null || doc.Buffers[0].ByteLength < 0 || length < doc.Buffers[0].ByteLength || length - doc.Buffers[0].ByteLength > 3)
                    throw new InvalidDataException("Invalid GLB binary chunk.");
                byte[] binary = new byte[(int)length];
                ReadExact(stream, binary);
                doc.Buffers[0].Data = binary;
                hasBinary = true;
            }
            else
            {
                stream.Position += length;
            }
            chunkIndex++;
        }
        if (doc is null)
            throw new InvalidDataException("Missing GLB JSON chunk.");
        ResolveBuffers(doc, null);
        return doc;
    }

    /// <summary>
    /// Parses a JSON-based glTF file from the specified stream and resolves external buffer URIs relative to the given base directory.
    /// </summary>
    /// <param name="stream">The JSON input stream.</param>
    /// <param name="baseDirectory">The directory to resolve relative buffer URIs against, or <see langword="null"/> if external resolution is not available.</param>
    /// <returns>A <see cref="GltfDocument"/> with all buffer data populated.</returns>
    public static GltfDocument ParseGltf(Stream stream, string? baseDirectory)
    {
        GltfDocument doc;
        if (stream is MemoryStream ms && ms.TryGetBuffer(out ArraySegment<byte> segment))
        {
            doc			= GltfJsonParser.Parse(segment.AsMemory(checked((int)ms.Position)));
            ms.Position	= ms.Length;
        }
        else
        {
            using MemoryStream copy = new();
            stream.CopyTo(copy);
            doc = GltfJsonParser.Parse(copy.GetBuffer().AsMemory(0, checked((int)copy.Length)));
        }

        ResolveBuffers(doc, baseDirectory);
        return doc;
    }

    /// <summary>
    /// Populates the specified <see cref="Scene"/> with models, meshes, materials, skeletons, and animations from the parsed glTF document.
    /// </summary>
    /// <param name="scene">The scene to populate.</param>
    public void Read(Scene scene) => Read(scene, CancellationToken.None);

    /// <summary>
    /// Collects all node indices referenced as joints across all skins.
    /// </summary>
    public void CollectJointIndices()
    {
        foreach (GltfSkin skin in _doc.Skins)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            foreach (int jointIdx in skin.Joints)
                _jointIndices.Add(jointIdx);
        }
    }

    /// <summary>
    /// Returns the root node indices for the default scene, or all root nodes if no scene is specified.
    /// </summary>
    /// <returns>An array of root node indices for the active glTF scene.</returns>
    public int[] GetSceneRootNodes()
    {
        int sceneIdx = _doc.Scene >= 0 ? _doc.Scene : 0;
        if (sceneIdx < _doc.Scenes.Count)
            return _doc.Scenes[sceneIdx].Nodes;

        List<int> roots = [];
        for (int i = 0; i < _doc.Nodes.Count; i++)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (!_parentByNode.ContainsKey(i))
                roots.Add(i);
        }

        return [.. roots];
    }

    /// <summary>
    /// Recursively processes a glTF node and its children to extract meshes.
    /// </summary>
    /// <param name="nodeIdx">The index of the glTF node to process.</param>
    /// <param name="model">The model node to add meshes to.</param>
    /// <param name="materials">The array of resolved materials to assign to mesh primitives.</param>
    /// <param name="skeletons">The array of resolved skeletons for skinning setup.</param>
    /// <param name="meshCounter">A running counter used to generate unique mesh names.</param>
    public void ProcessNodeForMeshes(int nodeIdx, MeshGroup model, Material[] materials, SkeletonBone[] skeletons, ref int meshCounter)
    {
        HashSet<string> names = new(model.EnumerateChildren().Select(static node => node.Name), StringComparer.OrdinalIgnoreCase);
        ProcessNodeForMeshes(nodeIdx, model, materials, skeletons, ref meshCounter, names);
    }

    /// <summary>
    /// Reads vertex attributes from a glTF mesh primitive into the RedFox mesh.
    /// </summary>
    /// <param name="prim">The glTF mesh primitive containing vertex attribute accessors.</param>
    /// <param name="mesh">The target mesh to populate with vertex data.</param>
    public void ReadPrimitiveAttributes(GltfMeshPrimitive prim, Mesh mesh)
    {

        if (prim.Attributes.TryGetValue("POSITION", out int posAccessor))
        {
            if (_doc.Accessors[posAccessor].Type != "VEC3")
                throw new InvalidDataException("Invalid glTF attribute shape.");
            float[] positions = _doc.ReadAccessorAsFloats(posAccessor);
            mesh.Positions = new DataBuffer<float>(positions, 1, 3);
        }

        if (prim.Attributes.TryGetValue("NORMAL", out int normAccessor))
        {
            if (_doc.Accessors[normAccessor].Type != "VEC3" || _doc.Accessors[normAccessor].Count != mesh.VertexCount)
                throw new InvalidDataException("Invalid glTF attribute shape.");
            float[] normals = _doc.ReadAccessorAsFloats(normAccessor);
            mesh.Normals = new DataBuffer<float>(normals, 1, 3);
        }

        if (prim.Attributes.TryGetValue("TANGENT", out int tanAccessor))
        {
            if (_doc.Accessors[tanAccessor].Type != "VEC4" || _doc.Accessors[tanAccessor].Count != mesh.VertexCount)
                throw new InvalidDataException("Invalid glTF attribute shape.");
            float[] tangents = _doc.ReadAccessorAsFloats(tanAccessor);
            mesh.Tangents = new DataBuffer<float>(tangents, 1, 4);
        }

        ReadUVLayers(prim, mesh);

        ReadColorLayers(prim, mesh);
    }

    /// <summary>
    /// Reads UV texture coordinate layers (TEXCOORD_0, TEXCOORD_1, ...) and combines them into a single multi-value DataBuffer.
    /// </summary>
    /// <param name="prim">The glTF mesh primitive containing UV accessor references.</param>
    /// <param name="mesh">The target mesh to populate with UV layer data.</param>
    public void ReadUVLayers(GltfMeshPrimitive prim, Mesh mesh)
    {
        List<float[]> uvSets = [];
        for (int i = 0; ; i++)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (!prim.Attributes.TryGetValue($"TEXCOORD_{i}", out int uvAccessor))
                break;
            if (_doc.Accessors[uvAccessor].Type != "VEC2" || _doc.Accessors[uvAccessor].Count != mesh.VertexCount)
                throw new InvalidDataException("Invalid glTF texture coordinate shape.");
            uvSets.Add(_doc.ReadAccessorAsFloats(uvAccessor));
        }

        if (uvSets.Count == 0) return;

        int vertexCount		= uvSets[0].Length / 2;
        int layerCount		= uvSets.Count;
        float[] combined	= new float[vertexCount * layerCount * 2];

        for (int v = 0; v < vertexCount; v++)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            for (int layer = 0; layer < layerCount; layer++)
            {
                int dstIdx = (v * layerCount + layer) * 2;
                int srcIdx = v * 2;
                combined[dstIdx] = uvSets[layer][srcIdx];
                combined[dstIdx + 1] = uvSets[layer][srcIdx + 1];
            }
        }

        mesh.UVLayers = new DataBuffer<float>(combined, layerCount, 2);
    }

    /// <summary>
    /// Reads vertex color layers (COLOR_0, COLOR_1, ...) and combines them into a single multi-value DataBuffer.
    /// </summary>
    /// <param name="prim">The glTF mesh primitive containing color accessor references.</param>
    /// <param name="mesh">The target mesh to populate with vertex color data.</param>
    public void ReadColorLayers(GltfMeshPrimitive prim, Mesh mesh)
    {
        List<float[]> colorSets		= [];
        List<int> componentCounts	= [];
        for (int i = 0; ; i++)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (!prim.Attributes.TryGetValue($"COLOR_{i}", out int colorAccessor))
                break;
            if (_doc.Accessors[colorAccessor].Type is not ("VEC3" or "VEC4") || _doc.Accessors[colorAccessor].Count != mesh.VertexCount)
                throw new InvalidDataException("Invalid glTF color shape.");
            colorSets.Add(_doc.ReadAccessorAsFloats(colorAccessor));
            componentCounts.Add(_doc.Accessors[colorAccessor].ComponentCount);
        }

        if (colorSets.Count == 0) return;

        int vertexCount		= colorSets[0].Length / componentCounts[0];
        int layerCount		= colorSets.Count;
        float[] combined	= new float[vertexCount * layerCount * 4];

        for (int v = 0; v < vertexCount; v++)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            for (int layer = 0; layer < layerCount; layer++)
            {
                int dstIdx		= (v * layerCount + layer) * 4;
                int compCount	= componentCounts[layer];
                int srcIdx		= v * compCount;

                combined[dstIdx] = colorSets[layer][srcIdx];
                combined[dstIdx + 1] = compCount >= 2 ? colorSets[layer][srcIdx + 1] : 0f;
                combined[dstIdx + 2] = compCount >= 3 ? colorSets[layer][srcIdx + 2] : 0f;
                combined[dstIdx + 3] = compCount >= 4 ? colorSets[layer][srcIdx + 3] : 1f;
            }
        }

        mesh.ColorLayers = new DataBuffer<float>(combined, layerCount, 4);
    }

    /// <summary>
    /// Reads joint indices (JOINTS_n) and weights (WEIGHTS_n) for skinning and combines multiple sets into single DataBuffers.
    /// </summary>
    /// <param name="prim">The glTF mesh primitive containing joint and weight accessor references.</param>
    /// <returns>The combined bone index and weight buffers, or <see langword="null"/> when the primitive has no skin influences.</returns>
    public (DataBuffer BoneIndices, DataBuffer BoneWeights)? ReadSkinInfluences(GltfMeshPrimitive prim)
    {
        List<int[]> jointSets		= [];
        List<float[]> weightSets	= [];

        for (int i = 0; ; i++)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            bool hasJoints	= prim.Attributes.TryGetValue($"JOINTS_{i}", out int jAccessor);
            bool hasWeights	= prim.Attributes.TryGetValue($"WEIGHTS_{i}", out int wAccessor);

            if (!hasJoints && !hasWeights)
                break;
            if (!hasJoints || !hasWeights || _doc.Accessors[jAccessor].Type != "VEC4" || _doc.Accessors[wAccessor].Type != "VEC4" || _doc.Accessors[jAccessor].Count != _doc.Accessors[wAccessor].Count)
                throw new InvalidDataException("Inconsistent glTF skin attributes.");

            jointSets.Add(_doc.ReadAccessorAsInts(jAccessor));
            weightSets.Add(_doc.ReadAccessorAsFloats(wAccessor));
        }

        if (jointSets.Count == 0) return null;

        int vertexCount		= jointSets[0].Length / 4;
        int influenceCount	= jointSets.Count * 4;

        int[] allJoints = new int[vertexCount * influenceCount];
        for (int v = 0; v < vertexCount; v++)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            for (int set = 0; set < jointSets.Count; set++)
            {
                for (int comp = 0; comp < 4; comp++)
                {
                    int dstIdx = v * influenceCount + set * 4 + comp;
                    allJoints[dstIdx] = jointSets[set][v * 4 + comp];
                }
            }
        }

        float[] allWeights = new float[vertexCount * influenceCount];
        for (int v = 0; v < vertexCount; v++)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            for (int set = 0; set < weightSets.Count; set++)
            {
                for (int comp = 0; comp < 4; comp++)
                {
                    int dstIdx = v * influenceCount + set * 4 + comp;
                    allWeights[dstIdx] = weightSets[set][v * 4 + comp];
                }
            }
        }
        return (new DataBuffer<int>(allJoints, influenceCount, 1), new DataBuffer<float>(allWeights, influenceCount, 1));
    }

    /// <summary>
    /// Reads face indices from the specified accessor into the mesh.
    /// </summary>
    /// <param name="accessorIndex">The glTF accessor index containing the face index data.</param>
    /// <param name="mesh">The target mesh to populate with face indices.</param>
    public void ReadFaceIndices(int accessorIndex, Mesh mesh)
    {
        GltfAccessor accessor = _doc.Accessors[accessorIndex];
        if (accessor.Type != "SCALAR" || accessor.ComponentType is not (5121 or 5123 or 5125) || accessor.Normalized || accessor.Count % 3 != 0)
            throw new InvalidDataException("Invalid glTF triangle index accessor.");
        int[] indices = _doc.ReadAccessorAsInts(accessorIndex);
        mesh.FaceIndices = new DataBuffer<int>(indices, 1, 1);
    }

    /// <summary>
    /// Reads morph targets into a <see cref="Morph"/> on the mesh.
    /// </summary>
    /// <param name="gltfMesh">The glTF mesh that holds the default target weights and names.</param>
    /// <param name="targets">The list of morph target dictionaries mapping attribute names to accessor indices.</param>
    /// <param name="mesh">The target mesh to receive the morph.</param>
    public void ReadMorph(GltfMesh gltfMesh, List<Dictionary<string, int>> targets, Mesh mesh)
    {
        string[] targetNames = new string[targets.Count];

        for (int t = 0; t < targetNames.Length; t++)
            targetNames[t] = gltfMesh.TargetNames is { } names && t < names.Length ? names[t] : $"Target_{t}";

        mesh.Morph = new Morph(targetNames, ReadMorphDeltas(targets, "POSITION", mesh.VertexCount), ReadMorphDeltas(targets, "NORMAL", mesh.VertexCount), ReadMorphDeltas(targets, "TANGENT", mesh.VertexCount));
        gltfMesh.Weights?.AsSpan(0, Math.Min(gltfMesh.Weights.Length, targetNames.Length)).CopyTo(mesh.Morph.Weights);
    }

    /// <summary>
    /// Reads one delta attribute across all morph targets into a single buffer whose value index is the target index.
    /// </summary>
    /// <param name="targets">The list of morph target dictionaries mapping attribute names to accessor indices.</param>
    /// <param name="attribute">The attribute name, such as POSITION.</param>
    /// <param name="vertexCount">The number of vertices in the mesh.</param>
    /// <returns>The delta buffer, or <see langword="null"/> when no target contains the attribute.</returns>
    public DataBuffer? ReadMorphDeltas(List<Dictionary<string, int>> targets, string attribute, int vertexCount)
    {
        if (!targets.Exists(target => target.ContainsKey(attribute)))
            return null;

        int targetCount	= targets.Count;
        float[] deltas	= new float[vertexCount * targetCount * 3];

        for (int t = 0; t < targetCount; t++)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (!targets[t].TryGetValue(attribute, out int accessorIndex))
                continue;

            float[] data = _doc.ReadAccessorAsFloats(accessorIndex);

            for (int v = 0; v < vertexCount; v++)
                data.AsSpan(v * 3, 3).CopyTo(deltas.AsSpan((v * targetCount + t) * 3));
        }

        return new DataBuffer<float>(deltas, targetCount, 3);
    }

    /// <summary>
    /// Builds all <see cref="Material"/> nodes from the glTF document and adds them to the model.
    /// </summary>
    /// <param name="model">The model node to add material nodes to.</param>
    /// <returns>An array of <see cref="Material"/> instances corresponding to each glTF material.</returns>
    public Material[] BuildMaterials(MeshGroup model)
    {
        Material[] result		= new Material[_doc.Materials.Count];
        HashSet<string> names	= new(model.EnumerateChildren().Select(static node => node.Name), StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < _doc.Materials.Count; i++)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            GltfMaterial gltfMat	= _doc.Materials[i];
            string matName			= gltfMat.Name ?? $"Material_{i}";

            while (!names.Add(matName))
                matName = $"{matName}_{i}";

            Material mat = model.AddNode<Material>(matName);
            mat.DoubleSided = gltfMat.DoubleSided;

            mat.DiffuseColor = new Vector4(gltfMat.BaseColorFactor[0], gltfMat.BaseColorFactor[1], gltfMat.BaseColorFactor[2], gltfMat.BaseColorFactor[3]);

            mat.MetallicColor = new Vector4(gltfMat.MetallicFactor, gltfMat.MetallicFactor, gltfMat.MetallicFactor, 1f);
            mat.RoughnessColor = new Vector4(gltfMat.RoughnessFactor, gltfMat.RoughnessFactor, gltfMat.RoughnessFactor, 1f);

            if (gltfMat.EmissiveFactor[0] != 0f || gltfMat.EmissiveFactor[1] != 0f || gltfMat.EmissiveFactor[2] != 0f)
            {
                mat.EmissiveColor = new Vector4(gltfMat.EmissiveFactor[0], gltfMat.EmissiveFactor[1], gltfMat.EmissiveFactor[2], 1f);
            }

            ResolveTextureReference(gltfMat.BaseColorTextureIndex, mat, "diffuse", v => mat.DiffuseMapName = v);
            ResolveTextureReference(gltfMat.NormalTextureIndex, mat, "normal", v => mat.NormalMapName = v);
            ResolveTextureReference(gltfMat.MetallicRoughnessTextureIndex, mat, "metallic", v => mat.MetallicMapName = v);
            ResolveTextureReference(gltfMat.OcclusionTextureIndex, mat, "ao", v => mat.AmbientOcclusionMapName = v);
            ResolveTextureReference(gltfMat.EmissiveTextureIndex, mat, "emissive", v => mat.EmissiveMapName = v);

            result[i] = mat;
        }

        return result;
    }

    /// <summary>
    /// Resolves a glTF texture index to an image path and creates a <see cref="Texture"/> node.
    /// </summary>
    /// <param name="textureIndex">The glTF texture index to resolve.</param>
    /// <param name="mat">The material node to add the texture to.</param>
    /// <param name="slot">The texture slot name (e.g., "diffuse", "normal").</param>
    /// <param name="setMapName">A callback to set the resolved image path on the material.</param>
    public void ResolveTextureReference(int textureIndex, Material mat, string slot, Action<string> setMapName)
    {
        if (textureIndex < 0 || textureIndex >= _doc.Textures.Count)
            return;

        GltfTexture tex = _doc.Textures[textureIndex];
        if (tex.Source < 0 || tex.Source >= _doc.Images.Count)
            return;

        GltfImage image			= _doc.Images[tex.Source];
        string imagePath		= image.Uri ?? image.Name ?? $"image_{tex.Source}";
        string? resolvedPath	= FilePathResolver.ResolveReferencePath(imagePath, _baseDirectory);

        setMapName(slot);

        if (!mat.TryGetTexture(slot, out _))
        {
            var texture = new Texture(imagePath)
            {
                Name = $"{slot}_{Path.GetFileNameWithoutExtension(imagePath)}",
                ResolvedFilePath = resolvedPath,
            };
            mat.AddNode(texture);
            mat.Connect(slot, texture);
        }
    }

    /// <summary>
    /// Builds <see cref="SkeletonBone"/> hierarchies from the glTF skin definitions.
    /// </summary>
    /// <param name="scene">The scene to add bone hierarchies to.</param>
    /// <returns>An array of root <see cref="SkeletonBone"/> instances corresponding to each glTF skin.</returns>
    public SkeletonBone[] BuildSkeletons(Scene scene)
    {
        CollectJointIndices();
        SkeletonBone[] result = new SkeletonBone[_doc.Skins.Count];
        foreach (int nodeIndex in _jointIndices)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if ((uint)nodeIndex >= (uint)_doc.Nodes.Count)
                throw new InvalidDataException("Invalid glTF joint index.");
            _jointBones[nodeIndex] = new SkeletonBone(_doc.Nodes[nodeIndex].Name ?? $"Bone_{nodeIndex}");
        }

        for (int skinIndex = 0; skinIndex < _doc.Skins.Count; skinIndex++)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            GltfSkin skin = _doc.Skins[skinIndex];
            if (skin.Joints.Length == 0)
                throw new InvalidDataException("A glTF skin must contain unique joints.");
            Skeleton container = new(skin.Name ?? $"Skeleton_{skinIndex}");
            foreach (int nodeIndex in skin.Joints)
            {
                SkeletonBone bone	= _jointBones[nodeIndex];
                int? parentIndex	= FindParentJoint(nodeIndex, _jointIndices);
                Matrix4x4 matrix	= GetNodeWorldMatrix(nodeIndex);
                if (parentIndex is int parent)
                {
                    if (!Matrix4x4.Invert(GetNodeWorldMatrix(parent), out Matrix4x4 inverse))
                        throw new InvalidDataException("Singular glTF joint transform.");
                    matrix *= inverse;
                }
                bone.BindTransform = SkeletonExportTransforms.DecomposeMatrix(matrix);
                if (bone.Parent is null)
                    (parentIndex is int index ? (SceneNode)_jointBones[index] : container).AddNode(bone);
            }
            if (container.Children is { Count: > 0 })
            {
                container.Name = $"{container.Name}_{skinIndex}";
                scene.RootNode.AddNode(container);
            }
            result[skinIndex] = _jointBones[skin.Joints[0]];
        }
        return result;
    }

    /// <summary>
    /// Finds the closest ancestor of the specified node that is also a joint in the given joint set.
    /// </summary>
    /// <param name="nodeIdx">The node index to find a parent joint for.</param>
    /// <param name="joints">The array of joint node indices to search within.</param>
    /// <returns>The node index of the closest ancestor joint, or <see langword="null"/> if none is found.</returns>
    public int? FindParentJoint(int nodeIdx, int[] joints)
    {
        HashSet<int> jointSet = [.. joints];
        return FindParentJoint(nodeIdx, jointSet);
    }

    /// <summary>
    /// Finds the parent node index for the given node by searching the node children arrays.
    /// </summary>
    /// <param name="nodeIdx">The node index to find a parent for.</param>
    /// <returns>The parent node index, or <see langword="null"/> if the node is a root node.</returns>
    public int? FindParentNode(int nodeIdx)
    {
        return _parentByNode.TryGetValue(nodeIdx, out int parentIndex) ? parentIndex : null;
    }

    /// <summary>
    /// Sets up skinning on a mesh by linking it to the skeleton bones.
    /// </summary>
    /// <param name="skin">The glTF skin definition containing joint references and inverse bind matrices.</param>
    /// <param name="prim">The glTF mesh primitive containing joint and weight accessor references.</param>
    /// <param name="mesh">The mesh to apply skinning to.</param>
    public void SetupSkinning(GltfSkin skin, GltfMeshPrimitive prim, Mesh mesh)
    {
        SkeletonBone[] bones	= new SkeletonBone[skin.Joints.Length];
        Matrix4x4[] matrices	= new Matrix4x4[skin.Joints.Length];
        float[]? values			= null;
        if (skin.InverseBindMatrices >= 0)
        {
            GltfAccessor accessor = _doc.Accessors[skin.InverseBindMatrices];
            if (accessor.Type != "MAT4" || accessor.Count != bones.Length)
                throw new InvalidDataException("Invalid glTF inverse bind matrix shape.");
            values = _doc.ReadAccessorAsFloats(skin.InverseBindMatrices);
        }
        for (int i = 0; i < bones.Length; i++)
        {
            bones[i]	= _jointBones[skin.Joints[i]];
            matrices[i]	= values is null ? Matrix4x4.Identity : ReadMatrix4x4(values, i * 16);
        }
        if (ReadSkinInfluences(prim) is var (boneIndices, boneWeights))
            mesh.Skin = new Skin(bones, boneIndices, boneWeights, matrices) { Name = skin.Name };
    }

    /// <summary>
    /// Builds <see cref="SkeletonAnimation"/> nodes from the glTF animation definitions.
    /// </summary>
    /// <param name="scene">The scene to add animation nodes to.</param>
    /// <param name="skeletons">The array of root bones corresponding to each skin.</param>
    public void BuildAnimations(Scene scene, SkeletonBone[] skeletons)
    {
        for (int animIdx = 0; animIdx < _doc.Animations.Count; animIdx++)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            GltfAnimation gltfAnim	= _doc.Animations[animIdx];
            string animName			= gltfAnim.Name ?? $"Animation_{animIdx}";

            const float frameRate = 30f;

            SkeletonAnimation anim = new(animName)
            {
                Framerate = frameRate,
                TransformType = TransformType.Absolute,
                TransformSpace = TransformSpace.Local,
            };

            Dictionary<int, SkeletonAnimationTrack> tracksByNode = [];

            foreach (GltfAnimationChannel channel in gltfAnim.Channels)
            {
                if (channel.TargetNode < 0) continue;

                GltfAnimationSampler sampler = gltfAnim.Samplers[channel.Sampler];
                if (sampler.Interpolation != GltfConstants.InterpolationLinear)
                    throw new NotSupportedException($"glTF interpolation '{sampler.Interpolation}' is not supported.");

                GltfAccessor input	= _doc.Accessors[sampler.Input];
                GltfAccessor output	= _doc.Accessors[sampler.Output];
                string shape		= channel.TargetPath switch
                {
                    GltfConstants.PathTranslation or GltfConstants.PathScale => "VEC3",
                    GltfConstants.PathRotation => "VEC4",
                    _ => throw new NotSupportedException($"glTF animation path '{channel.TargetPath}' is not supported."),
                };
                if (input.Type != "SCALAR" || input.ComponentType != GltfConstants.ComponentTypeFloat || output.Type != shape || output.Count != input.Count)
                    throw new InvalidDataException("Invalid glTF animation accessor shape.");
                float[] times	= _doc.ReadAccessorAsFloats(sampler.Input);
                float[] values	= _doc.ReadAccessorAsFloats(sampler.Output);

                if (!tracksByNode.TryGetValue(channel.TargetNode, out SkeletonAnimationTrack? track))
                {
                    GltfNode targetNode	= _doc.Nodes[channel.TargetNode];
                    string trackName	= targetNode.Name ?? $"Node_{channel.TargetNode}";
                    track = new SkeletonAnimationTrack(trackName)
                    {
                        TransformSpace = TransformSpace.Local,
                        TransformType = TransformType.Absolute
                    };
                    tracksByNode[channel.TargetNode] = track;
                }

                switch (channel.TargetPath)
                {
                    case GltfConstants.PathTranslation:
                        for (int k = 0; k < times.Length; k++)
                        {
                            int offset			= k * 3;
                            Vector3 translation	= new(values[offset], values[offset + 1], values[offset + 2]);
                            track.AddTranslationFrame(times[k] * frameRate, translation);
                        }
                        break;

                    case GltfConstants.PathRotation:
                        for (int k = 0; k < times.Length; k++)
                        {
                            int offset			= k * 4;
                            Quaternion rotation	= new(values[offset], values[offset + 1], values[offset + 2], values[offset + 3]);
                            track.AddRotationFrame(times[k] * frameRate, rotation);
                        }
                        break;

                    case GltfConstants.PathScale:
                        for (int k = 0; k < times.Length; k++)
                        {
                            int offset		= k * 3;
                            Vector3 scale	= new(values[offset], values[offset + 1], values[offset + 2]);
                            track.AddScaleFrame(times[k] * frameRate, scale);
                        }
                        break;
                }
            }

            foreach (SkeletonAnimationTrack track in tracksByNode.Values)
                anim.Tracks.Add(track);

            if (anim.Tracks.Count > 0)
                scene.RootNode.AddNode(anim);
        }
    }

    /// <summary>
    /// Applies a glTF node's TRS or matrix transformation to a RedFox <see cref="Transform"/>.
    /// </summary>
    /// <param name="node">The glTF node containing TRS or matrix transform data.</param>
    /// <param name="transform">The target transform to apply the node's transformation to.</param>
    public static void ApplyNodeTransform(GltfNode node, Transform transform)
    {
        if (node.Matrix is not null && node.Matrix.Length != 16 || node.Translation is not null && node.Translation.Length != 3 || node.Rotation is not null && node.Rotation.Length != 4 || node.Scale is not null && node.Scale.Length != 3)
            throw new InvalidDataException("Invalid glTF transform shape.");
        if (node.Matrix is { Length: 16 })
        {
            SkeletonExportTransforms.DecomposeMatrix(ReadMatrix4x4(node.Matrix, 0), out Vector3 scale, out Quaternion rotation, out Vector3 position);
            transform.SetLocalPosition(position);
            transform.SetLocalRotation(rotation);
            transform.Scale = scale;
        }
        else
        {
            if (node.Translation is { Length: 3 })
                transform.SetLocalPosition(new Vector3(node.Translation[0], node.Translation[1], node.Translation[2]));
            if (node.Rotation is { Length: 4 })
                transform.SetLocalRotation(new Quaternion(node.Rotation[0], node.Rotation[1], node.Rotation[2], node.Rotation[3]));
            if (node.Scale is { Length: 3 })
                transform.Scale = new Vector3(node.Scale[0], node.Scale[1], node.Scale[2]);
        }
    }

    /// <summary>
    /// Reads a <see cref="Matrix4x4"/> from a float array in glTF column-major order.
    /// </summary>
    /// <param name="data">The float array containing matrix data.</param>
    /// <param name="offset">The starting offset in the array.</param>
    /// <returns>A <see cref="Matrix4x4"/> in row-major (.NET) format.</returns>
    public static Matrix4x4 ReadMatrix4x4(float[] data, int offset)
    {

        return new Matrix4x4(data[offset], data[offset + 1], data[offset + 2], data[offset + 3], data[offset + 4], data[offset + 5], data[offset + 6], data[offset + 7], data[offset + 8], data[offset + 9], data[offset + 10], data[offset + 11], data[offset + 12], data[offset + 13], data[offset + 14], data[offset + 15]);
    }

    /// <summary>
    /// Resolves buffer data for all buffers in the document using external URIs or data URIs.
    /// </summary>
    /// <param name="doc">The glTF document whose buffers need to be resolved.</param>
    /// <param name="baseDirectory">The base directory for resolving relative buffer URIs, or <see langword="null"/> if external resolution is not available.</param>
    public static void ResolveBuffers(GltfDocument doc, string? baseDirectory)
    {
        foreach (GltfBuffer buffer in doc.Buffers)
        {
            if (buffer.Data is not null)
                continue;

            if (buffer.Uri is null)
                continue;

            if (buffer.Uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {

                int commaIdx = buffer.Uri.IndexOf(',');
                if (commaIdx >= 0)
                {
                    string base64 = buffer.Uri[(commaIdx + 1)..];
                    buffer.Data = Convert.FromBase64String(base64);
                }
            }
            else if (baseDirectory is not null)
            {
                string fullPath = Path.Combine(baseDirectory, buffer.Uri);
                if (File.Exists(fullPath))
                    buffer.Data = File.ReadAllBytes(fullPath);
            }
        }
    }

    /// <summary>
    /// Reads exactly the specified number of bytes from a stream, throwing on premature end.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="buffer">The buffer to fill with the read bytes.</param>
    public static void ReadExact(Stream stream, Span<byte> buffer)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = stream.Read(buffer[totalRead..]);
            if (read == 0)
                throw new InvalidOperationException("Unexpected end of stream.");
            totalRead += read;
        }
    }

    /// <summary>
    /// Reads data with cancellation support.
    /// </summary>
    /// <param name="scene">The scene to read.</param>
    /// <param name="token">The cancellation token.</param>
    public void Read(Scene scene, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        _cancellationToken = token;
        MeshGroup model = scene.RootNode.AddNode<MeshGroup>(_name);

        Material[] materials = BuildMaterials(model);

        SkeletonBone[] skeletons = BuildSkeletons(scene);

        int[] rootNodes			= GetSceneRootNodes();
        int meshCounter			= 0;
        HashSet<string> names	= new(model.EnumerateChildren().Select(static node => node.Name), StringComparer.OrdinalIgnoreCase);
        foreach (int nodeIdx in rootNodes)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            ProcessNodeForMeshes(nodeIdx, model, materials, skeletons, ref meshCounter, names);
        }

        BuildAnimations(scene, skeletons);
    }

    private void ProcessNodeForMeshes(int nodeIdx, MeshGroup model, Material[] materials, SkeletonBone[] skeletons, ref int meshCounter, HashSet<string> names)
    {
        if (nodeIdx < 0 || nodeIdx >= _doc.Nodes.Count) return;

        GltfNode node = _doc.Nodes[nodeIdx];

        if (node.Mesh >= 0 && node.Mesh < _doc.Meshes.Count)
        {
            GltfMesh gltfMesh = _doc.Meshes[node.Mesh];

            GltfSkin? skin	= null;
            int skinIdx		= -1;
            if (node.Skin >= 0 && node.Skin < _doc.Skins.Count)
            {
                skinIdx	= node.Skin;
                skin	= _doc.Skins[skinIdx];
            }

            for (int primIdx = 0; primIdx < gltfMesh.Primitives.Count; primIdx++)
            {
                GltfMeshPrimitive prim = gltfMesh.Primitives[primIdx];

                if (prim.Mode != GltfConstants.ModeTriangles)
                    continue;

                string meshName = gltfMesh.Name ?? $"Mesh_{meshCounter}";
                if (gltfMesh.Primitives.Count > 1)
                    meshName = $"{meshName}_{primIdx}";

                while (!names.Add(meshName))
                    meshName = $"{meshName}_{meshCounter}";

                Mesh mesh = model.AddNode<Mesh>(meshName);
                meshCounter++;

                Transform worldTransform = SkeletonExportTransforms.DecomposeMatrix(GetNodeWorldMatrix(nodeIdx));
                mesh.BindTransform = worldTransform;

                ReadPrimitiveAttributes(prim, mesh);

                if (prim.Indices >= 0)
                    ReadFaceIndices(prim.Indices, mesh);

                if (prim.Material != -1)
                    mesh.Materials = [materials[prim.Material]];

                if (prim.Targets is { Count: > 0 })
                    ReadMorph(gltfMesh, prim.Targets, mesh);

                if (skin is not null && skinIdx >= 0 && skinIdx < skeletons.Length)
                    SetupSkinning(skin, prim, mesh);
            }
        }

        if (node.Children is not null)
        {
            foreach (int childIdx in node.Children)
                ProcessNodeForMeshes(childIdx, model, materials, skeletons, ref meshCounter, names);
        }
    }

    private Matrix4x4 GetNodeWorldMatrix(int nodeIndex)
    {
        if (_nodeWorldMatrices.TryGetValue(nodeIndex, out Matrix4x4 matrix))
            return matrix;
        Transform transform = new();
        ApplyNodeTransform(_doc.Nodes[nodeIndex], transform);
        matrix = Matrix4x4.CreateScale(transform.Scale ?? Vector3.One) * Matrix4x4.CreateFromQuaternion(transform.LocalRotation ?? Quaternion.Identity) * Matrix4x4.CreateTranslation(transform.LocalPosition ?? Vector3.Zero);
        if (_parentByNode.TryGetValue(nodeIndex, out int parent))
            matrix *= GetNodeWorldMatrix(parent);
        _nodeWorldMatrices[nodeIndex] = matrix;
        return matrix;
    }

    private int? FindParentJoint(int nodeIdx, IReadOnlySet<int> jointSet)
    {

        int? parentIdx = FindParentNode(nodeIdx);
        while (parentIdx.HasValue)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (jointSet.Contains(parentIdx.Value))
                return parentIdx.Value;
            parentIdx = FindParentNode(parentIdx.Value);
        }

        return null;
    }
}
