using System.Numerics;

namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Converts an FBX document into scene nodes, keeping geometry in the file's native coordinate system.
/// </summary>
/// <remarks>
/// Initializes a new <see cref="FbxSceneImporter"/>.
/// </remarks>
/// <param name="graph">The indexed FBX document.</param>
/// <param name="scene">The scene that receives the imported nodes.</param>
/// <param name="sourceDirectory">The directory used to resolve relative texture paths, if known.</param>
public sealed class FbxSceneImporter(FbxObjectGraph graph, Scene scene, string? sourceDirectory)
{
    private readonly Dictionary<long, SceneNode>		_nodes		= [];
    private readonly Dictionary<long, FbxNodeTransform>	_transforms	= [];
    private readonly Dictionary<long, long>				_parents	= [];
    private readonly Dictionary<long, Matrix4x4>		_worlds		= [];
    private readonly List<long>							_order		= [];
    private readonly HashSet<string>					_names		= new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int>			_suffixes	= new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the indexed FBX document.
    /// </summary>
    public FbxObjectGraph Graph { get; } = graph;

    /// <summary>
    /// Gets the scene that receives the imported nodes.
    /// </summary>
    public Scene Scene { get; } = scene;

    /// <summary>
    /// Gets the directory used to resolve relative texture paths, if known.
    /// </summary>
    public string? SourceDirectory { get; } = sourceDirectory;

    /// <summary>
    /// Gets the model identifiers ordered so that parents precede their children.
    /// </summary>
    public IReadOnlyList<long> ModelOrder => _order;

    /// <summary>
    /// Gets the meshes created for each mesh model, keyed by model identifier.
    /// </summary>
    public Dictionary<long, List<Mesh>> Meshes { get; } = [];

    /// <summary>
    /// Gets the source control point of every vertex of each imported mesh.
    /// </summary>
    public Dictionary<Mesh, int[]> ControlPoints { get; } = [];

    /// <summary>
    /// Imports an FBX document into a scene.
    /// </summary>
    /// <param name="document">The FBX document.</param>
    /// <param name="scene">The scene that receives the imported nodes.</param>
    /// <param name="sourceDirectory">The directory used to resolve relative texture paths, if known.</param>
    public static void Import(FbxDocument document, Scene scene, string? sourceDirectory)
    {
        new FbxSceneImporter(new FbxObjectGraph(document), scene, sourceDirectory).Import();
    }

    /// <summary>
    /// Returns the scene node created for a model, if any.
    /// </summary>
    /// <param name="modelId">The model identifier.</param>
    /// <returns>The scene node, or <see langword="null"/>.</returns>
    public SceneNode? GetNode(long modelId)
    {
        return _nodes.GetValueOrDefault(modelId);
    }

    /// <summary>
    /// Returns the static transform data of a model.
    /// </summary>
    /// <param name="modelId">The model identifier.</param>
    /// <returns>The transform data.</returns>
    public FbxNodeTransform GetTransform(long modelId)
    {
        return _transforms[modelId];
    }

    /// <summary>
    /// Returns the parent model of a model, or <see cref="FbxObjectGraph.RootId"/> for top-level models.
    /// </summary>
    /// <param name="modelId">The model identifier.</param>
    /// <returns>The parent model identifier.</returns>
    public long GetParentId(long modelId)
    {
        return _parents[modelId];
    }

    /// <summary>
    /// Returns the world matrix of a model in its static pose.
    /// </summary>
    /// <param name="modelId">The model identifier.</param>
    /// <returns>The world matrix.</returns>
    public Matrix4x4 GetWorldMatrix(long modelId)
    {
        return _worlds.TryGetValue(modelId, out Matrix4x4 world) ? world : Matrix4x4.Identity;
    }

    /// <summary>
    /// Returns a name that is unique across the imported scene.
    /// </summary>
    /// <param name="name">The preferred name.</param>
    /// <returns>The preferred name, or the name with a numeric suffix when it is already used.</returns>
    public string GetUniqueName(string name)
    {
        string baseName = string.IsNullOrEmpty(name) ? "Node" : name;

        if (_names.Add(baseName))
            return baseName;

        int		index = _suffixes.GetValueOrDefault(baseName, 1);
        string	candidate;

        do
        {
            candidate = $"{baseName}_{index++}";
        }
        while (!_names.Add(candidate));

        _suffixes[baseName] = index;
        return candidate;
    }

