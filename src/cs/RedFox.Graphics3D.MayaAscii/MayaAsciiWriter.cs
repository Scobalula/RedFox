using System.Globalization;
using System.Numerics;
using System.Text;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.MayaAscii;

/// <summary>
/// Writes a <see cref="Scene"/> to a Maya ASCII (.ma) file stream.
/// Supports meshes with UVs, normals, vertex colors, skinning, materials,
/// skeleton hierarchies, and skeletal animation curves. Compatible with
/// Autodesk Maya 2012 and later.
/// </summary>
public sealed class MayaAsciiWriter
{
    /// <summary>
    /// The minimum Maya version string written to the file header, ensuring compatibility with Maya 2012 and later.
    /// </summary>
    public const string MinimumMayaVersion = "2012";

    /// <summary>
    /// The Maya ASCII file format requires identifier string placed at the top of the file.
    /// </summary>
    public const string FileIdentifier = "//Maya ASCII 2012 scene";

    /// <summary>
    /// The <c>requires</c> directive version string for the core Maya plugin.
    /// </summary>
    public const string RequiresVersion = "2012";

    private readonly StreamWriter _writer;
    private readonly MayaAsciiWriteOptions _options;
    private readonly List<MayaConnection> _connections = [];
    private readonly Dictionary<SceneNode, string> _nodeNames = [];
    private readonly HashSet<string> _usedNames = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="MayaAsciiWriter"/> class that writes to the specified stream.
    /// </summary>
    /// <param name="stream">The output stream to write Maya ASCII data to. Must be writable.</param>
    /// <param name="options">The write options controlling units, axes, and feature toggles.</param>
    public MayaAsciiWriter(Stream stream, MayaAsciiWriteOptions options)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);

        _writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true)
        {
            NewLine = "\n"
        };
        _options = options;
    }

    /// <summary>
    /// Writes the entire scene to the output stream as a Maya ASCII file.
    /// All supported node types are exported: models, groups, skeletons, bones,
    /// meshes, cameras, lights, constraints, materials, and skeletal animations.
    /// </summary>
    /// <param name="scene">The scene to export. Must not be <see langword="null"/>.</param>
    /// <param name="name">The file or scene name used in the file header.</param>
    public void Write(Scene scene, string name)
    {
        ArgumentNullException.ThrowIfNull(scene);

        _connections.Clear();
        _nodeNames.Clear();
        _usedNames.Clear();

        WriteHeader(name);

        Model[] models = scene.GetDescendants<Model>();
        Group[] groups = scene.GetDescendants<Group>();
        Skeleton[] skeletons = scene.GetDescendants<Skeleton>().Where(s => s is not SkeletonBone).ToArray();
        SkeletonBone[] bones = scene.GetDescendants<SkeletonBone>();
        Mesh[] meshes = scene.GetDescendants<Mesh>();
        Camera[] cameras = scene.GetDescendants<Camera>();
        Light[] lights = scene.GetDescendants<Light>();
        ConstraintNode[] constraints = scene.GetDescendants<ConstraintNode>();
        SkeletonAnimation[] animations = scene.GetDescendants<SkeletonAnimation>();

        Dictionary<Material, string> materialNodeNames = [];
        Dictionary<Mesh, string> meshShapeNames = [];

        foreach (Model model in models)
        {
            WriteTransformNode(model);
        }

        foreach (Group group in groups)
        {
            WriteTransformNode(group);
        }

        foreach (Skeleton skeleton in skeletons)
        {
            WriteTransformNode(skeleton);
        }

        foreach (SkeletonBone bone in bones)
        {
            WriteJointNode(bone);
        }

        foreach (Camera camera in cameras)
        {
            WriteTransformNode(camera);
        }

        foreach (Light light in lights)
        {
            WriteTransformNode(light);
        }

        foreach (Mesh mesh in meshes)
        {
            WriteMeshNode(mesh, materialNodeNames, meshShapeNames);
        }

        foreach (ConstraintNode constraint in constraints)
        {
            WriteConstraintNode(constraint);
        }

        if (_options.WriteAnimations)
        {
            foreach (SkeletonAnimation animation in animations)
            {
                WriteSkeletonAnimation(animation);
            }
        }

        WriteConnections();

        _writer.Flush();
    }

    /// <summary>
    /// Writes the Maya ASCII file header including the file identifier, requires directives,
    /// and currentUnit / fileInfo blocks.
    /// </summary>
    /// <param name="name">The scene or file name to embed in the fileInfo section.</param>
    public void WriteHeader(string name)
    {
        _writer.WriteLine(FileIdentifier);
        _writer.WriteLine($"requires maya \"{RequiresVersion}\";");
        _writer.WriteLine($"currentUnit -l {FormatLinearUnit(_options.LinearUnit)} -a {FormatAngularUnit(_options.AngularUnit)} -t {FormatTimeUnit(_options.TimeUnit)};");
        _writer.WriteLine($"fileInfo \"application\" \"RedFox\";");
        _writer.WriteLine($"fileInfo \"product\" \"RedFox Scene Translator\";");
        _writer.WriteLine($"fileInfo \"version\" \"{MinimumMayaVersion}\";");
        _writer.WriteLine($"fileInfo \"osv\" \"{Environment.OSVersion.Platform}\";");
        _writer.WriteLine($"fileInfo \"sceneName\" \"{EscapeMayaString(name)}\";");

        if (_options.UpAxis == MayaUpAxis.Z)
        {
            _writer.WriteLine("upAxis \"z\";");
        }
    }

    /// <summary>
    /// Writes a scene node as a Maya <c>transform</c> DAG node, preserving the parent-child
    /// hierarchy from the scene graph. Used for <see cref="Model"/>, <see cref="Group"/>,
    /// <see cref="Skeleton"/> (non-bone), <see cref="Camera"/>, and <see cref="Light"/> nodes.
    /// </summary>
    /// <param name="node">The scene node to write as a transform.</param>
    public void WriteTransformNode(SceneNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        string nodeName = RegisterDagNodeName(node);
        string? parentName = GetParentDagName(node);
        WriteCreateNode(MayaNodeTypes.Transform, nodeName, parentName);
        WriteTransformAttributes(node);
    }

    /// <summary>
    /// Writes translation, rotation, and scale <c>setAttr</c> commands for a transform node.
    /// Only non-identity values are emitted to keep the output concise.
    /// </summary>
    /// <param name="node">The scene node whose bind-pose local transform to write.</param>
    public void WriteTransformAttributes(SceneNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        Vector3 translation = node.GetBindLocalPosition();
        if (translation != Vector3.Zero)
        {
            _writer.WriteLine($"setAttr \".t\" -type \"double3\" {FormatFloat(translation.X)} {FormatFloat(translation.Y)} {FormatFloat(translation.Z)};");
        }

        Quaternion rotation = Quaternion.Normalize(node.GetBindLocalRotation());
        if (rotation != Quaternion.Identity)
        {
            Vector3 euler = QuaternionToEulerDegrees(rotation);
            _writer.WriteLine($"setAttr \".r\" -type \"double3\" {FormatFloat(euler.X)} {FormatFloat(euler.Y)} {FormatFloat(euler.Z)};");
        }

        Vector3 scale = node.GetBindLocalScale();
        if (scale != Vector3.One)
        {
            _writer.WriteLine($"setAttr \".s\" -type \"double3\" {FormatFloat(scale.X)} {FormatFloat(scale.Y)} {FormatFloat(scale.Z)};");
        }
    }

    /// <summary>
    /// Writes a <see cref="SkeletonBone"/> as a Maya <c>joint</c> node with proper
    /// joint orient, translation, and scale attributes for the bind pose.
    /// </summary>
    /// <param name="bone">The skeleton bone to write as a joint.</param>
    public void WriteJointNode(SkeletonBone bone)
    {
        ArgumentNullException.ThrowIfNull(bone);

        string nodeName = RegisterDagNodeName(bone);
        string? parentName = GetParentDagName(bone);
        WriteCreateNode(MayaNodeTypes.Joint, nodeName, parentName);
        WriteJointAttributes(bone);
    }

    /// <summary>
    /// Writes a single mesh as a Maya transform + mesh shape node pair, including vertex positions,
    /// face topology with edge definitions, UV sets, normals, vertex colors, and optional skin cluster deformer.
    /// When skinning is enabled, an intermediate "Orig" shape holds the base geometry while the visible
    /// shape receives deformed output from the skin cluster.
    /// </summary>
    /// <param name="mesh">The mesh to export.</param>
    /// <param name="materialNodeNames">A dictionary that receives the mapping from <see cref="Material"/> to Maya shader node name.</param>
    /// <param name="meshShapeNames">A dictionary that receives the mapping from <see cref="Mesh"/> to Maya shape node name.</param>
    public void WriteMeshNode(Mesh mesh, Dictionary<Material, string> materialNodeNames, Dictionary<Mesh, string> meshShapeNames)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        string meshTransformName = RegisterDagNodeName(mesh);
        string meshShapeName = RegisterName(mesh.Name + "Shape");
        meshShapeNames[mesh] = meshShapeName;

        bool hasSkinning = mesh.HasSkinning && mesh.SkinnedBones is not null;

        string? parentName = GetParentDagName(mesh);
        WriteCreateNode(MayaNodeTypes.Transform, meshTransformName, parentName);

        WriteTransformAttributes(mesh);

        if (hasSkinning)
        {
            // Visible shape: receives deformed mesh from skin cluster
            WriteCreateNode(MayaNodeTypes.Mesh, meshShapeName, meshTransformName);
            _writer.WriteLine("setAttr -k off \".v\";");
            _writer.WriteLine("setAttr -s 2 \".iog[0].og\";");
            _writer.WriteLine("setAttr \".vir\" yes;");
            _writer.WriteLine("setAttr \".vif\" yes;");
            if (mesh.UVLayers is not null)
            {
                _writer.WriteLine("setAttr \".uvst[0].uvsn\" -type \"string\" \"map1\";");
                _writer.WriteLine("setAttr \".cuvs\" -type \"string\" \"map1\";");
            }
            _writer.WriteLine("setAttr \".dcc\" -type \"string\" \"Ambient+Diffuse\";");
            _writer.WriteLine("setAttr \".covm[0]\"  0 1 1;");
            _writer.WriteLine("setAttr \".cdvm[0]\"  0 1 1;");

            // Orig shape: intermediate object holding base geometry.
            // MUST use raw (rest-pose) positions — the skinCluster deforms at load time.
            string meshShapeOrigName = RegisterName(mesh.Name + "ShapeOrig");
            WriteCreateNode(MayaNodeTypes.Mesh, meshShapeOrigName, meshTransformName);
            _writer.WriteLine("setAttr \".io\" yes;");
            WriteMeshGeometry(mesh, forceRawPositions: true);
            WriteSkinCluster(mesh, meshShapeName, meshShapeOrigName, meshTransformName);
        }
        else
        {
            WriteCreateNode(MayaNodeTypes.Mesh, meshShapeName, meshTransformName);
            WriteMeshGeometry(mesh);
        }

        if (_options.WriteMaterials && mesh.Materials is not null)
        {
            foreach (Material material in mesh.Materials)
            {
                if (!materialNodeNames.ContainsKey(material))
                {
                    WriteMaterial(material, materialNodeNames);
                }

                string sgName = materialNodeNames[material] + "SG";
                _connections.Add(new MayaConnection(meshShapeName + ".iog", sgName + ".dsm", true));
            }
        }
    }

    /// <summary>
    /// Writes the core polygon geometry data for a mesh, including vertex positions, edge definitions,
    /// and face topology using Maya's edge-based <c>polyFaces</c> format. Also sets required mesh shape
    /// attributes for Maya to correctly display the geometry.
    /// </summary>
    /// <param name="mesh">The mesh whose geometry data to write.</param>
    public void WriteMeshGeometry(Mesh mesh, bool forceRawPositions = false)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        if (mesh.Positions is null)
        {
            return;
        }

        int vertexCount = mesh.VertexCount;
        int faceCount = mesh.FaceCount;
        bool hasColors = _options.WriteVertexColors && mesh.ColorLayers is not null && mesh.FaceIndices is not null;

        _writer.WriteLine("setAttr -k off \".v\";");
        _writer.WriteLine("setAttr \".vir\" yes;");
        _writer.WriteLine("setAttr \".vif\" yes;");

        if (mesh.FaceIndices is not null)
        {
            _writer.WriteLine($"setAttr \".iog[0].og[0].gcl\" -type \"componentList\" 1 \"f[0:{faceCount - 1}]\";");
        }

        WriteMeshUVSets(mesh);

        if (hasColors)
        {
            WriteMeshColorSets(mesh);
        }

        _writer.WriteLine("setAttr \".covm[0]\"  0 1 1;");
        _writer.WriteLine("setAttr \".cdvm[0]\"  0 1 1;");

        _writer.WriteLine($"setAttr -s {vertexCount} \".vt\";");
        _writer.Write($"setAttr \".vt[0:{vertexCount - 1}]\"");
        bool useRaw = forceRawPositions || _options.WriteRawVertices;
        for (int i = 0; i < vertexCount; i++)
        {
            Vector3 position = mesh.GetVertexPosition(i, useRaw);
            _writer.Write($" {FormatFloat(position.X)} {FormatFloat(position.Y)} {FormatFloat(position.Z)}");
        }
        _writer.WriteLine(";");

        if (mesh.FaceIndices is not null)
        {
            int indexCount = mesh.IndexCount;
            int edgeCount = faceCount * 3;

            _writer.WriteLine($"setAttr -s {edgeCount} \".ed\";");
            _writer.Write($"setAttr \".ed[0:{edgeCount - 1}]\"");
            for (int f = 0; f < faceCount; f++)
            {
                int baseIndex = f * 3;
                int i0 = mesh.FaceIndices.Get<int>(baseIndex, 0, 0);
                int i1 = mesh.FaceIndices.Get<int>(baseIndex + 1, 0, 0);
                int i2 = mesh.FaceIndices.Get<int>(baseIndex + 2, 0, 0);

                _writer.Write($" {i0} {i1} 0 {i1} {i2} 0 {i2} {i0} 0");
            }
            _writer.WriteLine(";");

            if (_options.WriteNormals && mesh.Normals is not null)
            {
                WriteMeshNormals(mesh, null);
            }

            _writer.Write($"setAttr -s {faceCount} \".fc[0:{faceCount - 1}]\" -type \"polyFaces\"");
            for (int f = 0; f < faceCount; f++)
            {
                int baseIndex = f * 3;
                int i0 = mesh.FaceIndices.Get<int>(baseIndex, 0, 0);
                int i1 = mesh.FaceIndices.Get<int>(baseIndex + 1, 0, 0);
                int i2 = mesh.FaceIndices.Get<int>(baseIndex + 2, 0, 0);

                int edgeBase = f * 3;
                _writer.Write($" f 3 {edgeBase} {edgeBase + 1} {edgeBase + 2}");

                if (mesh.UVLayers is not null)
                {
                    for (int uvLayer = 0; uvLayer < mesh.UVLayerCount; uvLayer++)
                    {
                        _writer.Write($" mu {uvLayer} 3 {i0} {i1} {i2}");
                    }
                }

                if (hasColors)
                {
                    int mcBase = f * 3;
                    for (int clLayer = 0; clLayer < mesh.ColorLayerCount; clLayer++)
                    {
                        _writer.Write($" mc {clLayer} 3 {mcBase} {mcBase + 1} {mcBase + 2}");
                    }
                }
            }
            _writer.WriteLine(";");
        }

        _writer.WriteLine("setAttr \".cd\" -type \"dataPolyComponent\" Index_Data Edge 0 ;");
        _writer.WriteLine("setAttr \".cvd\" -type \"dataPolyComponent\" Index_Data Vertex 0 ;");
        _writer.WriteLine("setAttr \".hfd\" -type \"dataPolyComponent\" Index_Data Face 0 ;");
    }

    /// <summary>
    /// Writes UV set data for a mesh as <c>setAttr</c> commands on the mesh shape's <c>.uvst</c> attribute.
    /// Each UV layer is written as a separate UV set with unique name.
    /// </summary>
    /// <param name="mesh">The mesh whose UV layers to export.</param>
    public void WriteMeshUVSets(Mesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        if (mesh.UVLayers is null)
        {
            return;
        }

        int vertexCount = mesh.VertexCount;

        for (int layerIndex = 0; layerIndex < mesh.UVLayerCount; layerIndex++)
        {
            string uvSetName = layerIndex == 0 ? "map1" : $"uvSet{layerIndex}";
            _writer.WriteLine($"setAttr \".uvst[{layerIndex}].uvsn\" -type \"string\" \"{uvSetName}\";");

            _writer.WriteLine($"setAttr -s {vertexCount} \".uvst[{layerIndex}].uvsp\";");
            _writer.Write($"setAttr \".uvst[{layerIndex}].uvsp[0:{vertexCount - 1}]\" -type \"float2\"");
            for (int i = 0; i < vertexCount; i++)
            {
                Vector2 uv = mesh.UVLayers.GetVector2(i, layerIndex);
                _writer.Write($" {FormatFloat(uv.X)} {FormatFloat(uv.Y)}");
            }
            _writer.WriteLine(";");
        }

        _writer.WriteLine("setAttr \".cuvs\" -type \"string\" \"map1\";");
    }

    /// <summary>
    /// Writes per-vertex or per-face-vertex normal data for a mesh.
    /// Normals are written as <c>setAttr</c> commands on the mesh shape's <c>.n</c> attribute
    /// using the <c>polyNormal</c> representation.
    /// </summary>
    /// <param name="mesh">The mesh whose normal data to export.</param>
    /// <param name="bakeTransform">Optional uniform transform for baking normals on displaced meshes.</param>
    public void WriteMeshNormals(Mesh mesh, Matrix4x4? bakeTransform)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        if (mesh.Normals is null)
        {
            return;
        }

        if (mesh.FaceIndices is null)
        {
            return;
        }

        int indexCount = mesh.IndexCount;

        _writer.WriteLine($"setAttr -s {indexCount} \".n\";");
        _writer.Write($"setAttr \".n[0:{indexCount - 1}]\" -type \"float3\"");
        for (int i = 0; i < indexCount; i++)
        {
            int vertexIndex = mesh.FaceIndices.Get<int>(i, 0, 0);
            Vector3 normal = mesh.Normals.GetVector3(vertexIndex, 0);
            if (bakeTransform.HasValue)
            {
                normal = Vector3.Normalize(Vector3.TransformNormal(normal, bakeTransform.Value));
            }
            _writer.Write($" {FormatFloat(normal.X)} {FormatFloat(normal.Y)} {FormatFloat(normal.Z)}");
        }
        _writer.WriteLine(";");
    }

    /// <summary>
    /// Writes vertex color set data for a mesh as <c>setAttr</c> commands on the mesh shape's
    /// <c>.clst</c> attribute. Each color layer is written as a separate color set.
    /// </summary>
    /// <param name="mesh">The mesh whose vertex color layers to export.</param>
    public void WriteMeshColorSets(Mesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        if (mesh.ColorLayers is null || mesh.FaceIndices is null)
        {
            return;
        }

        int indexCount = mesh.IndexCount;

        _writer.WriteLine("setAttr \".dcol\" yes;");
        _writer.WriteLine("setAttr \".dcc\" -type \"string\" \"Ambient+Diffuse\";");

        for (int layerIndex = 0; layerIndex < mesh.ColorLayerCount; layerIndex++)
        {
            string colorSetName = layerIndex == 0 ? "colorSet1" : $"colorSet{layerIndex + 1}";

            if (layerIndex == 0)
            {
                _writer.WriteLine($"setAttr \".ccls\" -type \"string\" \"{colorSetName}\";");
            }

            _writer.WriteLine($"setAttr \".clst[{layerIndex}].clsn\" -type \"string\" \"{colorSetName}\";");

            int componentCount = mesh.ColorLayers.ComponentCount;
            _writer.WriteLine($"setAttr -s {indexCount} \".clst[{layerIndex}].clsp\";");
            _writer.Write($"setAttr \".clst[{layerIndex}].clsp[0:{indexCount - 1}]\"");
            for (int i = 0; i < indexCount; i++)
            {
                int vertexIndex = mesh.FaceIndices.Get<int>(i, 0, 0);
                if (componentCount >= 4)
                {
                    Vector4 color = mesh.ColorLayers.GetVector4(vertexIndex, layerIndex);
                    _writer.Write($" {FormatFloat(color.X)} {FormatFloat(color.Y)} {FormatFloat(color.Z)} {FormatFloat(color.W)}");
                }
                else if (componentCount >= 3)
                {
                    Vector3 color = mesh.ColorLayers.GetVector3(vertexIndex, layerIndex);
                    _writer.Write($" {FormatFloat(color.X)} {FormatFloat(color.Y)} {FormatFloat(color.Z)} 1");
                }
            }
            _writer.WriteLine(";");
        }
    }

    /// <summary>
    /// Writes a skin cluster deformer connecting a mesh to skeleton bones, including per-vertex
    /// bone weights, influence indices, pre-bind matrices, and the geometry matrix.
    /// Creates the full deformation pipeline: Orig shape → groupParts → tweak → groupParts → skinCluster → visible shape,
    /// with associated objectSet, groupId, and groupParts nodes matching Maya's internal pattern.
    /// </summary>
    /// <param name="mesh">The mesh containing skinning data.</param>
    /// <param name="meshShapeName">The Maya visible mesh shape node name.</param>
    /// <param name="meshShapeOrigName">The Maya intermediate (Orig) mesh shape node name holding base geometry.</param>
    /// <param name="meshTransformName">The Maya mesh transform node name.</param>
    public void WriteSkinCluster(Mesh mesh, string meshShapeName, string meshShapeOrigName, string meshTransformName)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        if (mesh.SkinnedBones is null || mesh.BoneIndices is null || mesh.BoneWeights is null)
        {
            return;
        }

        string baseName = SanitizeMayaName(mesh.Name);
        string skinClusterName = RegisterName($"skinCluster_{baseName}");
        string tweakName = RegisterName($"tweak_{baseName}");
        string skinClusterSetName = RegisterName($"{skinClusterName}Set");
        string skinClusterGroupIdName = RegisterName($"{skinClusterName}GroupId");
        string skinClusterGroupPartsName = RegisterName($"{skinClusterName}GroupParts");
        string tweakSetName = RegisterName($"{tweakName}Set");
        string tweakGroupIdName = RegisterName($"{tweakName}GroupId");
        string tweakGroupPartsName = RegisterName($"{tweakName}GroupParts");

        // Skin cluster node with weights and matrices
        WriteCreateNode(MayaNodeTypes.SkinCluster, skinClusterName, null);

        int vertexCount = mesh.VertexCount;
        int influenceCount = mesh.SkinInfluenceCount;

        _writer.WriteLine($"setAttr -s {vertexCount} \".wl\";");
        for (int v = 0; v < vertexCount; v++)
        {
            var weights = new List<(int boneIndex, float weight)>();
            for (int w = 0; w < influenceCount; w++)
            {
                int boneIdx = mesh.BoneIndices.Get<int>(v, w, 0);
                float weight = mesh.BoneWeights.Get<float>(v, w, 0);
                if (weight > 0.0f && boneIdx >= 0 && boneIdx < mesh.SkinnedBones.Count)
                {
                    weights.Add((boneIdx, weight));
                }
            }

            if (weights.Count == 1)
            {
                _writer.WriteLine($"setAttr \".wl[{v}].w[{weights[0].boneIndex}]\" {FormatFloat(weights[0].weight)};");
            }
            else if (weights.Count > 1)
            {
                _writer.WriteLine($"setAttr -s {weights.Count} \".wl[{v}].w\";");
                foreach ((int boneIndex, float weight) in weights)
                {
                    _writer.WriteLine($"setAttr \".wl[{v}].w[{boneIndex}]\" {FormatFloat(weight)};");
                }
            }
        }

        _writer.WriteLine($"setAttr -s {mesh.SkinnedBones.Count} \".pm\";");
        for (int i = 0; i < mesh.SkinnedBones.Count; i++)
        {
            Matrix4x4 boneWorldMatrix = mesh.SkinnedBones[i].GetBindWorldMatrix();
            Matrix4x4 bindMatrix = Matrix4x4.Invert(boneWorldMatrix, out Matrix4x4 inverseBoneWorld)
                ? inverseBoneWorld
                : Matrix4x4.Identity;

            WriteSetAttrMatrix($".pm[{i}]", bindMatrix);
        }

        WriteSetAttrMatrix(".gm", Matrix4x4.Identity);

        int boneCount = mesh.SkinnedBones.Count;
        _writer.WriteLine($"setAttr -s {boneCount} \".ma\";");

        _writer.Write($"setAttr -s {boneCount} \".dpf[0:{boneCount - 1}]\"");
        for (int i = 0; i < boneCount; i++)
        {
            _writer.Write(" 4");
        }
        _writer.WriteLine(";");

        _writer.WriteLine($"setAttr -s {boneCount} \".lw\";");
        _writer.WriteLine($"setAttr -s {boneCount} \".lw\";");
        _writer.WriteLine("setAttr \".mi\" 5;");
        _writer.WriteLine("setAttr \".ucm\" yes;");

        // Tweak, objectSet, groupId, groupParts nodes
        WriteCreateNode(MayaNodeTypes.Tweak, tweakName, null);

        WriteCreateNode(MayaNodeTypes.ObjectSet, skinClusterSetName, null);
        _writer.WriteLine("setAttr \".ihi\" 0;");
        _writer.WriteLine("setAttr \".vo\" yes;");

        WriteCreateNode(MayaNodeTypes.GroupId, skinClusterGroupIdName, null);
        _writer.WriteLine("setAttr \".ihi\" 0;");

        WriteCreateNode(MayaNodeTypes.GroupParts, skinClusterGroupPartsName, null);
        _writer.WriteLine("setAttr \".ihi\" 0;");
        _writer.WriteLine($"setAttr \".ic\" -type \"componentList\" 1 \"vtx[0:{vertexCount - 1}]\";");

        WriteCreateNode(MayaNodeTypes.ObjectSet, tweakSetName, null);
        _writer.WriteLine("setAttr \".ihi\" 0;");
        _writer.WriteLine("setAttr \".vo\" yes;");

        WriteCreateNode(MayaNodeTypes.GroupId, tweakGroupIdName, null);
        _writer.WriteLine("setAttr \".ihi\" 0;");

        WriteCreateNode(MayaNodeTypes.GroupParts, tweakGroupPartsName, null);
        _writer.WriteLine("setAttr \".ihi\" 0;");
        _writer.WriteLine("setAttr \".ic\" -type \"componentList\" 1 \"vtx[*]\";");

        // Pipeline: Orig → tweakGroupParts → tweak → skinClusterGroupParts → skinCluster → visibleShape
        _connections.Add(new MayaConnection(meshShapeOrigName + ".w", tweakGroupPartsName + ".ig", false));
        _connections.Add(new MayaConnection(tweakGroupIdName + ".id", tweakGroupPartsName + ".gi", false));
        _connections.Add(new MayaConnection(tweakGroupPartsName + ".og", tweakName + ".ip[0].ig", false));
        _connections.Add(new MayaConnection(tweakGroupIdName + ".id", tweakName + ".ip[0].gi", false));
        _connections.Add(new MayaConnection(tweakName + ".og[0]", skinClusterGroupPartsName + ".ig", false));
        _connections.Add(new MayaConnection(skinClusterGroupIdName + ".id", skinClusterGroupPartsName + ".gi", false));
        _connections.Add(new MayaConnection(skinClusterGroupPartsName + ".og", skinClusterName + ".ip[0].ig", false));
        _connections.Add(new MayaConnection(skinClusterGroupIdName + ".id", skinClusterName + ".ip[0].gi", false));
        _connections.Add(new MayaConnection(skinClusterName + ".og[0]", meshShapeName + ".i", false));

        // Visible shape object groups
        _connections.Add(new MayaConnection(skinClusterGroupIdName + ".id", meshShapeName + ".iog.og[0].gid", false));
        _connections.Add(new MayaConnection(skinClusterSetName + ".mwc", meshShapeName + ".iog.og[0].gco", false));
        _connections.Add(new MayaConnection(tweakGroupIdName + ".id", meshShapeName + ".iog.og[1].gid", false));
        _connections.Add(new MayaConnection(tweakSetName + ".mwc", meshShapeName + ".iog.og[1].gco", false));
        _connections.Add(new MayaConnection(tweakName + ".vl[0].vt[0]", meshShapeName + ".twl", false));

        // Object set membership
        _connections.Add(new MayaConnection(skinClusterGroupIdName + ".msg", skinClusterSetName + ".gn", true));
        _connections.Add(new MayaConnection(meshShapeName + ".iog.og[0]", skinClusterSetName + ".dsm", true));
        _connections.Add(new MayaConnection(skinClusterName + ".msg", skinClusterSetName + ".ub[0]", false));
        _connections.Add(new MayaConnection(tweakGroupIdName + ".msg", tweakSetName + ".gn", true));
        _connections.Add(new MayaConnection(meshShapeName + ".iog.og[1]", tweakSetName + ".dsm", true));
        _connections.Add(new MayaConnection(tweakName + ".msg", tweakSetName + ".ub[0]", false));

        // Joint connections
        for (int i = 0; i < mesh.SkinnedBones.Count; i++)
        {
            SkeletonBone bone = mesh.SkinnedBones[i];
            if (_nodeNames.TryGetValue(bone, out string? boneName))
            {
                _connections.Add(new MayaConnection(boneName + ".wm", skinClusterName + $".ma[{i}]", false));
                _connections.Add(new MayaConnection(boneName + ".liw", skinClusterName + $".lw[{i}]", false));
            }
        }
    }

    /// <summary>
    /// Writes a <see cref="Material"/> as a Maya Lambert or Phong shader node plus its shading engine
    /// and material info nodes.
    /// </summary>
    /// <param name="material">The material to export.</param>
    /// <param name="materialNodeNames">A dictionary that receives the mapping from <see cref="Material"/> to Maya node name.</param>
    public void WriteMaterial(Material material, Dictionary<Material, string> materialNodeNames)
    {
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(materialNodeNames);

        bool hasSpecular = material.SpecularMapName is not null || material.SpecularColor is not null || material.Shininess is not null;
        string shaderType = hasSpecular ? MayaNodeTypes.Phong : MayaNodeTypes.Lambert;
        string shaderName = RegisterName(material.Name);
        materialNodeNames[material] = shaderName;

        WriteCreateNode(shaderType, shaderName, null);

        if (material.DiffuseColor is Vector4 dc)
        {
            _writer.WriteLine($"setAttr \".c\" -type \"float3\" {FormatFloat(dc.X)} {FormatFloat(dc.Y)} {FormatFloat(dc.Z)};");
        }

        if (hasSpecular && material.SpecularColor is Vector4 sc)
        {
            _writer.WriteLine($"setAttr \".sc\" -type \"float3\" {FormatFloat(sc.X)} {FormatFloat(sc.Y)} {FormatFloat(sc.Z)};");
        }

        if (material.Shininess is float shininess)
        {
            _writer.WriteLine($"setAttr \".cp\" {FormatFloat(shininess)};");
        }

        // Shading engine
        string sgName = shaderName + "SG";
        WriteCreateNode(MayaNodeTypes.ShadingEngine, sgName, null);
        _writer.WriteLine("setAttr \".ihi\" 0;");
        _writer.WriteLine("setAttr \".ro\" yes;");

        // Material info
        string matInfoName = RegisterName($"materialInfo_{SanitizeMayaName(material.Name)}");
        WriteCreateNode(MayaNodeTypes.MaterialInfo, matInfoName, null);

        // Connect shader → SG → materialInfo
        _connections.Add(new MayaConnection(shaderName + ".outColor", sgName + ".surfaceShader", false));
        _connections.Add(new MayaConnection(sgName + ".message", matInfoName + ".shadingGroup", false));

        // Write texture file nodes for diffuse map
        if (material.DiffuseMapName is not null)
        {
            WriteFileTextureNode(material.DiffuseMapName, shaderName + ".color");
        }

        // Write texture file nodes for normal map
        if (material.NormalMapName is not null)
        {
            WriteFileTextureNode(material.NormalMapName, shaderName + ".normalCamera");
        }

        // Write texture file nodes for specular map
        if (hasSpecular && material.SpecularMapName is not null)
        {
            WriteFileTextureNode(material.SpecularMapName, shaderName + ".specularColor");
        }
    }

    /// <summary>
    /// Writes a Maya <c>file</c> texture node and its associated <c>place2dTexture</c> node,
    /// then connects the texture output to the specified shader attribute.
    /// </summary>
    /// <param name="texturePath">The file path or name of the texture image.</param>
    /// <param name="targetAttribute">The shader attribute to connect the texture output to (e.g., "lambert1.color").</param>
    public void WriteFileTextureNode(string texturePath, string targetAttribute)
    {
        string fileNodeName = RegisterName($"file_{SanitizeMayaName(Path.GetFileNameWithoutExtension(texturePath))}");
        string placeName = RegisterName($"place2dTexture_{SanitizeMayaName(Path.GetFileNameWithoutExtension(texturePath))}");

        WriteCreateNode(MayaNodeTypes.File, fileNodeName, null);
        _writer.WriteLine($"setAttr \".ftn\" -type \"string\" \"{EscapeMayaString(texturePath)}\";");

        WriteCreateNode(MayaNodeTypes.Place2dTexture, placeName, null);

        // Connect place2dTexture → file node
        _connections.Add(new MayaConnection(placeName + ".outUV", fileNodeName + ".uvCoord", false));
        _connections.Add(new MayaConnection(placeName + ".outUvFilterSize", fileNodeName + ".uvFilterSize", false));
        _connections.Add(new MayaConnection(placeName + ".coverage", fileNodeName + ".coverage", false));
        _connections.Add(new MayaConnection(placeName + ".translateFrame", fileNodeName + ".translateFrame", false));
        _connections.Add(new MayaConnection(placeName + ".rotateFrame", fileNodeName + ".rotateFrame", false));
        _connections.Add(new MayaConnection(placeName + ".mirrorU", fileNodeName + ".mirrorU", false));
        _connections.Add(new MayaConnection(placeName + ".mirrorV", fileNodeName + ".mirrorV", false));
        _connections.Add(new MayaConnection(placeName + ".stagger", fileNodeName + ".stagger", false));
        _connections.Add(new MayaConnection(placeName + ".wrapU", fileNodeName + ".wrapU", false));
        _connections.Add(new MayaConnection(placeName + ".wrapV", fileNodeName + ".wrapV", false));
        _connections.Add(new MayaConnection(placeName + ".repeatUV", fileNodeName + ".repeatUV", false));
        _connections.Add(new MayaConnection(placeName + ".vertexUvOne", fileNodeName + ".vertexUvOne", false));
        _connections.Add(new MayaConnection(placeName + ".vertexUvTwo", fileNodeName + ".vertexUvTwo", false));
        _connections.Add(new MayaConnection(placeName + ".vertexUvThree", fileNodeName + ".vertexUvThree", false));
        _connections.Add(new MayaConnection(placeName + ".vertexCameraOne", fileNodeName + ".vertexCameraOne", false));
        _connections.Add(new MayaConnection(placeName + ".offset", fileNodeName + ".offset", false));
        _connections.Add(new MayaConnection(placeName + ".rotateUV", fileNodeName + ".rotateUV", false));
        _connections.Add(new MayaConnection(placeName + ".noiseUV", fileNodeName + ".noiseUV", false));

        // Connect file node output to shader attribute
        _connections.Add(new MayaConnection(fileNodeName + ".outColor", targetAttribute, false));
    }

    /// <summary>
    /// Writes a <see cref="SkeletonAnimation"/> as a set of Maya animCurve nodes,
    /// one per channel (translateX, translateY, translateZ, rotateX, rotateY, rotateZ,
    /// scaleX, scaleY, scaleZ) per bone track.
    /// </summary>
    /// <param name="animation">The skeleton animation to export.</param>
    public void WriteSkeletonAnimation(SkeletonAnimation animation)
    {
        ArgumentNullException.ThrowIfNull(animation);

        foreach (SkeletonAnimationTrack track in animation.Tracks)
        {
            string? targetJointName = null;
            foreach ((SceneNode node, string name) in _nodeNames)
            {
                if (node is SkeletonBone bone && string.Equals(bone.Name, track.Name, StringComparison.OrdinalIgnoreCase))
                {
                    targetJointName = name;
                    break;
                }
            }

            if (targetJointName is null)
            {
                continue;
            }

            if (track.TranslationCurve is AnimationCurve translationCurve && translationCurve.KeyFrameCount > 0)
            {
                WriteAnimCurveChannels(translationCurve, targetJointName, MayaNodeTypes.AnimCurveTL, "translate", 3, animation.Framerate);
            }

            if (track.RotationCurve is AnimationCurve rotationCurve && rotationCurve.KeyFrameCount > 0)
            {
                WriteAnimCurveChannels(rotationCurve, targetJointName, MayaNodeTypes.AnimCurveTA, "rotate", rotationCurve.ComponentCount, animation.Framerate);
            }

            if (track.ScaleCurve is AnimationCurve scaleCurve && scaleCurve.KeyFrameCount > 0)
            {
                WriteAnimCurveChannels(scaleCurve, targetJointName, MayaNodeTypes.AnimCurveTU, "scale", 3, animation.Framerate);
            }
        }
    }

    /// <summary>
    /// Writes animation curve data for individual X/Y/Z (and optionally W) channels.
    /// For rotation curves with 4 components (quaternions), the values are converted to Euler angles in degrees.
    /// </summary>
    /// <param name="curve">The animation curve containing keyframe data.</param>
    /// <param name="targetJointName">The Maya joint node name to connect the curves to.</param>
    /// <param name="curveType">The Maya animCurve node type (e.g., animCurveTL, animCurveTA, animCurveTU).</param>
    /// <param name="attributeBase">The base attribute name (e.g., "translate", "rotate", "scale").</param>
    /// <param name="componentCount">Number of components in the curve values (3 for Vector3, 4 for Quaternion).</param>
    /// <param name="framerate">The animation framerate for time conversion.</param>
    public void WriteAnimCurveChannels(AnimationCurve curve, string targetJointName, string curveType, string attributeBase, int componentCount, float framerate)
    {
        string[] channelSuffixes = ["X", "Y", "Z"];
        int channelCount = Math.Min(componentCount, 3);
        bool isQuaternion = componentCount == 4;

        for (int channel = 0; channel < channelCount; channel++)
        {
            string channelAttr = attributeBase + channelSuffixes[channel];
            string curveName = RegisterName($"{SanitizeMayaName(targetJointName)}_{channelAttr}");

            WriteCreateNode(curveType, curveName, null);

            int keyCount = curve.KeyFrameCount;
            _writer.WriteLine($"setAttr -s {keyCount} \".ktv[0:{keyCount - 1}]\"");

            for (int k = 0; k < keyCount; k++)
            {
                float time = curve.Keys!.Get<float>(k, 0, 0);

                float value;
                if (isQuaternion)
                {
                    Quaternion q = new(
                        curve.Values!.Get<float>(k, 0, 0),
                        curve.Values!.Get<float>(k, 0, 1),
                        curve.Values!.Get<float>(k, 0, 2),
                        curve.Values!.Get<float>(k, 0, 3));
                    Vector3 euler = QuaternionToEulerDegrees(q);
                    value = channel switch
                    {
                        0 => euler.X,
                        1 => euler.Y,
                        _ => euler.Z,
                    };
                }
                else
                {
                    value = curve.Values!.Get<float>(k, 0, channel);
                }

                _writer.Write($" {FormatFloat(time)} {FormatFloat(value)}");
            }

            _writer.WriteLine(";");

            _connections.Add(new MayaConnection(curveName + ".output", targetJointName + "." + channelAttr, false));
        }
    }

    /// <summary>
    /// Writes all accumulated <c>connectAttr</c> commands that wire the Maya dependency graph together.
    /// This method is called at the end of the export process after all nodes have been emitted.
    /// </summary>
    public void WriteConnections()
    {
        foreach (MayaConnection connection in _connections)
        {
            if (connection.IsNextAvailable)
            {
                _writer.WriteLine($"connectAttr -na \"{EscapeMayaString(connection.Source)}\" \"{EscapeMayaString(connection.Destination)}\";");
            }
            else
            {
                _writer.WriteLine($"connectAttr \"{EscapeMayaString(connection.Source)}\" \"{EscapeMayaString(connection.Destination)}\";");
            }
        }
    }

    /// <summary>
    /// Writes a <c>createNode</c> command for a Maya dependency graph or DAG node.
    /// When a parent is specified, the <c>-p</c> flag is included to establish the DAG hierarchy.
    /// </summary>
    /// <param name="nodeType">The Maya node type (e.g., "transform", "joint", "mesh").</param>
    /// <param name="nodeName">The unique name for the new node.</param>
    /// <param name="parentName">The name of the parent DAG node, or <see langword="null"/> for root-level nodes.</param>
    public void WriteCreateNode(string nodeType, string nodeName, string? parentName)
    {
        if (parentName is not null)
        {
            _writer.WriteLine($"createNode {nodeType} -n \"{EscapeMayaString(nodeName)}\" -p \"{EscapeMayaString(parentName)}\";");
        }
        else
        {
            _writer.WriteLine($"createNode {nodeType} -n \"{EscapeMayaString(nodeName)}\";");
        }
    }

    /// <summary>
    /// Writes joint-specific <c>setAttr</c> commands for a skeleton bone node, including
    /// translation, joint orient (as Euler angles in degrees), and scale attributes.
    /// The rotation (<c>.r</c>) is left at its default zero value since the bind pose
    /// orientation is carried entirely by <c>.jo</c> (joint orient).
    /// </summary>
    /// <param name="bone">The skeleton bone to export joint attributes for.</param>
    public void WriteJointAttributes(SkeletonBone bone)
    {
        ArgumentNullException.ThrowIfNull(bone);

        _writer.WriteLine("addAttr -ci true -sn \"liw\" -ln \"lockInfluenceWeights\" -bt \"lock\" -min 0 -max 1 -at \"bool\";");
        _writer.WriteLine("setAttr \".uoc\" yes;");
        _writer.WriteLine("setAttr \".ove\" yes;");

        Vector3 translation = bone.GetBindLocalPosition();
        _writer.WriteLine($"setAttr \".t\" -type \"double3\" {FormatFloat(translation.X)} {FormatFloat(translation.Y)} {FormatFloat(translation.Z)};");

        _writer.WriteLine("setAttr \".mnrl\" -type \"double3\" -360 -360 -360;");
        _writer.WriteLine("setAttr \".mxrl\" -type \"double3\" 360 360 360;");

        Quaternion rotation = Quaternion.Normalize(bone.GetBindLocalRotation());
        Vector3 euler = QuaternionToEulerDegrees(rotation);
        _writer.WriteLine($"setAttr \".jo\" -type \"double3\" {FormatFloat(euler.X)} {FormatFloat(euler.Y)} {FormatFloat(euler.Z)};");

        Vector3 scale = bone.GetBindLocalScale();
        _writer.WriteLine($"setAttr \".scale\" -type \"double3\" {FormatFloat(scale.X)} {FormatFloat(scale.Y)} {FormatFloat(scale.Z)};");

        _writer.WriteLine("setAttr \".radi\" 0.5;");
    }

    /// <summary>
    /// Writes a <c>setAttr</c> command for a 4x4 matrix attribute value.
    /// The matrix is written in Maya's row-major attribute format as 16 space-separated doubles.
    /// </summary>
    /// <param name="attribute">The attribute name (e.g., ".pm[0]").</param>
    /// <param name="matrix">The 4x4 matrix to write.</param>
    public void WriteSetAttrMatrix(string attribute, Matrix4x4 matrix)
    {
        _writer.Write($"setAttr \"{attribute}\" -type \"matrix\"");
        _writer.Write($" {FormatFloat(matrix.M11)} {FormatFloat(matrix.M12)} {FormatFloat(matrix.M13)} {FormatFloat(matrix.M14)}");
        _writer.Write($" {FormatFloat(matrix.M21)} {FormatFloat(matrix.M22)} {FormatFloat(matrix.M23)} {FormatFloat(matrix.M24)}");
        _writer.Write($" {FormatFloat(matrix.M31)} {FormatFloat(matrix.M32)} {FormatFloat(matrix.M33)} {FormatFloat(matrix.M34)}");
        _writer.Write($" {FormatFloat(matrix.M41)} {FormatFloat(matrix.M42)} {FormatFloat(matrix.M43)} {FormatFloat(matrix.M44)}");
        _writer.WriteLine(";");
    }

    /// <summary>
    /// Registers a unique Maya node name for the given base name, tracking it in the
    /// <see cref="_usedNames"/> set. A numeric suffix is appended only when a collision occurs.
    /// </summary>
    /// <param name="baseName">The base name to sanitize and register.</param>
    /// <returns>A unique Maya-safe node name.</returns>
    public string RegisterName(string baseName)
    {
        string sanitized = SanitizeMayaName(baseName);
        if (string.IsNullOrEmpty(sanitized))
        {
            sanitized = "node";
        }

        string name = sanitized;
        if (!_usedNames.Add(name))
        {
            int counter = 1;
            do { name = $"{sanitized}_{counter++}"; } while (!_usedNames.Add(name));
        }

        return name;
    }

    /// <summary>
    /// Registers a scene node as a DAG node in the Maya name map, generating a unique name
    /// and storing the mapping for parent-child lookups via <see cref="GetParentDagName"/>.
    /// </summary>
    /// <param name="node">The scene node to register.</param>
    /// <returns>The unique Maya DAG node name.</returns>
    public string RegisterDagNodeName(SceneNode node)
    {
        string name = RegisterName(node.Name);
        _nodeNames[node] = name;
        return name;
    }

    /// <summary>
    /// Looks up the Maya DAG node name for the parent of the specified scene node.
    /// Returns <see langword="null"/> if the parent has not been registered or is the scene root.
    /// </summary>
    /// <param name="node">The scene node whose parent to look up.</param>
    /// <returns>The parent Maya node name, or <see langword="null"/> if no parent is registered.</returns>
    public string? GetParentDagName(SceneNode node)
    {
        if (node.Parent is not null && _nodeNames.TryGetValue(node.Parent, out string? parentName))
        {
            return parentName;
        }

        return null;
    }


    /// <summary>
    /// Converts a quaternion rotation to Euler angles expressed in degrees using XYZ rotation order.
    /// The conversion handles gimbal lock edge cases by clamping the pitch component.
    /// </summary>
    /// <param name="q">The quaternion to convert.</param>
    /// <returns>A <see cref="Vector3"/> containing rotation angles in degrees as (X, Y, Z).</returns>
    public static Vector3 QuaternionToEulerDegrees(Quaternion q)
    {
        q = Quaternion.Normalize(q);

        const float radToDeg = 180.0f / MathF.PI;

        // Yaw (Y-axis rotation) — check for gimbal lock first
        float sinYaw = 2.0f * (q.W * q.Y - q.Z * q.X);

        if (MathF.Abs(sinYaw) >= 0.9999f)
        {
            // Gimbal lock: X and Z axes are aligned, only their sum/difference is determined.
            // Convention: assign the combined rotation to X, set Z = 0.
            float yaw = MathF.CopySign(MathF.PI / 2.0f, sinYaw);
            float pitch = 2.0f * MathF.Atan2(q.X, q.W);
            return new Vector3(pitch * radToDeg, yaw * radToDeg, 0f);
        }

        float yawNormal = MathF.Asin(sinYaw);

        // Pitch (X-axis rotation)
        float sinPitch = 2.0f * (q.W * q.X + q.Y * q.Z);
        float cosPitch = 1.0f - 2.0f * (q.X * q.X + q.Y * q.Y);
        float pitch2 = MathF.Atan2(sinPitch, cosPitch);

        // Roll (Z-axis rotation)
        float sinRoll = 2.0f * (q.W * q.Z + q.X * q.Y);
        float cosRoll = 1.0f - 2.0f * (q.Y * q.Y + q.Z * q.Z);
        float roll = MathF.Atan2(sinRoll, cosRoll);

        return new Vector3(pitch2 * radToDeg, yawNormal * radToDeg, roll * radToDeg);
    }

    /// <summary>
    /// Sanitizes a name string for use as a Maya node identifier. Non-alphanumeric characters
    /// (except underscores and colons) are replaced with underscores, and leading digits are prefixed.
    /// Colons are preserved to support Maya namespace syntax (e.g., <c>t7::bone_name</c>).
    /// </summary>
    /// <param name="name">The raw name to sanitize.</param>
    /// <returns>A Maya-safe node name string.</returns>
    public static string SanitizeMayaName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "node";
        }

        Span<char> buffer = name.Length <= 256 ? stackalloc char[name.Length] : new char[name.Length];
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            buffer[i] = char.IsLetterOrDigit(c) || c == '_' || c == ':' ? c : '_';
        }

        // Maya names cannot start with a digit
        if (char.IsDigit(buffer[0]))
        {
            return "_" + new string(buffer);
        }

        return new string(buffer);
    }

    /// <summary>
    /// Escapes a string for safe inclusion in a Maya ASCII quoted string context.
    /// Backslashes and double-quotes are escaped with a preceding backslash.
    /// </summary>
    /// <param name="value">The string value to escape.</param>
    /// <returns>The escaped string suitable for Maya ASCII output.</returns>
    public static string EscapeMayaString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    /// <summary>
    /// Formats a floating-point value using invariant culture to ensure consistent decimal separators
    /// regardless of the host system's locale settings.
    /// </summary>
    /// <param name="value">The float value to format.</param>
    /// <returns>A string representation of the value using '.' as the decimal separator.</returns>
    public static string FormatFloat(float value) =>
        value.ToString("G9", CultureInfo.InvariantCulture);

    /// <summary>
    /// Returns the Maya ASCII linear unit keyword for the specified <see cref="MayaLinearUnit"/> value.
    /// </summary>
    /// <param name="unit">The linear unit to format.</param>
    /// <returns>The corresponding Maya unit string (e.g., "cm", "m", "in").</returns>
    public static string FormatLinearUnit(MayaLinearUnit unit) => unit switch
    {
        MayaLinearUnit.Millimeter => "mm",
        MayaLinearUnit.Centimeter => "cm",
        MayaLinearUnit.Meter => "m",
        MayaLinearUnit.Inch => "in",
        MayaLinearUnit.Foot => "ft",
        MayaLinearUnit.Yard => "yd",
        _ => "cm",
    };

    /// <summary>
    /// Returns the Maya ASCII angular unit keyword for the specified <see cref="MayaAngularUnit"/> value.
    /// </summary>
    /// <param name="unit">The angular unit to format.</param>
    /// <returns>The corresponding Maya unit string (e.g., "deg", "rad").</returns>
    public static string FormatAngularUnit(MayaAngularUnit unit) => unit switch
    {
        MayaAngularUnit.Degree => "deg",
        MayaAngularUnit.Radian => "rad",
        _ => "deg",
    };

    /// <summary>
    /// Returns the Maya ASCII time unit keyword for the specified <see cref="MayaTimeUnit"/> value.
    /// </summary>
    /// <param name="unit">The time unit to format.</param>
    /// <returns>The corresponding Maya time keyword (e.g., "film", "ntsc", "pal").</returns>
    public static string FormatTimeUnit(MayaTimeUnit unit) => unit switch
    {
        MayaTimeUnit.Film => "film",
        MayaTimeUnit.Game => "game",
        MayaTimeUnit.Ntsc => "ntsc",
        MayaTimeUnit.Pal => "pal",
        MayaTimeUnit.Show => "show",
        MayaTimeUnit.NtscField => "ntscf",
        MayaTimeUnit.PalField => "palf",
        _ => "film",
    };

    /// <summary>
    /// Writes a <see cref="ConstraintNode"/> as the appropriate Maya constraint node type.
    /// Supports <see cref="ParentConstraintNode"/> and <see cref="OrientConstraintNode"/>.
    /// The constraint is parented under its constrained node in the Maya DAG.
    /// </summary>
    /// <param name="constraint">The constraint node to export.</param>
    public void WriteConstraintNode(ConstraintNode constraint)
    {
        ArgumentNullException.ThrowIfNull(constraint);

        string constraintName = RegisterName(constraint.Name);

        string? constrainedParent = null;
        if (_nodeNames.TryGetValue(constraint.ConstrainedNode, out string? constrainedName))
        {
            constrainedParent = constrainedName;
        }

        if (constraint is ParentConstraintNode parentConstraint)
        {
            WriteCreateNode(MayaNodeTypes.ParentConstraint, constraintName, constrainedParent);

            _writer.WriteLine($"setAttr -s 1 \".tg\";");
            _writer.WriteLine($"setAttr \".tg[0].tw\" {FormatFloat(parentConstraint.Weight)};");

            Vector3 tOff = parentConstraint.TranslationOffset;
            if (tOff != Vector3.Zero)
            {
                _writer.WriteLine($"setAttr \".tg[0].tot\" -type \"double3\" {FormatFloat(tOff.X)} {FormatFloat(tOff.Y)} {FormatFloat(tOff.Z)};");
            }

            Quaternion rOff = parentConstraint.RotationOffset;
            if (rOff != Quaternion.Identity)
            {
                Vector3 euler = QuaternionToEulerDegrees(rOff);
                _writer.WriteLine($"setAttr \".tg[0].tor\" -type \"double3\" {FormatFloat(euler.X)} {FormatFloat(euler.Y)} {FormatFloat(euler.Z)};");
            }

            if (constrainedName is not null)
            {
                _connections.Add(new MayaConnection(constraintName + ".ctx", constrainedName + ".tx", false));
                _connections.Add(new MayaConnection(constraintName + ".cty", constrainedName + ".ty", false));
                _connections.Add(new MayaConnection(constraintName + ".ctz", constrainedName + ".tz", false));
                _connections.Add(new MayaConnection(constraintName + ".crx", constrainedName + ".rx", false));
                _connections.Add(new MayaConnection(constraintName + ".cry", constrainedName + ".ry", false));
                _connections.Add(new MayaConnection(constraintName + ".crz", constrainedName + ".rz", false));
            }

            if (_nodeNames.TryGetValue(parentConstraint.SourceNode, out string? sourceName))
            {
                _connections.Add(new MayaConnection(sourceName + ".t", constraintName + ".tg[0].tt", false));
                _connections.Add(new MayaConnection(sourceName + ".r", constraintName + ".tg[0].tr", false));
                _connections.Add(new MayaConnection(sourceName + ".ro", constraintName + ".tg[0].tro", false));
                _connections.Add(new MayaConnection(sourceName + ".pm", constraintName + ".tg[0].tpm", false));
            }
        }
        else if (constraint is OrientConstraintNode orientConstraint)
        {
            WriteCreateNode(MayaNodeTypes.OrientConstraint, constraintName, constrainedParent);

            _writer.WriteLine($"setAttr -s 1 \".tg\";");
            _writer.WriteLine($"setAttr \".tg[0].tw\" {FormatFloat(orientConstraint.Weight)};");

            Quaternion rOff = orientConstraint.RotationOffset;
            if (rOff != Quaternion.Identity)
            {
                Vector3 euler = QuaternionToEulerDegrees(rOff);
                _writer.WriteLine($"setAttr \".tg[0].tor\" -type \"double3\" {FormatFloat(euler.X)} {FormatFloat(euler.Y)} {FormatFloat(euler.Z)};");
            }

            if (constrainedName is not null)
            {
                _connections.Add(new MayaConnection(constraintName + ".crx", constrainedName + ".rx", false));
                _connections.Add(new MayaConnection(constraintName + ".cry", constrainedName + ".ry", false));
                _connections.Add(new MayaConnection(constraintName + ".crz", constrainedName + ".rz", false));
            }

            if (_nodeNames.TryGetValue(orientConstraint.SourceNode, out string? sourceName))
            {
                _connections.Add(new MayaConnection(sourceName + ".r", constraintName + ".tg[0].tr", false));
                _connections.Add(new MayaConnection(sourceName + ".ro", constraintName + ".tg[0].tro", false));
            }
        }
    }
}
