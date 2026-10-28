namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Indexes the objects and connections of an FBX document so relationships can be resolved without scanning.
/// </summary>
public sealed class FbxObjectGraph
{
    private readonly Dictionary<long, FbxObject>			_objects	= [];
    private readonly Dictionary<string, long>				_namedIds	= new(StringComparer.Ordinal);
    private readonly Dictionary<long, List<FbxConnection>>	_parents	= [];
    private readonly Dictionary<long, List<FbxConnection>>	_children	= [];

    /// <summary>
    /// Gets the identifier of the implicit scene root object.
    /// </summary>
    public const long RootId = 0;

    /// <summary>
    /// Gets the source document.
    /// </summary>
    public FbxDocument Document { get; }

    /// <summary>
    /// Gets all objects in document order.
    /// </summary>
    public List<FbxObject> Objects { get; } = [];

    /// <summary>
    /// Gets the document's <c>GlobalSettings</c>, which is empty when the document does not define any.
    /// </summary>
    public FbxObject Settings { get; }

    /// <summary>
    /// Initializes a new <see cref="FbxObjectGraph"/> from a parsed document.
    /// </summary>
    /// <param name="document">The FBX document to index.</param>
    public FbxObjectGraph(FbxDocument document)
    {
        Document = document;

        Dictionary<string, Dictionary<string, FbxNode>>	templates	= ReadTemplates(document);
        FbxNode?										objects		= document.FirstNode("Objects");

        _namedIds["Model::Scene"] = RootId;

        if (objects is not null)
        {
            foreach (FbxNode node in objects.Children)
            {
                if (node.Properties.Count > 0)
                    AddObject(node, templates);
            }
        }

        if (document.FirstNode("Connections") is FbxNode connections)
        {
            foreach (FbxNode connection in connections.Children)
                AddConnection(connection);
        }

        FbxNode settings = document.FirstNode("GlobalSettings") ?? objects?.FirstChild("GlobalSettings") ?? new FbxNode("GlobalSettings");
        Settings = new FbxObject(-1, "GlobalSettings", string.Empty, settings, null);
    }

    /// <summary>
    /// Returns the object with the given identifier, if any.
    /// </summary>
    /// <param name="id">The object identifier.</param>
    /// <returns>The object, or <see langword="null"/>.</returns>
    public FbxObject? GetObject(long id)
    {
        return _objects.GetValueOrDefault(id);
    }

    /// <summary>
    /// Resolves the identifier of an object referenced by its raw FBX 6 name such as <c>Model::Cube</c>.
    /// </summary>
    /// <param name="rawName">The raw object name.</param>
    /// <param name="id">The object identifier.</param>
    /// <returns><see langword="true"/> when the name is known.</returns>
    public bool TryGetIdByName(string rawName, out long id)
    {
        return _namedIds.TryGetValue(rawName, out id);
    }

    /// <summary>
    /// Returns the connections in which the given object is the child.
    /// </summary>
    /// <param name="childId">The child object identifier.</param>
    /// <returns>The connections to the object's parents.</returns>
    public IReadOnlyList<FbxConnection> GetParentConnections(long childId)
    {
        return _parents.TryGetValue(childId, out List<FbxConnection>? connections) ? connections : [];
    }

    /// <summary>
    /// Returns the connections in which the given object is the parent.
    /// </summary>
    /// <param name="parentId">The parent object identifier.</param>
    /// <returns>The connections to the object's children.</returns>
    public IReadOnlyList<FbxConnection> GetChildConnections(long parentId)
    {
        return _children.TryGetValue(parentId, out List<FbxConnection>? connections) ? connections : [];
    }

    /// <summary>
    /// Returns the objects of a class connected as children of the given object.
    /// </summary>
    /// <param name="parentId">The parent object identifier.</param>
    /// <param name="className">The FBX class name to match.</param>
    /// <returns>The matching child objects in connection order.</returns>
    public IEnumerable<FbxObject> GetChildren(long parentId, string className)
    {
        foreach (FbxConnection connection in GetChildConnections(parentId))
        {
            if (_objects.TryGetValue(connection.ChildId, out FbxObject? child) && child.ClassName == className)
                yield return child;
        }
    }

    /// <summary>
    /// Returns the first object of a class connected as a child of the given object.
    /// </summary>
    /// <param name="parentId">The parent object identifier.</param>
    /// <param name="className">The FBX class name to match.</param>
    /// <returns>The first matching child, or <see langword="null"/>.</returns>
    public FbxObject? GetFirstChild(long parentId, string className)
    {
        foreach (FbxObject child in GetChildren(parentId, className))
            return child;

        return null;
    }