    /// <summary>
    /// Evaluates a model's world matrix and the matching local matrix relative to its parent's world matrix.
    /// </summary>
    /// <param name="modelId">The model identifier.</param>
    /// <param name="local">The model's FBX local matrix.</param>
    /// <param name="parentWorld">The parent's world matrix.</param>
    /// <param name="parentScaling">The parent's local scale.</param>
    /// <param name="world">The model's world matrix.</param>
    /// <returns>The local matrix that reproduces the world matrix under standard matrix inheritance.</returns>
    public Matrix4x4 ResolveLocalMatrix(long modelId, Matrix4x4 local, Matrix4x4 parentWorld, Vector3 parentScaling, out Matrix4x4 world)
    {
        FbxInheritType inheritType = _parents[modelId] == FbxObjectGraph.RootId ? FbxInheritType.RSrs : _transforms[modelId].InheritType;

        world = FbxTransform.ComputeWorld(local, parentWorld, parentScaling, inheritType);

        if (inheritType == FbxInheritType.RSrs || !Matrix4x4.Invert(parentWorld, out Matrix4x4 parentInverse))
            return local;

        return world * parentInverse;
    }

    /// <summary>
    /// Returns the node attribute that describes a model, which is the model itself in FBX 6 files.
    /// </summary>
    /// <param name="modelId">The model identifier.</param>
    /// <returns>The attribute object.</returns>
    public FbxObject GetAttribute(long modelId)
    {
        return Graph.GetFirstChild(modelId, "NodeAttribute") ?? Graph.GetObject(modelId)!;
    }

    private static SceneUpAxis GetUpAxis(FbxObject settings)
    {
        return settings.GetInt32("UpAxis", 1) switch
        {
            0 => SceneUpAxis.X,
            2 => SceneUpAxis.Z,
            _ => SceneUpAxis.Y,
        };
    }

    private static bool IsBoneType(string subType)
    {
        return subType is "LimbNode" or "Limb" or "Root";
    }

