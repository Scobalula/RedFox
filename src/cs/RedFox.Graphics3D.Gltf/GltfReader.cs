using System.Buffers.Binary;
using System.Numerics;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.Gltf;

/// <summary>
/// Reads a glTF 2.0 asset (.gltf or .glb) and populates a <see cref="Scene"/>
/// with models, meshes, materials, skeletons, and animations.
/// <para>
/// Supports both JSON-based (.gltf) files with external binary buffers and
/// GLB binary containers. Buffer URIs may be file paths (resolved relative to
/// a base directory) or <c>data:</c> URIs with Base64 payloads.
/// </para>
/// </summary>
public sealed class GltfReader
{
    private readonly GltfDocument _doc;
    private readonly string _name;
    private readonly SceneTranslatorOptions _options;
    private readonly string? _baseDirectory;

    /// <summary>
    /// Node-index to <see cref="SkeletonBone"/> mapping, populated during skeleton construction.
    /// </summary>
    private readonly Dictionary<int, SkeletonBone> _jointBones = [];

    /// <summary>
    /// Set of all node indices that are referenced as joints by any skin.
    /// </summary>
    private readonly HashSet<int> _jointIndices = [];

    /// <summary>
    /// Initializes a new <see cref="GltfReader"/> with a pre-parsed <see cref="GltfDocument"/>.
    /// </summary>
    /// <param name="doc">The parsed glTF document with buffer data loaded.</param>
    /// <param name="name">The scene/file name used for the root model node.</param>
    /// <param name="options">Translation options.</param>
    public GltfReader(GltfDocument doc, string name, SceneTranslatorOptions options, string? baseDirectory = null)
    {
        _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _baseDirectory = baseDirectory;
    }