    /// <summary>
    /// Returns the objects of a class that the given object is connected to as a child.
    /// </summary>
    /// <param name="childId">The child object identifier.</param>
    /// <param name="className">The FBX class name to match.</param>
    /// <returns>The matching parent objects in connection order.</returns>
    public IEnumerable<FbxObject> GetParents(long childId, string className)
    {
        foreach (FbxConnection connection in GetParentConnections(childId))
        {
            if (_objects.TryGetValue(connection.ParentId, out FbxObject? parent) && parent.ClassName == className)
                yield return parent;
        }
    }

    /// <summary>
    /// Returns the first object of a class that the given object is connected to as a child.
    /// </summary>
    /// <param name="childId">The child object identifier.</param>
    /// <param name="className">The FBX class name to match.</param>
    /// <returns>The first matching parent, or <see langword="null"/>.</returns>
    public FbxObject? GetFirstParent(long childId, string className)
    {
        foreach (FbxObject parent in GetParents(childId, className))
            return parent;

        return null;
    }

    private static Dictionary<string, Dictionary<string, FbxNode>> ReadTemplates(FbxDocument document)
    {
        Dictionary<string, Dictionary<string, FbxNode>> templates = new(StringComparer.Ordinal);

        if (document.FirstNode("Definitions") is not FbxNode definitions)
            return templates;

        foreach (FbxNode objectType in definitions.ChildrenNamed("ObjectType"))
        {
            if (objectType.Properties.Count == 0 || objectType.Properties[0].Type != FbxPropertyType.String)
                continue;

            foreach (FbxNode template in objectType.ChildrenNamed("PropertyTemplate"))
            {
                if (template.FirstChild("Properties70") is not FbxNode properties)
                    continue;

                string className = objectType.Properties[0].AsString();

                if (!templates.TryGetValue(className, out Dictionary<string, FbxNode>? table))
                    templates[className] = table = new Dictionary<string, FbxNode>(StringComparer.Ordinal);

                foreach (KeyValuePair<string, FbxNode> property in FbxObject.ReadPropertyTable(properties))
                    table.TryAdd(property.Key, property.Value);
            }
        }

        return templates;
    }

    private void AddObject(FbxNode node, Dictionary<string, Dictionary<string, FbxNode>> templates)
    {
        FbxProperty	first = node.Properties[0];
        long		id;
        string		rawName;
        string		subType;

        if (first.Type == FbxPropertyType.String)
        {
            rawName	= first.AsString();
            subType	= node.Properties.Count > 1 && node.Properties[1].Type == FbxPropertyType.String ? node.Properties[1].AsString() : string.Empty;
            id		= _namedIds.Count;

            if (!_namedIds.TryAdd(rawName, id))
                return;
        }
        else if (first.IsNumber)
        {
            id		= first.AsInt64();
            rawName	= node.Properties.Count > 1 && node.Properties[1].Type == FbxPropertyType.String ? node.Properties[1].AsString() : string.Empty;
            subType	= node.Properties.Count > 2 && node.Properties[2].Type == FbxPropertyType.String ? node.Properties[2].AsString() : string.Empty;
        }
        else
        {
            return;
        }

        FbxObject fbxObject = new(id, FbxObject.ParseName(rawName), subType, node, templates.GetValueOrDefault(node.Name));

        if (_objects.TryAdd(id, fbxObject))
            Objects.Add(fbxObject);
    }

    private void AddConnection(FbxNode node)
    {
        if (node.Properties.Count < 3 || node.Properties[0].Type != FbxPropertyType.String)
            return;

        if (!TryResolveId(node.Properties[1], out long childId) || !TryResolveId(node.Properties[2], out long parentId))
            return;

        string			property	= node.Properties.Count > 3 && node.Properties[3].Type == FbxPropertyType.String ? node.Properties[3].AsString() : string.Empty;
        FbxConnection	connection	= new(node.Properties[0].AsString(), childId, parentId, property);

        if (!_parents.TryGetValue(childId, out List<FbxConnection>? parents))
            _parents[childId] = parents = [];

        if (!_children.TryGetValue(parentId, out List<FbxConnection>? children))
            _children[parentId] = children = [];

        parents.Add(connection);
        children.Add(connection);
    }

    private bool TryResolveId(FbxProperty property, out long id)
    {
        if (property.Type == FbxPropertyType.String)
            return _namedIds.TryGetValue(property.AsString(), out id);

        if (property.IsNumber)
        {
            id = property.AsInt64();
            return true;
        }

        id = 0;
        return false;
    }
}