    private static void ApplyCamera(Camera camera, FbxObject attribute, Matrix4x4 world)
    {
        float	filmWidth		= attribute.GetSingle("FilmWidth", 0.816f);
        float	filmHeight		= attribute.GetSingle("FilmHeight", 0.612f);
        float	aspect			= attribute.GetSingle("FilmAspectRatio", filmHeight > 0.0f ? filmWidth / filmHeight : camera.AspectRatio);
        float	focalLength		= attribute.GetSingle("FocalLength", 0.0f);
        int		apertureMode	= attribute.GetInt32("ApertureMode", 2);
        float	fieldOfView		= attribute.GetSingle("FieldOfView", camera.FieldOfView);

        if ((apertureMode == 3 || !attribute.HasProperty("FieldOfView")) && focalLength > 0.0f)
            fieldOfView = 2.0f * MathF.Atan(filmHeight * 25.4f / (2.0f * focalLength)) * (180.0f / MathF.PI);
        else if (apertureMode is 0 or 1 && aspect > 0.0f)
            fieldOfView = 2.0f * MathF.Atan(MathF.Tan(fieldOfView * (MathF.PI / 360.0f)) / aspect) * (180.0f / MathF.PI);

        camera.FieldOfView		= fieldOfView;
        camera.AspectRatio		= aspect > 0.0f ? aspect : camera.AspectRatio;
        camera.NearPlane		= attribute.GetSingle("NearPlane", camera.NearPlane);
        camera.FarPlane			= attribute.GetSingle("FarPlane", camera.FarPlane);
        camera.Projection		= attribute.GetInt32("CameraProjectionType", 0) == 1 ? CameraProjection.Orthographic : CameraProjection.Perspective;
        camera.OrthographicSize	= attribute.GetSingle("OrthoZoom", camera.OrthographicSize);
        camera.Position			= world.Translation;
        camera.Target			= world.Translation + Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitX, world));
        camera.Up				= Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitY, world));
    }

    private static void ApplyLight(Light light, FbxObject attribute)
    {
        light.Color				= attribute.GetVector3("Color", Vector3.One);
        light.Intensity			= attribute.GetSingle("Intensity", 100.0f) / 100.0f;
        light.Enabled			= attribute.GetBoolean("CastLight", true);
        light.InnerConeAngle	= attribute.GetSingle("InnerAngle", light.InnerConeAngle);
        light.OuterConeAngle	= attribute.GetSingle("OuterAngle", attribute.GetSingle("Cone angle", light.OuterConeAngle));
        light.Type				= attribute.GetInt32("LightType", 0) switch
        {
            1 => LightType.Directional,
            2 => LightType.Spot,
            3 => LightType.Area,
            _ => LightType.Point,
        };
    }

    private void Import()
    {
        Scene.UpAxis = GetUpAxis(Graph.Settings);

        CreateModelNodes();
        LinkHierarchy();
        ApplyStaticTransforms();

        Dictionary<long, Material> materials = FbxMaterialImporter.Import(this);

        FbxMeshImporter.Import(this, materials);
        FbxSkinImporter.Import(this);
        FbxMorphImporter.Import(this);
        FbxConstraintImporter.Import(this);
        FbxAnimationImporter.Import(this);
    }

    private void CreateModelNodes()
    {
        HashSet<long> boneIds = [];

        foreach (FbxObject fbxObject in Graph.Objects)
        {
            if (fbxObject.ClassName == "Deformer" && fbxObject.SubType == "Cluster")
            {
                foreach (FbxObject link in Graph.GetChildren(fbxObject.Id, "Model"))
                    boneIds.Add(link.Id);

                foreach (FbxObject link in Graph.GetParents(fbxObject.Id, "Model"))
                    boneIds.Add(link.Id);
            }
        }

        foreach (FbxObject model in Graph.Objects)
        {
            if (model.ClassName != "Model" || model.Id == FbxObjectGraph.RootId)
                continue;

            SceneNode node = CreateNode(model, GetUniqueName(model.Name), boneIds.Contains(model.Id));

            _nodes[model.Id]		= node;
            _transforms[model.Id]	= new FbxNodeTransform(model);

            if (node is Mesh mesh)
                Meshes[model.Id] = [mesh];
        }
    }

    private SceneNode CreateNode(FbxObject model, string name, bool isSkinLink)
    {
        FbxObject attribute = GetAttribute(model.Id);

        if (model.SubType == "Mesh" || Graph.GetFirstChild(model.Id, "Geometry") is not null || model.Node.FirstChild("Vertices") is not null)
        {
            if (Graph.GetChildren(model.Id, "Material").Skip(1).Any())
                return new Group(name);

            return new Mesh { Name = name };
        }

        if (isSkinLink || IsBoneType(model.SubType) || IsBoneType(attribute.SubType))
            return new SkeletonBone(name);

        if (model.SubType == "Camera" || attribute.SubType == "Camera")
            return new Camera(name);

        if (model.SubType == "Light" || attribute.SubType == "Light")
            return new Light(name);

        return new Group(name);
    }

    private void LinkHierarchy()
    {
        Dictionary<long, List<long>> children = [];

        foreach (long id in _nodes.Keys)
        {
            long parentId = FbxObjectGraph.RootId;

            foreach (FbxConnection connection in Graph.GetParentConnections(id))
            {
                if (connection.ConnectionType == "OO" && connection.ParentId != id && _nodes.ContainsKey(connection.ParentId))
                {
                    parentId = connection.ParentId;
                    break;
                }
            }

            _parents[id] = parentId;
        }

        BreakCycles();

        foreach ((long id, long parentId) in _parents)
        {
            if (!children.TryGetValue(parentId, out List<long>? list))
                children[parentId] = list = [];

            list.Add(id);
        }

        Queue<long> pending = new();
        pending.Enqueue(FbxObjectGraph.RootId);

        while (pending.TryDequeue(out long parentId))
        {
            if (!children.TryGetValue(parentId, out List<long>? list))
                continue;

            foreach (long id in list)
            {
                _order.Add(id);
                _nodes[id].SetParentUnsafe(parentId == FbxObjectGraph.RootId ? Scene : _nodes[parentId]);
                pending.Enqueue(id);
            }
        }
    }

    private void BreakCycles()
    {
        Dictionary<long, bool>	resolved	= [];
        List<long>				chain		= [];

        foreach (long start in _parents.Keys.ToArray())
        {
            long current = start;
            chain.Clear();

            while (current != FbxObjectGraph.RootId && !resolved.ContainsKey(current))
            {
                resolved[current] = false;
                chain.Add(current);
                current = _parents[current];
            }

            if (current != FbxObjectGraph.RootId && !resolved[current])
                _parents[current] = FbxObjectGraph.RootId;

            foreach (long id in chain)
                resolved[id] = true;
        }
    }

    private void ApplyStaticTransforms()
    {
        foreach (long id in _order)
        {
            FbxNodeTransform	transform	= _transforms[id];
            long				parentId	= _parents[id];
            Matrix4x4			parentWorld	= parentId == FbxObjectGraph.RootId ? Matrix4x4.Identity : _worlds[parentId];
            Vector3				parentScale	= parentId == FbxObjectGraph.RootId ? Vector3.One : _transforms[parentId].Scaling;
            Matrix4x4			local		= ResolveLocalMatrix(id, transform.GetLocalMatrix(), parentWorld, parentScale, out Matrix4x4 world);
            SceneNode			node		= _nodes[id];

            _worlds[id] = world;

            FbxTransform.Decompose(local, out	Vector3 translation, out Quaternion rotation, out Vector3 scale);
            node.BindTransform.LocalPosition	= translation;
            node.BindTransform.LocalRotation	= rotation;
            node.BindTransform.Scale			= scale;

            if (node is Camera camera)
                ApplyCamera(camera, GetAttribute(id), world);
            else if (node is Light light)
                ApplyLight(light, GetAttribute(id));
        }
    }
}
