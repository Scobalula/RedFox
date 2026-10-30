using System.Numerics;
using RedFox.Graphics3D.IO;

namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Converts a scene selection into an FBX 7 document.
/// </summary>
/// <remarks>
/// Initializes a new <see cref="FbxSceneExporter"/>.
/// </remarks>
/// <param name="selection">The scene selection to export.</param>
/// <param name="options">The write options.</param>
/// <param name="targetDirectory">The directory the file is written to, if known.</param>
public sealed class FbxSceneExporter(SceneTranslationSelection selection, FbxWriteOptions options, string? targetDirectory)
{
    private const string CreationTime = "1970-01-01 10:00:00:000";

    private static readonly byte[] FileId = [0x28, 0xB3, 0x2A, 0xEB, 0xB6, 0x24, 0xCC, 0xC2, 0xBF, 0xC8, 0xB0, 0x2A, 0xA9, 0x2B, 0xFC, 0xF1];

    private readonly Dictionary<SceneNode, long>		_modelIds		= [];
    private readonly Dictionary<SceneNode, Matrix4x4>	_bindWorlds		= [];
    private readonly Dictionary<SceneNode, Matrix4x4>	_activeWorlds	= [];
    private readonly Dictionary<string, int>			_objectCounts	= new(StringComparer.Ordinal);
    private long										_nextId			= 1000000;

    /// <summary>
    /// Gets the exported selection.
    /// </summary>
    public SceneTranslationSelection Selection { get; } = selection;

    /// <summary>
    /// Gets the write options.
    /// </summary>
    public FbxWriteOptions Options { get; } = options;

    /// <summary>
    /// Gets the directory the file is written to, used to make texture paths relative, if known.
    /// </summary>
    public string? TargetDirectory { get; } = targetDirectory;

    /// <summary>
    /// Gets the <c>Objects</c> node that receives exported objects.
    /// </summary>
    public FbxNode Objects { get; } = new("Objects");

    /// <summary>
    /// Gets the <c>Connections</c> node that receives exported connections.
    /// </summary>
    public FbxNode Connections { get; } = new("Connections");

    /// <summary>
    /// Gets the exported scene nodes and their FBX model identifiers, in export order.
    /// </summary>
    public IReadOnlyDictionary<SceneNode, long> ModelIds => _modelIds;

    /// <summary>
    /// Gets the animation take names and their time ranges in FBX ticks, written to the <c>Takes</c> section.
    /// </summary>
    public List<FbxExportedTake> Takes { get; } = [];

    /// <summary>
    /// Gets the exported materials and their FBX object identifiers.
    /// </summary>
    public Dictionary<Material, long> MaterialIds { get; } = [];