    /// <summary>
    /// Parses a GLB binary container from the specified stream and returns a fully
    /// loaded <see cref="GltfDocument"/> with buffer data resolved.
    /// </summary>
    /// <param name="stream">The GLB input stream.</param>
    /// <returns>A <see cref="GltfDocument"/> with all buffer data populated.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the GLB header or chunks are invalid.</exception>
    public static GltfDocument ParseGlb(Stream stream)
    {
        Span<byte> header = stackalloc byte[GltfConstants.GlbHeaderSize];
        ReadExact(stream, header);

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(header);
        if (magic != GltfConstants.GlbMagic)
            throw new InvalidOperationException($"Invalid GLB magic: 0x{magic:X8}");

        uint version = BinaryPrimitives.ReadUInt32LittleEndian(header[4..]);
        if (version != GltfConstants.GltfVersion)
            throw new InvalidOperationException($"Unsupported glTF version: {version}");

        // Parse JSON chunk
        Span<byte> chunkHeader = stackalloc byte[GltfConstants.GlbChunkHeaderSize];
        ReadExact(stream, chunkHeader);

        uint jsonLength = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader);
        uint jsonType = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader[4..]);

        if (jsonType != GltfConstants.ChunkTypeJson)
            throw new InvalidOperationException($"Expected JSON chunk, got 0x{jsonType:X8}");

        byte[] jsonBytes = new byte[jsonLength];
        ReadExact(stream, jsonBytes);

        GltfDocument doc = GltfJsonParser.Parse(jsonBytes);

        // Parse optional BIN chunk
        if (stream.Position < stream.Length)
        {
            ReadExact(stream, chunkHeader);
            uint binLength = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader);
            uint binType = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader[4..]);

            if (binType == GltfConstants.ChunkTypeBin)
            {
                byte[] binData = new byte[binLength];
                ReadExact(stream, binData);

                if (doc.Buffers.Count > 0)
                    doc.Buffers[0].Data = binData;
            }
        }

        return doc;
    }

    /// <summary>
    /// Parses a JSON-based glTF file from the specified stream and resolves external
    /// buffer URIs relative to the given base directory.
    /// </summary>
    /// <param name="stream">The JSON input stream.</param>
    /// <param name="baseDirectory">The directory to resolve relative buffer URIs against, or <see langword="null"/> if external resolution is not available.</param>
    /// <returns>A <see cref="GltfDocument"/> with all buffer data populated.</returns>
    public static GltfDocument ParseGltf(Stream stream, string? baseDirectory)
    {
        byte[] jsonBytes;
        if (stream is MemoryStream ms && ms.TryGetBuffer(out ArraySegment<byte> segment))
        {
            jsonBytes = segment.ToArray();
        }
        else
        {
            using MemoryStream copy = new();
            stream.CopyTo(copy);
            jsonBytes = copy.ToArray();
        }

        GltfDocument doc = GltfJsonParser.Parse(jsonBytes);
        ResolveBuffers(doc, baseDirectory);
        return doc;
    }

    /// <summary>
    /// Populates the specified <see cref="Scene"/> with models, meshes, materials,
    /// skeletons, and animations from the parsed glTF document.
    /// </summary>
    /// <param name="scene">The scene to populate.</param>
    public void Read(Scene scene)
    {
        Model model = scene.RootNode.AddNode<Model>(_name);

        // Collect all joint node indices
        CollectJointIndices();

        // Build materials
        Material[] materials = BuildMaterials(model);

        // Build skeletons
        SkeletonBone[] skeletons = BuildSkeletons(scene);

        // Process scene nodes to build meshes
        int[] rootNodes = GetSceneRootNodes();
        int meshCounter = 0;
        foreach (int nodeIdx in rootNodes)
        {
            ProcessNodeForMeshes(nodeIdx, model, materials, skeletons, ref meshCounter);
        }

        // Build animations
        BuildAnimations(scene, skeletons);
    }

    /// <summary>
    /// Collects all node indices referenced as joints across all skins.
    /// </summary>
    public void CollectJointIndices()
    {
        foreach (GltfSkin skin in _doc.Skins)
        {
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

        // Fallback: return all root nodes (nodes that aren't children of any other node)
        HashSet<int> childNodes = [];
        foreach (GltfNode node in _doc.Nodes)
        {
            if (node.Children is not null)
            {
                foreach (int c in node.Children)
                    childNodes.Add(c);
            }
        }

        List<int> roots = [];
        for (int i = 0; i < _doc.Nodes.Count; i++)
        {
            if (!childNodes.Contains(i))
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
    public void ProcessNodeForMeshes(int nodeIdx, Model model, Material[] materials, SkeletonBone[] skeletons, ref int meshCounter)
    {
        if (nodeIdx < 0 || nodeIdx >= _doc.Nodes.Count) return;

        GltfNode node = _doc.Nodes[nodeIdx];

        if (node.Mesh >= 0 && node.Mesh < _doc.Meshes.Count)
        {
            GltfMesh gltfMesh = _doc.Meshes[node.Mesh];

            // Find the skin for this node, if any
            GltfSkin? skin = null;
            int skinIdx = -1;
            if (node.Skin >= 0 && node.Skin < _doc.Skins.Count)
            {
                skinIdx = node.Skin;
                skin = _doc.Skins[skinIdx];
            }

            for (int primIdx = 0; primIdx < gltfMesh.Primitives.Count; primIdx++)
            {
                GltfMeshPrimitive prim = gltfMesh.Primitives[primIdx];

                // Only support triangles
                if (prim.Mode != GltfConstants.ModeTriangles)
                    continue;

                string meshName = gltfMesh.Name ?? $"Mesh_{meshCounter}";
                if (gltfMesh.Primitives.Count > 1)
                    meshName = $"{meshName}_{primIdx}";

                // Ensure unique name
                while (model.Children?.Any(c => c.Name.Equals(meshName, StringComparison.OrdinalIgnoreCase)) == true)
                    meshName = $"{meshName}_{meshCounter}";

                Mesh mesh = model.AddNode<Mesh>(meshName);
                meshCounter++;

                // Set transform from node TRS
                ApplyNodeTransform(node, mesh.BindTransform);

                // Read vertex attributes
                ReadPrimitiveAttributes(prim, mesh);

                // Read face indices
                if (prim.Indices >= 0)
                    ReadFaceIndices(prim.Indices, mesh);

                // Assign material
                if (prim.Material >= 0 && prim.Material < materials.Length)
                    mesh.Materials = [materials[prim.Material]];

                // Read morph targets
                if (prim.Targets is { Count: > 0 })
                    ReadMorphTargets(prim.Targets, mesh);

                // Set up skinning
                if (skin is not null && skinIdx >= 0 && skinIdx < skeletons.Length)
                    SetupSkinning(skin, skeletons[skinIdx], mesh);
            }
        }

        // Process children
        if (node.Children is not null)
        {
            foreach (int childIdx in node.Children)
                ProcessNodeForMeshes(childIdx, model, materials, skeletons, ref meshCounter);
        }
    }

    /// <summary>
    /// Reads vertex attributes from a glTF mesh primitive into the RedFox mesh.
    /// </summary>
    /// <param name="prim">The glTF mesh primitive containing vertex attribute accessors.</param>
    /// <param name="mesh">The target mesh to populate with vertex data.</param>
    public void ReadPrimitiveAttributes(GltfMeshPrimitive prim, Mesh mesh)
    {
        // Positions
        if (prim.Attributes.TryGetValue("POSITION", out int posAccessor))
        {
            float[] positions = _doc.ReadAccessorAsFloats(posAccessor);
            mesh.Positions = new DataBuffer<float>(positions, 1, 3);
        }

        // Normals
        if (prim.Attributes.TryGetValue("NORMAL", out int normAccessor))
        {
            float[] normals = _doc.ReadAccessorAsFloats(normAccessor);
            mesh.Normals = new DataBuffer<float>(normals, 1, 3);
        }

        // Tangents (VEC4: XYZ = tangent direction, W = bitangent sign)
        if (prim.Attributes.TryGetValue("TANGENT", out int tanAccessor))
        {
            float[] tangents = _doc.ReadAccessorAsFloats(tanAccessor);
            mesh.Tangents = new DataBuffer<float>(tangents, 1, 4);
        }

        // UV Layers
        ReadUVLayers(prim, mesh);

        // Color Layers
        ReadColorLayers(prim, mesh);

        // Joint indices and weights (skinning)
        ReadSkinInfluences(prim, mesh);
    }

    /// <summary>
    /// Reads UV texture coordinate layers (TEXCOORD_0, TEXCOORD_1, ...) and combines
    /// them into a single multi-value DataBuffer.
    /// </summary>
    /// <param name="prim">The glTF mesh primitive containing UV accessor references.</param>
    /// <param name="mesh">The target mesh to populate with UV layer data.</param>
    public void ReadUVLayers(GltfMeshPrimitive prim, Mesh mesh)
    {
        List<float[]> uvSets = [];
        for (int i = 0; ; i++)
        {
            if (!prim.Attributes.TryGetValue($"TEXCOORD_{i}", out int uvAccessor))
                break;
            uvSets.Add(_doc.ReadAccessorAsFloats(uvAccessor));
        }

        if (uvSets.Count == 0) return;

        int vertexCount = uvSets[0].Length / 2;
        int layerCount = uvSets.Count;
        float[] combined = new float[vertexCount * layerCount * 2];

        for (int v = 0; v < vertexCount; v++)
        {
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
    /// Reads vertex color layers (COLOR_0, COLOR_1, ...) and combines them
    /// into a single multi-value DataBuffer.
    /// </summary>
    /// <param name="prim">The glTF mesh primitive containing color accessor references.</param>
    /// <param name="mesh">The target mesh to populate with vertex color data.</param>
    public void ReadColorLayers(GltfMeshPrimitive prim, Mesh mesh)
    {
        List<float[]> colorSets = [];
        List<int> componentCounts = [];
        for (int i = 0; ; i++)
        {
            if (!prim.Attributes.TryGetValue($"COLOR_{i}", out int colorAccessor))
                break;
            colorSets.Add(_doc.ReadAccessorAsFloats(colorAccessor));
            componentCounts.Add(_doc.Accessors[colorAccessor].ComponentCount);
        }

        if (colorSets.Count == 0) return;

        // Normalize all to 4 components (RGBA)
        int vertexCount = colorSets[0].Length / componentCounts[0];
        int layerCount = colorSets.Count;
        float[] combined = new float[vertexCount * layerCount * 4];

        for (int v = 0; v < vertexCount; v++)
        {
            for (int layer = 0; layer < layerCount; layer++)
            {
                int dstIdx = (v * layerCount + layer) * 4;
                int compCount = componentCounts[layer];
                int srcIdx = v * compCount;

                combined[dstIdx] = colorSets[layer][srcIdx];
                combined[dstIdx + 1] = compCount >= 2 ? colorSets[layer][srcIdx + 1] : 0f;
                combined[dstIdx + 2] = compCount >= 3 ? colorSets[layer][srcIdx + 2] : 0f;
                combined[dstIdx + 3] = compCount >= 4 ? colorSets[layer][srcIdx + 3] : 1f;
            }
        }

        mesh.ColorLayers = new DataBuffer<float>(combined, layerCount, 4);
    }

    /// <summary>
    /// Reads joint indices (JOINTS_n) and weights (WEIGHTS_n) for skinning and combines
    /// multiple sets into single DataBuffers.
    /// </summary>
    /// <param name="prim">The glTF mesh primitive containing joint and weight accessor references.</param>
    /// <param name="mesh">The target mesh to populate with skin influence data.</param>
    public void ReadSkinInfluences(GltfMeshPrimitive prim, Mesh mesh)
    {
        List<int[]> jointSets = [];
        List<float[]> weightSets = [];

        for (int i = 0; ; i++)
        {
            bool hasJoints = prim.Attributes.TryGetValue($"JOINTS_{i}", out int jAccessor);
            bool hasWeights = prim.Attributes.TryGetValue($"WEIGHTS_{i}", out int wAccessor);

            if (!hasJoints || !hasWeights)
                break;

            jointSets.Add(_doc.ReadAccessorAsInts(jAccessor));
            weightSets.Add(_doc.ReadAccessorAsFloats(wAccessor));
        }

        if (jointSets.Count == 0) return;

        int vertexCount = jointSets[0].Length / 4;
        int influenceCount = jointSets.Count * 4;

        // Bone indices
        int[] allJoints = new int[vertexCount * influenceCount];
        for (int v = 0; v < vertexCount; v++)
        {
            for (int set = 0; set < jointSets.Count; set++)
            {
                for (int comp = 0; comp < 4; comp++)
                {
                    int dstIdx = v * influenceCount + set * 4 + comp;
                    allJoints[dstIdx] = jointSets[set][v * 4 + comp];
                }
            }
        }
        mesh.BoneIndices = new DataBuffer<int>(allJoints, influenceCount, 1);

        // Bone weights
        float[] allWeights = new float[vertexCount * influenceCount];
        for (int v = 0; v < vertexCount; v++)
        {
            for (int set = 0; set < weightSets.Count; set++)
            {
                for (int comp = 0; comp < 4; comp++)
                {
                    int dstIdx = v * influenceCount + set * 4 + comp;
                    allWeights[dstIdx] = weightSets[set][v * 4 + comp];
                }
            }
        }
        mesh.BoneWeights = new DataBuffer<float>(allWeights, influenceCount, 1);
    }

    /// <summary>
    /// Reads face indices from the specified accessor into the mesh.
    /// </summary>
    /// <param name="accessorIndex">The glTF accessor index containing the face index data.</param>
    /// <param name="mesh">The target mesh to populate with face indices.</param>
    public void ReadFaceIndices(int accessorIndex, Mesh mesh)
    {
        int[] indices = _doc.ReadAccessorAsInts(accessorIndex);
        mesh.FaceIndices = new DataBuffer<int>(indices, 1, 1);
    }

    /// <summary>
    /// Reads morph targets from a list of target attribute dictionaries.
    /// Each target may contain POSITION, NORMAL, and/or TANGENT deltas.
    /// </summary>
    /// <param name="targets">The list of morph target dictionaries mapping attribute names to accessor indices.</param>
    /// <param name="mesh">The target mesh to populate with morph target delta data.</param>
    public void ReadMorphTargets(List<Dictionary<string, int>> targets, Mesh mesh)
    {
        int targetCount = targets.Count;
        if (targetCount == 0) return;

        int vertexCount = mesh.VertexCount;

        // Collect delta positions across all targets
        bool hasPositions = false;
        bool hasNormals = false;
        bool hasTangents = false;

        foreach (Dictionary<string, int> target in targets)
        {
            if (target.ContainsKey("POSITION")) hasPositions = true;
            if (target.ContainsKey("NORMAL")) hasNormals = true;
            if (target.ContainsKey("TANGENT")) hasTangents = true;
        }

        if (hasPositions)
        {
            float[] deltaPos = new float[vertexCount * targetCount * 3];
            for (int t = 0; t < targetCount; t++)
            {
                if (targets[t].TryGetValue("POSITION", out int accIdx))
                {
                    float[] data = _doc.ReadAccessorAsFloats(accIdx);
                    for (int v = 0; v < vertexCount; v++)
                    {
                        int dstBase = (v * targetCount + t) * 3;
                        int srcBase = v * 3;
                        deltaPos[dstBase] = data[srcBase];
                        deltaPos[dstBase + 1] = data[srcBase + 1];
                        deltaPos[dstBase + 2] = data[srcBase + 2];
                    }
                }
            }
            mesh.DeltaPositions = new DataBuffer<float>(deltaPos, targetCount, 3);
        }

        if (hasNormals)
        {
            float[] deltaNorm = new float[vertexCount * targetCount * 3];
            for (int t = 0; t < targetCount; t++)
            {
                if (targets[t].TryGetValue("NORMAL", out int accIdx))
                {
                    float[] data = _doc.ReadAccessorAsFloats(accIdx);
                    for (int v = 0; v < vertexCount; v++)
                    {
                        int dstBase = (v * targetCount + t) * 3;
                        int srcBase = v * 3;
                        deltaNorm[dstBase] = data[srcBase];
                        deltaNorm[dstBase + 1] = data[srcBase + 1];
                        deltaNorm[dstBase + 2] = data[srcBase + 2];
                    }
                }
            }
            mesh.DeltaNormals = new DataBuffer<float>(deltaNorm, targetCount, 3);
        }

        if (hasTangents)
        {
            float[] deltaTan = new float[vertexCount * targetCount * 3];
            for (int t = 0; t < targetCount; t++)
            {
                if (targets[t].TryGetValue("TANGENT", out int accIdx))
                {
                    float[] data = _doc.ReadAccessorAsFloats(accIdx);
                    for (int v = 0; v < vertexCount; v++)
                    {
                        int dstBase = (v * targetCount + t) * 3;
                        int srcBase = v * 3;
                        deltaTan[dstBase] = data[srcBase];
                        deltaTan[dstBase + 1] = data[srcBase + 1];
                        deltaTan[dstBase + 2] = data[srcBase + 2];
                    }
                }
            }
            mesh.DeltaTangents = new DataBuffer<float>(deltaTan, targetCount, 3);
        }
    }

    /// <summary>
    /// Builds all <see cref="Material"/> nodes from the glTF document and adds them to the model.
    /// </summary>
    /// <param name="model">The model node to add material nodes to.</param>
    /// <returns>An array of <see cref="Material"/> instances corresponding to each glTF material.</returns>
    public Material[] BuildMaterials(Model model)
    {
        Material[] result = new Material[_doc.Materials.Count];

        for (int i = 0; i < _doc.Materials.Count; i++)
        {
            GltfMaterial gltfMat = _doc.Materials[i];
            string matName = gltfMat.Name ?? $"Material_{i}";

            // Ensure unique name
            while (model.Children?.Any(c => c.Name.Equals(matName, StringComparison.OrdinalIgnoreCase)) == true)
                matName = $"{matName}_{i}";

            Material mat = model.AddNode<Material>(matName);
            mat.DoubleSided = gltfMat.DoubleSided;

            // Base color
            mat.DiffuseColor = new Vector4(
                gltfMat.BaseColorFactor[0],
                gltfMat.BaseColorFactor[1],
                gltfMat.BaseColorFactor[2],
                gltfMat.BaseColorFactor[3]);

            // Metallic/Roughness
            mat.MetallicColor = new Vector4(gltfMat.MetallicFactor, gltfMat.MetallicFactor, gltfMat.MetallicFactor, 1f);
            mat.RoughnessColor = new Vector4(gltfMat.RoughnessFactor, gltfMat.RoughnessFactor, gltfMat.RoughnessFactor, 1f);

            // Emissive
            if (gltfMat.EmissiveFactor[0] != 0f || gltfMat.EmissiveFactor[1] != 0f || gltfMat.EmissiveFactor[2] != 0f)
            {
                mat.EmissiveColor = new Vector4(
                    gltfMat.EmissiveFactor[0],
                    gltfMat.EmissiveFactor[1],
                    gltfMat.EmissiveFactor[2],
                    1f);
            }

            // Texture references
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

        GltfImage image = _doc.Images[tex.Source];
        string imagePath = image.Uri ?? image.Name ?? $"image_{tex.Source}";
        string? resolvedPath = ResolveTexturePath(imagePath);

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

    private string? ResolveTexturePath(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || imagePath.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return null;

        if (Path.IsPathRooted(imagePath))
            return Path.GetFullPath(imagePath);

        if (!string.IsNullOrWhiteSpace(_baseDirectory))
            return Path.GetFullPath(Path.Combine(_baseDirectory, imagePath));

        return null;
    }

    /// <summary>
    /// Builds <see cref="SkeletonBone"/> hierarchies from the glTF skin definitions.
    /// </summary>
    /// <param name="scene">The scene to add bone hierarchies to.</param>
    /// <returns>An array of root <see cref="SkeletonBone"/> instances corresponding to each glTF skin.</returns>
    public SkeletonBone[] BuildSkeletons(Scene scene)
    {
        SkeletonBone[] result = new SkeletonBone[_doc.Skins.Count];

        for (int skinIdx = 0; skinIdx < _doc.Skins.Count; skinIdx++)
        {
            GltfSkin skin = _doc.Skins[skinIdx];
            string skelName = skin.Name ?? $"Skeleton_{skinIdx}";

            SkeletonBone rootBone = null;

            // Read inverse bind matrices
            Matrix4x4[]? ibms = null;
            if (skin.InverseBindMatrices >= 0)
            {
                float[] ibmData = _doc.ReadAccessorAsFloats(skin.InverseBindMatrices);
                ibms = new Matrix4x4[skin.Joints.Length];
                for (int j = 0; j < skin.Joints.Length; j++)
                {
                    int offset = j * 16;
                    ibms[j] = ReadMatrix4x4(ibmData, offset);
                }
            }

            // Create bones for each joint
            // First pass: create all bones
            Dictionary<int, SkeletonBone> bonesByNodeIdx = [];
            for (int j = 0; j < skin.Joints.Length; j++)
            {
                int nodeIdx = skin.Joints[j];
                GltfNode jointNode = _doc.Nodes[nodeIdx];
                string boneName = jointNode.Name ?? $"Bone_{j}";

                SkeletonBone bone = new(boneName);
                ApplyNodeTransform(jointNode, bone.BindTransform);

                bonesByNodeIdx[nodeIdx] = bone;
                _jointBones[nodeIdx] = bone;
            }

            // Second pass: build parent-child hierarchy
            foreach (int nodeIdx in skin.Joints)
            {
                GltfNode jointNode = _doc.Nodes[nodeIdx];
                SkeletonBone bone = bonesByNodeIdx[nodeIdx];

                // Find parent in the joint set
                int? parentNodeIdx = FindParentJoint(nodeIdx, skin.Joints);

                if (parentNodeIdx.HasValue && bonesByNodeIdx.TryGetValue(parentNodeIdx.Value, out SkeletonBone? parentBone))
                {
                    parentBone.AddNode(bone);
                }
                else
                {
                    // Root bone
                    if (rootBone == null)
                        rootBone = bone;
                    scene.RootNode.AddNode(bone);
                }
            }

            result[skinIdx] = rootBone;
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

        // Walk up the glTF node tree to find a parent that's also a joint
        int? parentIdx = FindParentNode(nodeIdx);
        while (parentIdx.HasValue)
        {
            if (jointSet.Contains(parentIdx.Value))
                return parentIdx.Value;
            parentIdx = FindParentNode(parentIdx.Value);
        }

        return null;
    }

    /// <summary>
    /// Finds the parent node index for the given node by searching the node children arrays.
    /// </summary>
    /// <param name="nodeIdx">The node index to find a parent for.</param>
    /// <returns>The parent node index, or <see langword="null"/> if the node is a root node.</returns>
    public int? FindParentNode(int nodeIdx)
    {
        for (int i = 0; i < _doc.Nodes.Count; i++)
        {
            int[]? children = _doc.Nodes[i].Children;
            if (children is not null && Array.IndexOf(children, nodeIdx) >= 0)
                return i;
        }
        return null;
    }

    /// <summary>
    /// Sets up skinning on a mesh by linking it to the skeleton bones.
    /// </summary>
    /// <param name="skin">The glTF skin definition containing joint references and inverse bind matrices.</param>
    /// <param name="skeleton">The skeleton to bind the mesh to.</param>
    /// <param name="mesh">The mesh to apply skinning to.</param>
    public void SetupSkinning(GltfSkin skin, SkeletonBone skeleton, Mesh mesh)
    {
        List<SkeletonBone> bones = [];
        List<Matrix4x4> ibmList = [];

        // Read inverse bind matrices
        Matrix4x4[]? ibms = null;
        if (skin.InverseBindMatrices >= 0)
        {
            float[] ibmData = _doc.ReadAccessorAsFloats(skin.InverseBindMatrices);
            ibms = new Matrix4x4[skin.Joints.Length];
            for (int j = 0; j < skin.Joints.Length; j++)
                ibms[j] = ReadMatrix4x4(ibmData, j * 16);
        }

        for (int j = 0; j < skin.Joints.Length; j++)
        {
            int nodeIdx = skin.Joints[j];
            if (_jointBones.TryGetValue(nodeIdx, out SkeletonBone? bone))
            {
                bones.Add(bone);
                ibmList.Add(ibms is not null ? ibms[j] : Matrix4x4.Identity);
            }
        }

        if (bones.Count > 0)
        {
            mesh.SetSkinBinding(bones, ibmList);
            mesh.SkinBindingName = skeleton.Name;
        }
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
            GltfAnimation gltfAnim = _doc.Animations[animIdx];
            string animName = gltfAnim.Name ?? $"Animation_{animIdx}";

            SkeletonBone? targetBone = skeletons.Length > 0 ? skeletons[0] : null;

            SkeletonAnimation anim = new(animName);
            anim.TransformType = TransformType.Absolute;
            anim.TransformSpace = TransformSpace.Local;

            // Group channels by target node
            Dictionary<int, SkeletonAnimationTrack> tracksByNode = [];

            foreach (GltfAnimationChannel channel in gltfAnim.Channels)
            {
                if (channel.TargetNode < 0) continue;

                GltfAnimationSampler sampler = gltfAnim.Samplers[channel.Sampler];

                // Read keyframe times
                float[] times = _doc.ReadAccessorAsFloats(sampler.Input);
                float[] values = _doc.ReadAccessorAsFloats(sampler.Output);

                // Get or create track for this node
                if (!tracksByNode.TryGetValue(channel.TargetNode, out SkeletonAnimationTrack? track))
                {
                    GltfNode targetNode = _doc.Nodes[channel.TargetNode];
                    string trackName = targetNode.Name ?? $"Node_{channel.TargetNode}";
                    track = new SkeletonAnimationTrack(trackName)
                    {
                        TransformSpace = TransformSpace.Local,
                        TransformType = TransformType.Absolute
                    };
                    tracksByNode[channel.TargetNode] = track;
                }

                // Handle cubic spline: take only the main value (skip in/out tangents)
                bool isCubicSpline = sampler.Interpolation == GltfConstants.InterpolationCubicSpline;

                switch (channel.TargetPath)
                {
                    case GltfConstants.PathTranslation:
                        for (int k = 0; k < times.Length; k++)
                        {
                            int offset = isCubicSpline ? (k * 9 + 3) : (k * 3);
                            Vector3 translation = new(values[offset], values[offset + 1], values[offset + 2]);
                            track.AddTranslationFrame(times[k], translation);
                        }
                        break;

                    case GltfConstants.PathRotation:
                        for (int k = 0; k < times.Length; k++)
                        {
                            int offset = isCubicSpline ? (k * 12 + 4) : (k * 4);
                            Quaternion rotation = new(values[offset], values[offset + 1], values[offset + 2], values[offset + 3]);
                            track.AddRotationFrame(times[k], rotation);
                        }
                        break;

                    case GltfConstants.PathScale:
                        for (int k = 0; k < times.Length; k++)
                        {
                            int offset = isCubicSpline ? (k * 9 + 3) : (k * 3);
                            Vector3 scale = new(values[offset], values[offset + 1], values[offset + 2]);
                            track.AddScaleFrame(times[k], scale);
                        }
                        break;
                }
            }

            // Add tracks to animation
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
        if (node.Matrix is { Length: 16 })
        {
            // Decompose column-major matrix into TRS
            Matrix4x4 mat = ReadMatrix4x4(node.Matrix, 0);
            if (Matrix4x4.Decompose(mat, out Vector3 scale, out Quaternion rotation, out Vector3 translation))
            {
                transform.SetLocalPosition(translation);
                transform.SetLocalRotation(rotation);
                transform.Scale = scale;
            }
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
        // glTF stores column-major: [col0.x, col0.y, col0.z, col0.w, col1.x, ...]
        // .NET Matrix4x4 constructor is row-major: (m11, m12, m13, m14, m21, ...)
        return new Matrix4x4(
            data[offset + 0], data[offset + 4], data[offset + 8], data[offset + 12],
            data[offset + 1], data[offset + 5], data[offset + 9], data[offset + 13],
            data[offset + 2], data[offset + 6], data[offset + 10], data[offset + 14],
            data[offset + 3], data[offset + 7], data[offset + 11], data[offset + 15]);
    }

    /// <summary>
    /// Resolves buffer data for all buffers in the document using external URIs
    /// or data URIs.
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
                // Data URI: data:application/octet-stream;base64,...
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
}