    /// <summary>
    /// Gets the exported blend shape channels keyed by morph target name.
    /// </summary>
    public Dictionary<string, List<long>> BlendShapeChannels { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the bind world matrices written to the bind pose, keyed by model identifier.
    /// </summary>
    public Dictionary<long, Matrix4x4> BindPose { get; } = [];

    /// <summary>
    /// Exports a scene selection as an FBX document.
    /// </summary>
    /// <param name="selection">The scene selection to export.</param>
    /// <param name="options">The write options.</param>
    /// <param name="targetDirectory">The directory the file is written to, if known.</param>
    /// <returns>The FBX document.</returns>
    public static FbxDocument Export(SceneTranslationSelection selection, FbxWriteOptions options, string? targetDirectory)
    {
        return new FbxSceneExporter(selection, options, targetDirectory).Export();
    }

    /// <summary>
    /// Creates an object in the <c>Objects</c> section.
    /// </summary>
    /// <param name="className">The FBX class name, such as <c>Model</c> or <c>Geometry</c>.</param>
    /// <param name="name">The object name.</param>
    /// <param name="subType">The object sub-type.</param>
    /// <param name="node">The created object node.</param>
    /// <returns>The object identifier.</returns>
    public long AddObject(string className, string name, string subType, out FbxNode node)
    {
        long id = _nextId++;

        node = Objects.AddChild(className);
        node.Properties.Add(new FbxProperty(id));
        node.Properties.Add(new FbxProperty($"{name}\0\u0001{className}"));
        node.Properties.Add(new FbxProperty(subType));

        _objectCounts[className] = _objectCounts.GetValueOrDefault(className) + 1;
        return id;
    }

    /// <summary>
    /// Adds an object-to-object connection.
    /// </summary>
    /// <param name="childId">The child object identifier.</param>
    /// <param name="parentId">The parent object identifier.</param>
    public void Connect(long childId, long parentId)
    {
        FbxNode connection = Connections.AddChild("C", new FbxProperty("OO"));
        connection.Properties.Add(new FbxProperty(childId));
        connection.Properties.Add(new FbxProperty(parentId));
    }

    /// <summary>
    /// Adds an object-to-property connection.
    /// </summary>
    /// <param name="childId">The child object identifier.</param>
    /// <param name="parentId">The parent object identifier.</param>
    /// <param name="property">The parent property the child connects to.</param>
    public void Connect(long childId, long parentId, string property)
    {
        FbxNode connection = Connections.AddChild("C", new FbxProperty("OP"));
        connection.Properties.Add(new FbxProperty(childId));
        connection.Properties.Add(new FbxProperty(parentId));
        connection.Properties.Add(new FbxProperty(property));
    }

    /// <summary>
    /// Returns the bind world matrix of a scene node, caching the result.
    /// </summary>
    /// <param name="node">The scene node.</param>
    /// <returns>The bind world matrix.</returns>
    public Matrix4x4 GetBindWorld(SceneNode node)
    {
        return GetWorld(node, _bindWorlds, false);
    }

    /// <summary>
    /// Returns the active world matrix of a scene node, which uses its live transform when set, caching the result.
    /// </summary>
    /// <param name="node">The scene node.</param>
    /// <returns>The active world matrix.</returns>
    public Matrix4x4 GetActiveWorld(SceneNode node)
    {
        return GetWorld(node, _activeWorlds, true);
    }

    /// <summary>
    /// Returns the exported model identifier of the closest exported ancestor of a node, or <see cref="FbxObjectGraph.RootId"/>.
    /// </summary>
    /// <param name="node">The scene node.</param>
    /// <param name="parent">The closest exported ancestor, or <see langword="null"/>.</param>
    /// <returns>The parent model identifier.</returns>
    public long GetExportedParent(SceneNode node, out SceneNode? parent)
    {
        for (parent = node.Parent; parent is not null; parent = parent.Parent)
        {
            if (_modelIds.TryGetValue(parent, out long id))
                return id;
        }

        return FbxObjectGraph.RootId;
    }

    /// <summary>
    /// Appends a <c>P</c> property definition to a <c>Properties70</c> node.
    /// </summary>
    /// <param name="properties">The <c>Properties70</c> node.</param>
    /// <param name="name">The property name.</param>
    /// <param name="type">The property type name.</param>
    /// <param name="label">The property label.</param>
    /// <param name="flags">The property flags, such as <c>A</c> for animatable.</param>
    /// <param name="values">The property values.</param>
    public static void AddProperty(FbxNode properties, string name, string type, string label, string flags, params ReadOnlySpan<FbxProperty> values)
    {
        FbxNode property = properties.AddChild("P", new FbxProperty(name));

        property.Properties.Add(new FbxProperty(type));
        property.Properties.Add(new FbxProperty(label));
        property.Properties.Add(new FbxProperty(flags));

        foreach (FbxProperty value in values)
            property.Properties.Add(value);
    }

    /// <summary>
    /// Appends a three-component property definition to a <c>Properties70</c> node.
    /// </summary>
    /// <param name="properties">The <c>Properties70</c> node.</param>
    /// <param name="name">The property name.</param>
    /// <param name="type">The property type name, such as <c>Vector3D</c> or <c>Color</c>.</param>
    /// <param name="flags">The property flags.</param>
    /// <param name="value">The property value.</param>
    public static void AddVector3(FbxNode properties, string name, string type, string flags, Vector3 value)
    {
        string label = type switch
        {
            "Color" or "ColorRGB"	=> "Color",
            "Vector3D"				=> "Vector",
            _						=> string.Empty,
        };

        AddProperty(properties, name, type, label, flags, new FbxProperty((double)value.X), new FbxProperty((double)value.Y), new FbxProperty((double)value.Z));
    }

    /// <summary>
    /// Appends a numeric property definition to a <c>Properties70</c> node.
    /// </summary>
    /// <param name="properties">The <c>Properties70</c> node.</param>
    /// <param name="name">The property name.</param>
    /// <param name="flags">The property flags.</param>
    /// <param name="value">The property value.</param>
    public static void AddNumber(FbxNode properties, string name, string flags, double value)
    {
        AddProperty(properties, name, "Number", string.Empty, flags, new FbxProperty(value));
    }

    /// <summary>
    /// Appends an integer or enumeration property definition to a <c>Properties70</c> node.
    /// </summary>
    /// <param name="properties">The <c>Properties70</c> node.</param>
    /// <param name="name">The property name.</param>
    /// <param name="type">The property type name, such as <c>int</c>, <c>enum</c> or <c>bool</c>.</param>
    /// <param name="value">The property value.</param>
    public static void AddInteger(FbxNode properties, string name, string type, int value)
    {
        AddProperty(properties, name, type, string.Empty, string.Empty, new FbxProperty(value));
    }

    /// <summary>
    /// Appends a time property definition to a <c>Properties70</c> node.
    /// </summary>
    /// <param name="properties">The <c>Properties70</c> node.</param>
    /// <param name="name">The property name.</param>
    /// <param name="ticks">The time in FBX ticks.</param>
    public static void AddTime(FbxNode properties, string name, long ticks)
    {
        AddProperty(properties, name, "KTime", "Time", string.Empty, new FbxProperty(ticks));
    }

    /// <summary>
    /// Flattens a matrix into the sixteen values stored by FBX.
    /// </summary>
    /// <param name="matrix">The matrix.</param>
    /// <returns>The matrix values in row order.</returns>
    public static double[] ToArray(Matrix4x4 matrix)
    {
        return [matrix.M11, matrix.M12, matrix.M13, matrix.M14, matrix.M21, matrix.M22, matrix.M23, matrix.M24, matrix.M31, matrix.M32, matrix.M33, matrix.M34, matrix.M41, matrix.M42, matrix.M43, matrix.M44];
    }

    private static string GetModelType(SceneNode node)
    {
        return node switch
        {
            Mesh			=> "Mesh",
            SkeletonBone	=> "LimbNode",
            Camera			=> "Camera",
            Light			=> "Light",
            _				=> "Null",
        };
    }

    private static bool IsExportedModel(SceneNode node)
    {
        return node is Group or Skeleton or SkeletonBone or Mesh or Camera or Light;
    }

    private FbxDocument Export()
    {
        foreach (SceneNode node in Selection.GetDescendants<SceneNode>())
        {
            if (IsExportedModel(node))
                ExportModel(node);
        }

        if (Options.WriteMaterials)
            FbxMaterialExporter.Export(this);

        foreach ((SceneNode node, long modelId) in _modelIds)
        {
            if (node is Mesh mesh)
                FbxMeshExporter.Export(this, mesh, modelId);
        }

        if (BindPose.Count > 0)
            ExportBindPose();

        FbxConstraintExporter.Export(this);

        if (Options.WriteAnimations)
            FbxAnimationExporter.Export(this);

        FbxDocument document = new()
        {
            Version	= Options.Version,
            Format	= Options.Format,
        };

        DateTime now = DateTime.Now;

        document.Nodes.Add(CreateHeader(now));
        document.Nodes.Add(new FbxNode("FileId") { Properties = { new FbxProperty(FileId) } });
        document.Nodes.Add(new FbxNode("CreationTime") { Properties = { new FbxProperty(CreationTime) } });
        document.Nodes.Add(new FbxNode("Creator") { Properties = { new FbxProperty("RedFox Graphics3D") } });
        document.Nodes.Add(CreateGlobalSettings());
        document.Nodes.Add(CreateDocuments());
        document.Nodes.Add(new FbxNode("References"));
        document.Nodes.Add(CreateDefinitions());
        document.Nodes.Add(Objects);
        document.Nodes.Add(Connections);
        document.Nodes.Add(CreateTakes());

        return document;
    }

    private void ExportModel(SceneNode node)
    {
        string		type		= GetModelType(node);
        long		modelId		= AddObject("Model", node.Name, type, out FbxNode model);
        long		parentId	= GetExportedParent(node, out SceneNode? parent);
        Matrix4x4	local		= GetExportedLocal(node, parent);

        _modelIds[node] = modelId;

        FbxTransform.Decompose(local, out Vector3 translation, out Quaternion rotation, out Vector3 scale);

        FbxNode properties = model.AddChild("Properties70");

        model.Children.Insert(0, new FbxNode("Version") { Properties = { new FbxProperty(232) } });
        AddInteger(properties, "InheritType", "enum", 1);
        AddInteger(properties, "DefaultAttributeIndex", "int", 0);
        AddVector3(properties, "Lcl Translation", "Lcl Translation", "A", translation);
        AddVector3(properties, "Lcl Rotation", "Lcl Rotation", "A", FbxTransform.QuaternionToEulerXyz(rotation));
        AddVector3(properties, "Lcl Scaling", "Lcl Scaling", "A", scale);

        model.AddChild("Shading", FbxProperty.CreateBoolean((byte)'T'));
        model.AddChild("Culling", new FbxProperty("CullingOff"));

        Connect(modelId, parentId);

        if (type == "Mesh")
            return;

        long		attributeId	= AddObject("NodeAttribute", node.Name, type, out FbxNode attribute);
        FbxNode		values		= attribute.AddChild("Properties70");

        switch (node)
        {
            case Camera camera:
                FbxNodeAttributeExporter.WriteCamera(attribute, values, camera);
                break;
            case Light light:
                FbxNodeAttributeExporter.WriteLight(attribute, values, light);
                break;
            case SkeletonBone:
                AddNumber(values, "Size", string.Empty, 1.0);
                attribute.AddChild("TypeFlags", new FbxProperty("Skeleton"));
                break;
            default:
                attribute.AddChild("TypeFlags", new FbxProperty("Null"));
                break;
        }

        Connect(attributeId, modelId);
    }

    private void ExportBindPose()
    {
        AddObject("Pose", "BindPose", "BindPose", out FbxNode pose);

        pose.AddChild("Type", new FbxProperty("BindPose"));
        pose.AddChild("Version", new FbxProperty(100));
        pose.AddChild("NbPoseNodes", new FbxProperty(BindPose.Count));

        foreach ((long modelId, Matrix4x4 matrix) in BindPose)
        {
            FbxNode poseNode = pose.AddChild("PoseNode");
            poseNode.AddChild("Node", new FbxProperty(modelId));
            poseNode.AddChild("Matrix", new FbxProperty(ToArray(matrix)));
        }
    }

    private static Matrix4x4 GetWorld(SceneNode node, Dictionary<SceneNode, Matrix4x4> cache, bool active)
    {
        if (cache.TryGetValue(node, out Matrix4x4 world))
            return world;

        Stack<SceneNode> pending = new();

        for (SceneNode? current = node; current is not null && !cache.ContainsKey(current); current = current.Parent)
            pending.Push(current);

        while (pending.TryPop(out SceneNode? current))
        {
            world = active ? current.GetActiveLocalMatrix() : current.GetBindLocalMatrix();

            if (current.Parent is not null)
                world *= cache[current.Parent];

            cache[current] = world;
        }

        return world;
    }

    private Matrix4x4 GetExportedLocal(SceneNode node, SceneNode? parent)
    {
        if (parent is null)
            return GetActiveWorld(node);

        if (ReferenceEquals(parent, node.Parent))
            return node.GetActiveLocalMatrix();

        return Matrix4x4.Invert(GetActiveWorld(parent), out Matrix4x4 inverse) ? GetActiveWorld(node) * inverse : GetActiveWorld(node);
    }

    private FbxNode CreateHeader(DateTime now)
    {
        FbxNode header		= new("FBXHeaderExtension");
        FbxNode timeStamp	= new("CreationTimeStamp");

        header.AddChild("FBXHeaderVersion", new FbxProperty(1003));
        header.AddChild("FBXVersion", new FbxProperty(Options.Version));
        header.AddChild("EncryptionType", new FbxProperty(0));
        timeStamp.AddChild("Version", new FbxProperty(1000));
        timeStamp.AddChild("Year", new FbxProperty(now.Year));
        timeStamp.AddChild("Month", new FbxProperty(now.Month));
        timeStamp.AddChild("Day", new FbxProperty(now.Day));
        timeStamp.AddChild("Hour", new FbxProperty(now.Hour));
        timeStamp.AddChild("Minute", new FbxProperty(now.Minute));
        timeStamp.AddChild("Second", new FbxProperty(now.Second));
        timeStamp.AddChild("Millisecond", new FbxProperty(now.Millisecond));
        header.Children.Add(timeStamp);
        header.AddChild("Creator", new FbxProperty("RedFox Graphics3D"));

        return header;
    }

    private FbxNode CreateGlobalSettings()
    {
        FbxNode settings	= new("GlobalSettings");
        FbxNode properties	= new("Properties70");

        SceneUpAxis	upAxis		= Selection.Scene.UpAxis;
        int			up			= upAxis switch { SceneUpAxis.X => 0, SceneUpAxis.Z => 2, _ => 1 };
        int			front		= upAxis == SceneUpAxis.Z ? 1 : 2;
        int			coord		= upAxis == SceneUpAxis.X ? 1 : 0;
        int			frontSign	= upAxis == SceneUpAxis.Z ? -1 : 1;

        settings.AddChild("Version", new FbxProperty(1000));
        settings.Children.Add(properties);

        AddInteger(properties, "UpAxis", "int", up);
        AddInteger(properties, "UpAxisSign", "int", 1);
        AddInteger(properties, "FrontAxis", "int", front);
        AddInteger(properties, "FrontAxisSign", "int", frontSign);
        AddInteger(properties, "CoordAxis", "int", coord);
        AddInteger(properties, "CoordAxisSign", "int", upAxis == SceneUpAxis.X ? -1 : 1);
        AddInteger(properties, "OriginalUpAxis", "int", up);
        AddInteger(properties, "OriginalUpAxisSign", "int", 1);
        AddProperty(properties, "UnitScaleFactor", "double", "Number", string.Empty, new FbxProperty(1.0));
        AddProperty(properties, "OriginalUnitScaleFactor", "double", "Number", string.Empty, new FbxProperty(1.0));
        AddInteger(properties, "TimeMode", "enum", 14);
        AddProperty(properties, "CustomFrameRate", "double", "Number", string.Empty, new FbxProperty(FbxAnimationExporter.GetFrameRate(Selection)));
        AddTime(properties, "TimeSpanStart", Takes.Count > 0 ? Takes.Min(static take => take.Start) : 0);
        AddTime(properties, "TimeSpanStop", Takes.Count > 0 ? Takes.Max(static take => take.Stop) : FbxAnimationCurve.TicksPerSecond);

        return settings;
    }

    private FbxNode CreateDocuments()
    {
        FbxNode documents	= new("Documents");
        FbxNode document	= new("Document");
        FbxNode properties	= new("Properties70");

        documents.AddChild("Count", new FbxProperty(1));
        document.Properties.Add(new FbxProperty(_nextId++));
        document.Properties.Add(new FbxProperty("Scene"));
        document.Properties.Add(new FbxProperty("Scene"));
        document.Children.Add(properties);
        document.AddChild("RootNode", new FbxProperty(0L));
        documents.Children.Add(document);

        AddProperty(properties, "SourceObject", "object", string.Empty, string.Empty);
        AddProperty(properties, "ActiveAnimStackName", "KString", string.Empty, string.Empty, new FbxProperty(Takes.Count > 0 ? Takes[0].Name : string.Empty));

        return documents;
    }

    private FbxNode CreateDefinitions()
    {
        FbxNode definitions = new("Definitions");

        definitions.AddChild("Version", new FbxProperty(100));
        definitions.AddChild("Count", new FbxProperty(_objectCounts.Values.Sum() + 1));
        definitions.AddChild("ObjectType", new FbxProperty("GlobalSettings")).AddChild("Count", new FbxProperty(1));

        foreach ((string className, int count) in _objectCounts)
            definitions.AddChild("ObjectType", new FbxProperty(className)).AddChild("Count", new FbxProperty(count));

        return definitions;
    }

    private FbxNode CreateTakes()
    {
        FbxNode takes = new("Takes");

        takes.AddChild("Current", new FbxProperty(Takes.Count > 0 ? Takes[0].Name : string.Empty));

        foreach (FbxExportedTake exported in Takes)
        {
            FbxNode take	= takes.AddChild("Take", new FbxProperty(exported.Name));
            FbxNode local	= take.AddChild("LocalTime", new FbxProperty(exported.Start));

            take.Children.Insert(0, new FbxNode("FileName") { Properties = { new FbxProperty($"{exported.Name}.tak") } });
            local.Properties.Add(new FbxProperty(exported.Stop));
            take.AddChild("ReferenceTime", new FbxProperty(exported.Start)).Properties.Add(new FbxProperty(exported.Stop));
        }

        return takes;
    }
}
