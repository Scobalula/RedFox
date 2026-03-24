using System.Globalization;
using System.Numerics;
using RedFox.Graphics3D.Buffers;

namespace RedFox.Graphics3D.KaydaraFbx;

/// <summary>
/// Maps between RedFox scene structures and FBX object graphs.
/// </summary>
public static class FbxSceneMapper
{
    private const long RootConnectionId = 0;

    /// <summary>
    /// Imports a scene from an FBX document.
    /// </summary>
    /// <param name="document">The source FBX document.</param>
    /// <param name="sceneName">The destination scene name.</param>
    /// <returns>The imported scene.</returns>
    public static Scene ImportScene(FbxDocument document, string sceneName)
    {
        ArgumentNullException.ThrowIfNull(document);

        Scene scene = new(string.IsNullOrWhiteSpace(sceneName) ? "FBX Scene" : sceneName);
        FbxNode? objectsNode = document.FirstNode("Objects");
        FbxNode? connectionsNode = document.FirstNode("Connections");
        if (objectsNode is null)
        {
            return scene;
        }

        Dictionary<long, FbxNode> objectsById = BuildObjectMap(objectsNode);
        List<FbxConnection> connections = BuildConnections(connectionsNode);
        Skeleton skeletonRoot = scene.RootNode.AddNode(new Skeleton("FbxSkeleton"));
        Model modelRoot = scene.RootNode.AddNode(new Model { Name = "FbxModelRoot" });
        Dictionary<long, SceneNode> modelNodes = [];
        Dictionary<long, Mesh> meshesByModelId = [];
        Dictionary<long, FbxNode> geometryNodes = [];
        Dictionary<long, Material> materialsById = [];
        Dictionary<long, SkeletonBone> bonesByModelId = [];
        Dictionary<long, Camera> camerasByModelId = [];
        Dictionary<long, Light> lightsByModelId = [];
        Dictionary<Mesh, int[]> perTriangleMaterials = [];

        foreach ((long objectId, FbxNode objectNode) in objectsById)
        {
            switch (objectNode.Name)
            {
                case "Model":
                {
                    string modelType = objectNode.Properties.Count > 2 ? objectNode.Properties[2].AsString() : "Null";
                    if (string.Equals(modelType, "Camera", StringComparison.OrdinalIgnoreCase))
                    {
                        CreateCameraNode(objectNode, objectId, modelRoot, modelNodes, camerasByModelId);
                    }
                    else if (string.Equals(modelType, "Light", StringComparison.OrdinalIgnoreCase))
                    {
                        CreateLightNode(objectNode, objectId, modelRoot, modelNodes, lightsByModelId);
                    }
                    else
                    {
                        CreateModelNode(objectNode, objectId, modelRoot, skeletonRoot, modelNodes, meshesByModelId, bonesByModelId);
                    }

                    break;
                }

                case "Geometry":
                    geometryNodes[objectId] = objectNode;
                    break;
                case "Material":
                    CreateMaterialNode(objectNode, objectId, modelRoot, materialsById);
                    break;
            }
        }

        ApplyModelHierarchy(modelNodes, connections);
        AttachGeometry(meshesByModelId, geometryNodes, connections, perTriangleMaterials);
        AttachMaterials(meshesByModelId, materialsById, connections);
        SplitMeshesByMaterial(perTriangleMaterials);
        FbxSkinningMapper.ImportSkinning(meshesByModelId, objectsById, connections, bonesByModelId);
            AttachCameraAndLightNodeAttributes(camerasByModelId, lightsByModelId, objectsById, connections);
        return scene;
    }

    /// <summary>
    /// Exports a scene into an FBX document.
    /// </summary>
    /// <param name="scene">The source scene.</param>
    /// <param name="format">The target FBX format.</param>
    /// <returns>The exported FBX document.</returns>
    public static FbxDocument ExportScene(Scene scene, FbxFormat format)
    {
        ArgumentNullException.ThrowIfNull(scene);

        FbxDocument document = new() { Format = format, Version = 7400 };
        AddHeaderNodes(document);

        FbxNode objectsNode = new("Objects");
        FbxNode connectionsNode = new("Connections");
        document.Nodes.Add(objectsNode);
        document.Nodes.Add(connectionsNode);

        long nextId = 100000;
        Dictionary<SceneNode, long> modelIds = [];
        Dictionary<Mesh, long> geometryIds = [];
        Dictionary<Material, long> materialIds = [];
        Dictionary<SkeletonBone, long> boneIds = [];
        Dictionary<SkeletonBone, long> boneAttributeIds = [];

        Model[] models = scene.GetDescendants<Model>();
        Mesh[] meshes = scene.GetDescendants<Mesh>();
        Material[] materials = scene.GetDescendants<Material>();
        Skeleton[] skeletons = scene.GetDescendants<Skeleton>();
        SkeletonBone[] bones = scene.GetDescendants<SkeletonBone>();
    Camera[] cameras = scene.GetDescendants<Camera>();
    Light[] lights = scene.GetDescendants<Light>();

        for (int i = 0; i < models.Length; i++)
        {
            Model model = models[i];
            long id = nextId++;
            modelIds[model] = id;
            objectsNode.Children.Add(CreateModelObject(id, model.Name, "Null", model));
        }

        for (int i = 0; i < skeletons.Length; i++)
        {
            Skeleton skeleton = skeletons[i];
            if (skeleton is SkeletonBone)
            {
                continue;
            }

            long id = nextId++;
            modelIds[skeleton] = id;
            objectsNode.Children.Add(CreateModelObject(id, skeleton.Name, "Null", skeleton));
        }

        for (int i = 0; i < bones.Length; i++)
        {
            SkeletonBone bone = bones[i];
            long modelId = nextId++;
            long nodeAttributeId = nextId++;
            boneIds[bone] = modelId;
            boneAttributeIds[bone] = nodeAttributeId;
            modelIds[bone] = modelId;
            objectsNode.Children.Add(CreateModelObject(modelId, bone.Name, "LimbNode", bone));
            objectsNode.Children.Add(CreateBoneNodeAttribute(nodeAttributeId, bone.Name));
            AddConnection(connectionsNode, "OO", nodeAttributeId, modelId);
        }

        for (int i = 0; i < meshes.Length; i++)
        {
            Mesh mesh = meshes[i];
            long modelId = nextId++;
            long geometryId = nextId++;
            modelIds[mesh] = modelId;
            geometryIds[mesh] = geometryId;
            objectsNode.Children.Add(CreateModelObject(modelId, mesh.Name, "Mesh", mesh));
            objectsNode.Children.Add(FbxGeometryMapper.ExportGeometry(geometryId, mesh));
            AddConnection(connectionsNode, "OO", geometryId, modelId);
        }

        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            long id = nextId++;
            materialIds[material] = id;
            objectsNode.Children.Add(CreateMaterialObject(id, material));
        }

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            long modelId = nextId++;
            long nodeAttributeId = nextId++;
            modelIds[camera] = modelId;
            objectsNode.Children.Add(CreateModelObject(modelId, camera.Name, "Camera", camera));
            objectsNode.Children.Add(CreateCameraNodeAttribute(nodeAttributeId, camera));
            AddConnection(connectionsNode, "OO", nodeAttributeId, modelId);
        }

        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            long modelId = nextId++;
            long nodeAttributeId = nextId++;
            modelIds[light] = modelId;
            objectsNode.Children.Add(CreateModelObject(modelId, light.Name, "Light", light));
            objectsNode.Children.Add(CreateLightNodeAttribute(nodeAttributeId, light));
            AddConnection(connectionsNode, "OO", nodeAttributeId, modelId);
        }

        for (int i = 0; i < meshes.Length; i++)
        {
            Mesh mesh = meshes[i];
            if (!modelIds.TryGetValue(mesh, out long meshModelId))
            {
                continue;
            }

            if (mesh.Materials is { Count: > 0 } meshMaterials)
            {
                for (int materialIndex = 0; materialIndex < meshMaterials.Count; materialIndex++)
                {
                    Material material = meshMaterials[materialIndex];
                    if (materialIds.TryGetValue(material, out long materialId))
                    {
                        AddConnection(connectionsNode, "OO", materialId, meshModelId);
                    }
                }
            }

            if (mesh.HasSkinning && geometryIds.TryGetValue(mesh, out long geometryId))
            {
                FbxSkinningMapper.ExportSkinning(objectsNode, connectionsNode, mesh, geometryId, boneIds, ref nextId);
                objectsNode.Children.Add(CreateBindPoseObject(nextId++, mesh, meshModelId, modelIds, boneIds));
            }
        }

        foreach ((SceneNode node, long childId) in modelIds)
        {
            long parentId = RootConnectionId;
            if (node.Parent is not null && modelIds.TryGetValue(node.Parent, out long resolvedParentId))
            {
                parentId = resolvedParentId;
            }

            AddConnection(connectionsNode, "OO", childId, parentId);
        }

        _ = boneAttributeIds;
        return document;
    }

    /// <summary>
    /// Adds an FBX connection entry to the connections node.
    /// </summary>
    /// <param name="connectionsNode">The destination connections node.</param>
    /// <param name="type">The connection type token, for example <c>OO</c>.</param>
    /// <param name="childId">The child object identifier.</param>
    /// <param name="parentId">The parent object identifier.</param>
    public static void AddConnection(FbxNode connectionsNode, string type, long childId, long parentId)
    {
        FbxNode connection = new("C");
        connection.Properties.Add(new FbxProperty('S', type));
        connection.Properties.Add(new FbxProperty('L', childId));
        connection.Properties.Add(new FbxProperty('L', parentId));
        connectionsNode.Children.Add(connection);
    }

    /// <summary>
    /// Reads a typed array from a named child node property.
    /// </summary>
    /// <typeparam name="T">The expected element type.</typeparam>
    /// <param name="parent">The parent node.</param>
    /// <param name="childName">The child node name.</param>
    /// <returns>A typed array when available; otherwise an empty array.</returns>
    public static T[] GetNodeArray<T>(FbxNode parent, string childName)
    {
        FbxNode? child = parent.FirstChild(childName);
        if (child is null || child.Properties.Count == 0)
        {
            return [];
        }

        object value = child.Properties[0].Value;
        if (value is T[] typed)
        {
            return typed;
        }

        if (value is not Array array)
        {
            return [];
        }

        T[] converted = new T[array.Length];
        for (int i = 0; i < array.Length; i++)
        {
            converted[i] = (T)Convert.ChangeType(array.GetValue(i)!, typeof(T), CultureInfo.InvariantCulture);
        }

        return converted;
    }

    /// <summary>
    /// Reads a string from the first property of a named child node.
    /// </summary>
    /// <param name="parent">The parent node.</param>
    /// <param name="childName">The child node name.</param>
    /// <returns>The string value when present; otherwise <see langword="null"/>.</returns>
    public static string? GetNodeString(FbxNode parent, string childName)
    {
        FbxNode? child = parent.FirstChild(childName);
        return child is null || child.Properties.Count == 0 ? null : child.Properties[0].AsString();
    }

    /// <summary>
    /// Attaches imported geometry nodes to their corresponding mesh objects by scanning connections.
    /// </summary>
    /// <param name="meshesByModelId">Mesh map keyed by model id.</param>
    /// <param name="geometryNodes">Geometry FBX nodes keyed by object id.</param>
    /// <param name="connections">The full FBX connection list.</param>
    /// <param name="perTriangleMaterials">Output dictionary populated with per-triangle material indices.</param>
    public static void AttachGeometry(Dictionary<long, Mesh> meshesByModelId, Dictionary<long, FbxNode> geometryNodes, IReadOnlyList<FbxConnection> connections, Dictionary<Mesh, int[]> perTriangleMaterials)
    {
        for (int i = 0; i < connections.Count; i++)
        {
            FbxConnection connection = connections[i];
            if (!string.Equals(connection.ConnectionType, "OO", StringComparison.Ordinal) || !geometryNodes.TryGetValue(connection.ChildId, out FbxNode? geometry) || !meshesByModelId.TryGetValue(connection.ParentId, out Mesh? mesh))
            {
                continue;
            }

            int[] materialIndices = FbxGeometryMapper.ImportGeometry(mesh, geometry);
            if (materialIndices.Length > 0)
            {
                perTriangleMaterials[mesh] = materialIndices;
            }
        }
    }

    /// <summary>
    /// Attaches imported material objects to their target meshes by scanning connections.
    /// </summary>
    /// <param name="meshesByModelId">Mesh map keyed by model id.</param>
    /// <param name="materialsById">Material map keyed by object id.</param>
    /// <param name="connections">The full FBX connection list.</param>
    public static void AttachMaterials(Dictionary<long, Mesh> meshesByModelId, Dictionary<long, Material> materialsById, IReadOnlyList<FbxConnection> connections)
    {
        for (int i = 0; i < connections.Count; i++)
        {
            FbxConnection connection = connections[i];
            if (!string.Equals(connection.ConnectionType, "OO", StringComparison.Ordinal) || !materialsById.TryGetValue(connection.ChildId, out Material? material) || !meshesByModelId.TryGetValue(connection.ParentId, out Mesh? mesh))
            {
                continue;
            }

            mesh.Materials ??= [];
            mesh.Materials.Add(material);
        }
    }

    /// <summary>
    /// Splits multi-material meshes into one mesh per material, removing the original.
    /// </summary>
    /// <param name="perTriangleMaterials">Per-triangle material index map for meshes that need splitting.</param>
    public static void SplitMeshesByMaterial(Dictionary<Mesh, int[]> perTriangleMaterials)
    {
        foreach ((Mesh mesh, int[] triangleMaterials) in perTriangleMaterials)
        {
            if (triangleMaterials.Length == 0 || mesh.FaceIndices is null || mesh.Materials is not { Count: > 0 } materials)
            {
                continue;
            }

            int triangleCount = mesh.FaceIndices.ElementCount / 3;
            if (triangleCount == 0)
            {
                continue;
            }

            int firstMaterial = triangleMaterials[0];
            bool singleMaterial = true;
            for (int i = 1; i < Math.Min(triangleCount, triangleMaterials.Length); i++)
            {
                if (triangleMaterials[i] != firstMaterial)
                {
                    singleMaterial = false;
                    break;
                }
            }

            if (singleMaterial)
            {
                mesh.Materials = [materials[Math.Clamp(firstMaterial, 0, materials.Count - 1)]];
                continue;
            }

            SceneNode? parent = mesh.Parent;
            if (parent is null)
            {
                continue;
            }

            Dictionary<int, List<int>> indicesByMaterial = [];
            for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
            {
                int materialIndex = triangleMaterials[Math.Min(triangleIndex, triangleMaterials.Length - 1)];
                if (materialIndex < 0 || materialIndex >= materials.Count)
                {
                    materialIndex = 0;
                }

                if (!indicesByMaterial.TryGetValue(materialIndex, out List<int>? indices))
                {
                    indices = [];
                    indicesByMaterial[materialIndex] = indices;
                }

                int baseIndex = triangleIndex * 3;
                indices.Add(mesh.FaceIndices.Get<int>(baseIndex, 0, 0));
                indices.Add(mesh.FaceIndices.Get<int>(baseIndex + 1, 0, 0));
                indices.Add(mesh.FaceIndices.Get<int>(baseIndex + 2, 0, 0));
            }

            if (indicesByMaterial.Count <= 1)
            {
                continue;
            }

            foreach ((int materialIndex, List<int> triangleIndices) in indicesByMaterial)
            {
                Mesh splitMesh = parent.AddNode(new Mesh { Name = mesh.Name + "_mat" + materialIndex.ToString(CultureInfo.InvariantCulture) });
                splitMesh.Positions = mesh.Positions;
                splitMesh.Normals = mesh.Normals;
                splitMesh.Tangents = mesh.Tangents;
                splitMesh.BiTangents = mesh.BiTangents;
                splitMesh.ColorLayers = mesh.ColorLayers;
                splitMesh.UVLayers = mesh.UVLayers;
                splitMesh.BoneIndices = mesh.BoneIndices;
                splitMesh.BoneWeights = mesh.BoneWeights;
                splitMesh.DeltaPositions = mesh.DeltaPositions;
                splitMesh.DeltaNormals = mesh.DeltaNormals;
                splitMesh.DeltaTangents = mesh.DeltaTangents;
                splitMesh.SkinBindingName = mesh.SkinBindingName;
                splitMesh.Materials = [materials[materialIndex]];
                splitMesh.FaceIndices = new DataBuffer<int>(triangleIndices.ToArray(), 1, 1);

                if (mesh.SkinnedBones is not null)
                {
                    splitMesh.SetSkinBinding(mesh.SkinnedBones, mesh.InverseBindMatrices);
                }
            }

            mesh.Detach();
        }
    }

    /// <summary>
    /// Builds a dictionary of FBX objects keyed by their unique identifier from the Objects node.
    /// </summary>
    /// <param name="objectsNode">The root FBX Objects node.</param>
    /// <returns>A dictionary of FBX nodes keyed by object id.</returns>
    public static Dictionary<long, FbxNode> BuildObjectMap(FbxNode objectsNode)
    {
        Dictionary<long, FbxNode> objectsById = [];
        foreach (FbxNode child in objectsNode.Children)
        {
            if (child.Properties.Count > 0)
            {
                objectsById[child.Properties[0].AsInt64()] = child;
            }
        }

        return objectsById;
    }

    /// <summary>
    /// Parses all connection entries from the FBX Connections node.
    /// </summary>
    /// <param name="connectionsNode">The root FBX Connections node, or <see langword="null"/>.</param>
    /// <returns>A list of parsed <see cref="FbxConnection"/> entries.</returns>
    public static List<FbxConnection> BuildConnections(FbxNode? connectionsNode)
    {
        List<FbxConnection> connections = [];
        if (connectionsNode is null)
        {
            return connections;
        }

        foreach (FbxNode connectionNode in connectionsNode.Children)
        {
            if (!string.Equals(connectionNode.Name, "C", StringComparison.Ordinal) || connectionNode.Properties.Count < 3)
            {
                continue;
            }

            connections.Add(new FbxConnection(connectionNode.Properties[0].AsString(), connectionNode.Properties[1].AsInt64(), connectionNode.Properties[2].AsInt64()));
        }

        return connections;
    }

    /// <summary>
    /// Creates a scene node from an FBX Model object and registers it in the relevant tracking maps.
    /// </summary>
    /// <param name="objectNode">The FBX Model node.</param>
    /// <param name="objectId">The unique object identifier.</param>
    /// <param name="modelRoot">The parent model container for general nodes.</param>
    /// <param name="skeletonRoot">The parent skeleton for bone nodes.</param>
    /// <param name="modelNodes">Map updated with the new scene node.</param>
    /// <param name="meshesByModelId">Map updated when the node is a mesh.</param>
    /// <param name="bonesByModelId">Map updated when the node is a bone.</param>
    public static void CreateModelNode(FbxNode objectNode, long objectId, Model modelRoot, Skeleton skeletonRoot, Dictionary<long, SceneNode> modelNodes, Dictionary<long, Mesh> meshesByModelId, Dictionary<long, SkeletonBone> bonesByModelId)
    {
        string modelName = GetNodeObjectName(objectNode);
        string modelType = objectNode.Properties.Count > 2 ? objectNode.Properties[2].AsString() : "Null";

        if (string.Equals(modelType, "Mesh", StringComparison.OrdinalIgnoreCase))
        {
            Mesh mesh = modelRoot.AddNode(new Mesh { Name = modelName });
            ApplyModelTransform(mesh, objectNode);
            modelNodes[objectId] = mesh;
            meshesByModelId[objectId] = mesh;
            return;
        }

        if (string.Equals(modelType, "LimbNode", StringComparison.OrdinalIgnoreCase) || string.Equals(modelType, "Root", StringComparison.OrdinalIgnoreCase))
        {
            SkeletonBone bone = skeletonRoot.AddNode(new SkeletonBone(modelName));
            ApplyModelTransform(bone, objectNode);
            modelNodes[objectId] = bone;
            bonesByModelId[objectId] = bone;
            return;
        }

        Model container = modelRoot.AddNode(new Model { Name = modelName });
        ApplyModelTransform(container, objectNode);
        modelNodes[objectId] = container;
    }

    /// <summary>
    /// Creates a <see cref="Material"/> scene node from an FBX Material object.
    /// </summary>
    /// <param name="objectNode">The FBX Material node.</param>
    /// <param name="objectId">The unique object identifier.</param>
    /// <param name="modelRoot">The parent container for the new material node.</param>
    /// <param name="materialsById">Map updated with the new material.</param>
    public static void CreateMaterialNode(FbxNode objectNode, long objectId, Model modelRoot, Dictionary<long, Material> materialsById)
    {
        string materialName = GetNodeObjectName(objectNode);
        Material material = modelRoot.AddNode(new Material(materialName));
        ApplyMaterialProperties(material, objectNode);
        materialsById[objectId] = material;
    }

    /// <summary>
    /// Resolves parent–child relationships between already-created scene nodes by scanning connections.
    /// </summary>
    /// <param name="modelNodes">All imported scene nodes keyed by model id.</param>
    /// <param name="connections">The full FBX connection list.</param>
    public static void ApplyModelHierarchy(Dictionary<long, SceneNode> modelNodes, IReadOnlyList<FbxConnection> connections)
    {
        for (int i = 0; i < connections.Count; i++)
        {
            FbxConnection connection = connections[i];
            if (!string.Equals(connection.ConnectionType, "OO", StringComparison.Ordinal) || !modelNodes.TryGetValue(connection.ChildId, out SceneNode? child) || !modelNodes.TryGetValue(connection.ParentId, out SceneNode? parent) || ReferenceEquals(child.Parent, parent))
            {
                continue;
            }

            if (child is SkeletonBone childBone && parent is SkeletonBone parentBone)
            {
                childBone.MoveTo(parentBone, ReparentTransformMode.PreserveExisting);
                continue;
            }

            if (child is Mesh && parent is Model)
            {
                child.MoveTo(parent, ReparentTransformMode.PreserveExisting);
                continue;
            }

            if (child is Model && parent is Model)
            {
                child.MoveTo(parent, ReparentTransformMode.PreserveExisting);
                continue;
            }

            if ((child is Camera || child is Light) && parent is SceneNode)
            {
                child.MoveTo(parent, ReparentTransformMode.PreserveExisting);
            }
        }
    }

    /// <summary>
    /// Extracts the user-facing object name from an FBX node that uses the <c>Name\0\x01Type</c> encoding.
    /// </summary>
    /// <param name="objectNode">The FBX node whose second property contains the encoded name.</param>
    /// <returns>The decoded object name.</returns>
    public static string GetNodeObjectName(FbxNode objectNode)
    {
        if (objectNode.Properties.Count < 2)
        {
            return objectNode.Name;
        }

        string rawName = objectNode.Properties[1].AsString();
        int terminatorIndex = rawName.IndexOf('\0');
        return terminatorIndex >= 0 ? rawName[..terminatorIndex] : rawName;
    }

    /// <summary>
    /// Reads and applies the local transform from a Model node's Properties70 to the given scene node.
    /// </summary>
    /// <param name="node">The target scene node.</param>
    /// <param name="modelObject">The source FBX Model node.</param>
    public static void ApplyModelTransform(SceneNode node, FbxNode modelObject)
    {
        FbxNode? properties70 = modelObject.FirstChild("Properties70");
        if (properties70 is null)
        {
            return;
        }

        Vector3 localTranslation = GetPropertyVector3(properties70, "Lcl Translation", Vector3.Zero);
        Vector3 localRotation = GetPropertyVector3(properties70, "Lcl Rotation", Vector3.Zero);
        Vector3 localScale = GetPropertyVector3(properties70, "Lcl Scaling", Vector3.One);
        Vector3 preRotation = GetPropertyVector3(properties70, "PreRotation", Vector3.Zero);
        Vector3 postRotation = GetPropertyVector3(properties70, "PostRotation", Vector3.Zero);
        Vector3 rotationOffset = GetPropertyVector3(properties70, "RotationOffset", Vector3.Zero);
        Vector3 rotationPivot = GetPropertyVector3(properties70, "RotationPivot", Vector3.Zero);
        Vector3 scalingOffset = GetPropertyVector3(properties70, "ScalingOffset", Vector3.Zero);
        Vector3 scalingPivot = GetPropertyVector3(properties70, "ScalingPivot", Vector3.Zero);
        Vector3 geometricTranslation = GetPropertyVector3(properties70, "GeometricTranslation", Vector3.Zero);
        Vector3 geometricRotation = GetPropertyVector3(properties70, "GeometricRotation", Vector3.Zero);
        Vector3 geometricScaling = GetPropertyVector3(properties70, "GeometricScaling", Vector3.One);
        int rotationOrder = GetPropertyInt(properties70, "RotationOrder", 0);

        Matrix4x4 localMatrix = ComposeNodeLocalTransform(localTranslation, localRotation, localScale, preRotation, postRotation, rotationOffset, rotationPivot, scalingOffset, scalingPivot, rotationOrder);
        if (node is Mesh)
        {
            Matrix4x4 geometricMatrix = ComposeGeometricTransform(geometricTranslation, geometricRotation, geometricScaling, rotationOrder);
            localMatrix *= geometricMatrix;
        }

        if (!Matrix4x4.Decompose(localMatrix, out Vector3 resolvedScale, out Quaternion resolvedRotation, out Vector3 resolvedTranslation))
        {
            resolvedScale = localScale;
            resolvedRotation = ComposeEulerRotation(localRotation, rotationOrder);
            resolvedTranslation = localTranslation;
        }

        node.BindTransform.LocalPosition = resolvedTranslation;
        node.BindTransform.LocalRotation = Quaternion.Normalize(resolvedRotation);
        node.BindTransform.Scale = resolvedScale;
    }

    /// <summary>
    /// Reads material colour properties from a Material node's Properties70 and applies them.
    /// </summary>
    /// <param name="material">The target material.</param>
    /// <param name="materialNode">The source FBX Material node.</param>
    public static void ApplyMaterialProperties(Material material, FbxNode materialNode)
    {
        FbxNode? properties70 = materialNode.FirstChild("Properties70");
        if (properties70 is null)
        {
            return;
        }

        foreach (FbxNode propertyNode in properties70.ChildrenNamed("P"))
        {
            if (propertyNode.Properties.Count < 7)
            {
                continue;
            }

            string propertyName = propertyNode.Properties[0].AsString();
            Vector4 value = new((float)propertyNode.Properties[4].AsDouble(), (float)propertyNode.Properties[5].AsDouble(), (float)propertyNode.Properties[6].AsDouble(), 1f);
            if (string.Equals(propertyName, "DiffuseColor", StringComparison.OrdinalIgnoreCase))
            {
                material.DiffuseColor = value;
                continue;
            }

            if (string.Equals(propertyName, "EmissiveColor", StringComparison.OrdinalIgnoreCase))
            {
                material.EmissiveColor = value;
                continue;
            }

            if (string.Equals(propertyName, "SpecularColor", StringComparison.OrdinalIgnoreCase))
            {
                material.SpecularColor = value;
            }
        }
    }

    /// <summary>
    /// Appends the FBX header and global settings nodes to the document.
    /// </summary>
    /// <param name="document">The target FBX document.</param>
    public static void AddHeaderNodes(FbxDocument document)
    {
        FbxNode header = new("FBXHeaderExtension");
        header.Children.Add(new FbxNode("FBXHeaderVersion") { Properties = { new FbxProperty('I', 1003) } });
        header.Children.Add(new FbxNode("FBXVersion") { Properties = { new FbxProperty('I', 7400) } });
        header.Children.Add(new FbxNode("Creator") { Properties = { new FbxProperty('S', "RedFox Graphics3D Kaydara FBX") } });
        document.Nodes.Add(header);

        FbxNode globalSettings = new("GlobalSettings");
        FbxNode globalProperties = globalSettings.AddChild("Properties70");
        AddGlobalProperty(globalProperties, "UpAxis", "int", new FbxProperty('I', 2));
        AddGlobalProperty(globalProperties, "UpAxisSign", "int", new FbxProperty('I', 1));
        AddGlobalProperty(globalProperties, "FrontAxis", "int", new FbxProperty('I', 1));
        AddGlobalProperty(globalProperties, "FrontAxisSign", "int", new FbxProperty('I', -1));
        AddGlobalProperty(globalProperties, "CoordAxis", "int", new FbxProperty('I', 0));
        AddGlobalProperty(globalProperties, "CoordAxisSign", "int", new FbxProperty('I', 1));
        AddGlobalProperty(globalProperties, "OriginalUpAxis", "int", new FbxProperty('I', -1));
        AddGlobalProperty(globalProperties, "OriginalUpAxisSign", "int", new FbxProperty('I', 1));
        AddGlobalProperty(globalProperties, "UnitScaleFactor", "double", new FbxProperty('D', 100.0));
        AddGlobalProperty(globalProperties, "OriginalUnitScaleFactor", "double", new FbxProperty('D', 100.0));
        document.Nodes.Add(globalSettings);
    }

    /// <summary>
    /// Appends a typed property entry to a Properties70 node.
    /// </summary>
    /// <param name="properties">The Properties70 node to append to.</param>
    /// <param name="name">The property name.</param>
    /// <param name="type">The FBX property type string.</param>
    /// <param name="value">The value property.</param>
    public static void AddGlobalProperty(FbxNode properties, string name, string type, FbxProperty value)
    {
        FbxNode property = properties.AddChild("P");
        property.Properties.Add(new FbxProperty('S', name));
        property.Properties.Add(new FbxProperty('S', type));
        property.Properties.Add(new FbxProperty('S', string.Empty));
        property.Properties.Add(new FbxProperty('S', string.Empty));
        property.Properties.Add(value);
    }

    /// <summary>
    /// Creates an FBX Model node for export with the specified type and transform.
    /// </summary>
    /// <param name="id">The FBX object identifier.</param>
    /// <param name="name">The model name.</param>
    /// <param name="type">The FBX Model type string (e.g. <c>Null</c>, <c>Mesh</c>, <c>LimbNode</c>).</param>
    /// <param name="sourceNode">The source scene node providing the local transform.</param>
    /// <returns>The generated FBX Model node.</returns>
    public static FbxNode CreateModelObject(long id, string name, string type, SceneNode sourceNode)
    {
        FbxNode modelNode = new("Model");
        modelNode.Properties.Add(new FbxProperty('L', id));
        modelNode.Properties.Add(new FbxProperty('S', name + "\0\u0001Model"));
        modelNode.Properties.Add(new FbxProperty('S', type));
        FbxNode properties = modelNode.AddChild("Properties70");
        (Vector3 localTranslation, Quaternion localRotation, Vector3 localScale) = ResolveExportLocalTransform(sourceNode);
        AddVectorProperty(properties, "Lcl Translation", "Lcl Translation", localTranslation);
        AddVectorProperty(properties, "Lcl Rotation", "Lcl Rotation", FbxRotation.ToEulerDegreesXyz(localRotation));
        AddVectorProperty(properties, "Lcl Scaling", "Lcl Scaling", localScale);
        AddVectorProperty(properties, "PreRotation", "Vector3D", Vector3.Zero);
        AddVectorProperty(properties, "PostRotation", "Vector3D", Vector3.Zero);
        AddVectorProperty(properties, "RotationOffset", "Vector3D", Vector3.Zero);
        AddVectorProperty(properties, "RotationPivot", "Vector3D", Vector3.Zero);
        AddVectorProperty(properties, "ScalingOffset", "Vector3D", Vector3.Zero);
        AddVectorProperty(properties, "ScalingPivot", "Vector3D", Vector3.Zero);
        AddVectorProperty(properties, "GeometricTranslation", "Vector3D", Vector3.Zero);
        AddVectorProperty(properties, "GeometricRotation", "Vector3D", Vector3.Zero);
        AddVectorProperty(properties, "GeometricScaling", "Vector3D", Vector3.One);
        AddIntProperty(properties, "RotationOrder", "enum", 0);
        AddIntProperty(properties, "InheritType", "enum", 1);
        return modelNode;
    }

    /// <summary>
    /// Creates an FBX NodeAttribute node for a skeleton limb bone.
    /// </summary>
    /// <param name="id">The FBX object identifier.</param>
    /// <param name="boneName">The bone name.</param>
    /// <returns>The generated NodeAttribute node.</returns>
    public static FbxNode CreateBoneNodeAttribute(long id, string boneName)
    {
        FbxNode nodeAttribute = new("NodeAttribute");
        nodeAttribute.Properties.Add(new FbxProperty('L', id));
        nodeAttribute.Properties.Add(new FbxProperty('S', boneName + "\0\u0001NodeAttribute"));
        nodeAttribute.Properties.Add(new FbxProperty('S', "LimbNode"));
        FbxNode properties = nodeAttribute.AddChild("Properties70");
        AddGlobalProperty(properties, "Size", "double", new FbxProperty('D', 1.0));
        return nodeAttribute;
    }

    /// <summary>
    /// Creates an FBX Material node for export.
    /// </summary>
    /// <param name="id">The FBX object identifier.</param>
    /// <param name="material">The source material.</param>
    /// <returns>The generated FBX Material node.</returns>
    public static FbxNode CreateMaterialObject(long id, Material material)
    {
        FbxNode materialNode = new("Material");
        materialNode.Properties.Add(new FbxProperty('L', id));
        materialNode.Properties.Add(new FbxProperty('S', material.Name + "\0\u0001Material"));
        materialNode.Properties.Add(new FbxProperty('S', string.Empty));
        FbxNode properties = materialNode.AddChild("Properties70");
        Vector4 diffuse = material.DiffuseColor ?? Vector4.One;
        AddVectorProperty(properties, "DiffuseColor", "Color", new Vector3(diffuse.X, diffuse.Y, diffuse.Z));
        Vector4 emissive = material.EmissiveColor ?? Vector4.Zero;
        AddVectorProperty(properties, "EmissiveColor", "Color", new Vector3(emissive.X, emissive.Y, emissive.Z));
        Vector4 specular = material.SpecularColor ?? Vector4.Zero;
        AddVectorProperty(properties, "SpecularColor", "Color", new Vector3(specular.X, specular.Y, specular.Z));
        return materialNode;
    }

    /// <summary>
    /// Creates an FBX BindPose node for the given mesh and its skinned bone palette.
    /// </summary>
    /// <param name="id">The FBX object identifier.</param>
    /// <param name="mesh">The source mesh.</param>
    /// <param name="meshModelId">The FBX model id of the mesh.</param>
    /// <param name="modelIds">All exported model ids keyed by scene node.</param>
    /// <param name="boneIds">All exported bone model ids keyed by skeleton bone.</param>
    /// <returns>The generated FBX Pose node.</returns>
    public static FbxNode CreateBindPoseObject(long id, Mesh mesh, long meshModelId, Dictionary<SceneNode, long> modelIds, Dictionary<SkeletonBone, long> boneIds)
    {
        FbxNode poseNode = new("Pose");
        poseNode.Properties.Add(new FbxProperty('L', id));
        poseNode.Properties.Add(new FbxProperty('S', mesh.Name + "_BindPose\0\u0001Pose"));
        poseNode.Properties.Add(new FbxProperty('S', "BindPose"));
        poseNode.Children.Add(new FbxNode("Type") { Properties = { new FbxProperty('S', "BindPose") } });
        poseNode.Children.Add(new FbxNode("Version") { Properties = { new FbxProperty('I', 100) } });

        Skeleton? armature = FindBindPoseArmature(mesh);
        bool hasArmature = armature is not null && modelIds.ContainsKey(armature);
        int poseNodeCount = hasArmature ? 2 : 1;
        if (mesh.SkinnedBones is { Count: > 0 } skinnedBones)
        {
            poseNodeCount += skinnedBones.Count;
        }

        poseNode.Children.Add(new FbxNode("NbPoseNodes") { Properties = { new FbxProperty('I', poseNodeCount) } });
        poseNode.Children.Add(CreatePoseEntry(meshModelId, mesh.GetBindWorldMatrix()));

        if (hasArmature && armature is not null)
        {
            poseNode.Children.Add(CreatePoseEntry(modelIds[armature], armature.GetBindWorldMatrix()));
        }

        if (mesh.SkinnedBones is { Count: > 0 } bones)
        {
            for (int i = 0; i < bones.Count; i++)
            {
                SkeletonBone bone = bones[i];
                if (boneIds.TryGetValue(bone, out long boneModelId))
                {
                    poseNode.Children.Add(CreatePoseEntry(boneModelId, bone.GetBindWorldMatrix()));
                }
            }
        }

        return poseNode;
    }

    /// <summary>
    /// Creates an FBX PoseNode entry referencing the specified node id and its bind-world matrix.
    /// </summary>
    /// <param name="nodeId">The FBX node identifier.</param>
    /// <param name="matrix">The bind-world matrix for the node.</param>
    /// <returns>The generated PoseNode child node.</returns>
    public static FbxNode CreatePoseEntry(long nodeId, Matrix4x4 matrix)
    {
        FbxNode poseEntry = new("PoseNode");
        poseEntry.Children.Add(new FbxNode("Node") { Properties = { new FbxProperty('L', nodeId) } });
        poseEntry.Children.Add(new FbxNode("Matrix") { Properties = { new FbxProperty('d', MatrixToArray(matrix)) } });
        return poseEntry;
    }

    /// <summary>
    /// Resolves the local transform to export for a scene node by computing
    /// <c>worldMatrix * parentWorldInverse</c>.
    /// </summary>
    /// <param name="sourceNode">The scene node to query.</param>
    /// <returns>A tuple of translation, rotation, and scale in local space.</returns>
    public static (Vector3 Translation, Quaternion Rotation, Vector3 Scale) ResolveExportLocalTransform(SceneNode sourceNode)
    {
        Matrix4x4 localMatrix = sourceNode.GetBindWorldMatrix();
        if (sourceNode.Parent is not null)
        {
            Matrix4x4 parentWorld = sourceNode.Parent.GetBindWorldMatrix();
            if (Matrix4x4.Invert(parentWorld, out Matrix4x4 parentInverse))
            {
                localMatrix *= parentInverse;
            }
        }

        if (Matrix4x4.Decompose(localMatrix, out Vector3 scale, out Quaternion rotation, out Vector3 translation))
        {
            return (translation, Quaternion.Normalize(rotation), scale);
        }

        return (sourceNode.GetBindLocalPosition(), sourceNode.GetBindLocalRotation(), sourceNode.GetBindLocalScale());
    }

    /// <summary>
    /// Walks a mesh's skinned bone parent chain to locate the owning <see cref="Skeleton"/> (armature).
    /// </summary>
    /// <param name="mesh">The skinned mesh to inspect.</param>
    /// <returns>The owning skeleton node, or <see langword="null"/> when not found.</returns>
    public static Skeleton? FindBindPoseArmature(Mesh mesh)
    {
        if (mesh.SkinnedBones is not { Count: > 0 } bones)
        {
            return null;
        }

        SceneNode? current = bones[0].Parent;
        while (current is not null)
        {
            if (current is Skeleton skeleton && current is not SkeletonBone)
            {
                return skeleton;
            }

            current = current.Parent;
        }

        return null;
    }

    /// <summary>
    /// Appends a Vector3-typed Properties70 entry to the given properties node.
    /// </summary>
    /// <param name="properties">The Properties70 node to append to.</param>
    /// <param name="propertyName">The FBX property name.</param>
    /// <param name="propertyType">The FBX property type string.</param>
    /// <param name="value">The XYZ value to write.</param>
    public static void AddVectorProperty(FbxNode properties, string propertyName, string propertyType, Vector3 value)
    {
        FbxNode property = properties.AddChild("P");
        property.Properties.Add(new FbxProperty('S', propertyName));
        property.Properties.Add(new FbxProperty('S', propertyType));
        property.Properties.Add(new FbxProperty('S', string.Empty));
        property.Properties.Add(new FbxProperty('S', "A"));
        property.Properties.Add(new FbxProperty('D', value.X));
        property.Properties.Add(new FbxProperty('D', value.Y));
        property.Properties.Add(new FbxProperty('D', value.Z));
    }

    /// <summary>
    /// Appends an integer-typed Properties70 entry to the given properties node.
    /// </summary>
    /// <param name="properties">The Properties70 node to append to.</param>
    /// <param name="propertyName">The FBX property name.</param>
    /// <param name="propertyType">The FBX property type string.</param>
    /// <param name="value">The integer value to write.</param>
    public static void AddIntProperty(FbxNode properties, string propertyName, string propertyType, int value)
    {
        FbxNode property = properties.AddChild("P");
        property.Properties.Add(new FbxProperty('S', propertyName));
        property.Properties.Add(new FbxProperty('S', propertyType));
        property.Properties.Add(new FbxProperty('S', string.Empty));
        property.Properties.Add(new FbxProperty('S', string.Empty));
        property.Properties.Add(new FbxProperty('I', value));
    }

    /// <summary>
    /// Reads a Vector3 value from a named typed property in a Properties70 node.
    /// </summary>
    /// <param name="properties70">The Properties70 node to search.</param>
    /// <param name="name">The property name to locate.</param>
    /// <param name="defaultValue">The fallback value when the property is absent.</param>
    /// <returns>The resolved Vector3 or <paramref name="defaultValue"/>.</returns>
    public static Vector3 GetPropertyVector3(FbxNode properties70, string name, Vector3 defaultValue)
    {
        foreach (FbxNode propertyNode in properties70.ChildrenNamed("P"))
        {
            if (propertyNode.Properties.Count < 7 || !string.Equals(propertyNode.Properties[0].AsString(), name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return new Vector3((float)propertyNode.Properties[4].AsDouble(), (float)propertyNode.Properties[5].AsDouble(), (float)propertyNode.Properties[6].AsDouble());
        }

        return defaultValue;
    }

    /// <summary>
    /// Reads an integer value from a named typed property in a Properties70 node.
    /// </summary>
    /// <param name="properties70">The Properties70 node to search.</param>
    /// <param name="name">The property name to locate.</param>
    /// <param name="defaultValue">The fallback value when the property is absent.</param>
    /// <returns>The resolved integer or <paramref name="defaultValue"/>.</returns>
    public static int GetPropertyInt(FbxNode properties70, string name, int defaultValue)
    {
        foreach (FbxNode propertyNode in properties70.ChildrenNamed("P"))
        {
            if (propertyNode.Properties.Count < 5 || !string.Equals(propertyNode.Properties[0].AsString(), name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return (int)propertyNode.Properties[4].AsInt64();
        }

        return defaultValue;
    }

    /// <summary>
    /// Composes the full FBX node local transform matrix from its TRS and pivot components.
    /// </summary>
    /// <param name="translation">Local translation vector.</param>
    /// <param name="rotation">Local rotation in Euler degrees.</param>
    /// <param name="scaling">Local scale vector.</param>
    /// <param name="preRotation">Pre-rotation in Euler degrees.</param>
    /// <param name="postRotation">Post-rotation in Euler degrees.</param>
    /// <param name="rotationOffset">Rotation offset translation.</param>
    /// <param name="rotationPivot">Rotation pivot translation.</param>
    /// <param name="scalingOffset">Scaling offset translation.</param>
    /// <param name="scalingPivot">Scaling pivot translation.</param>
    /// <param name="rotationOrder">FBX rotation order index.</param>
    /// <returns>The composed local transform matrix.</returns>
    public static Matrix4x4 ComposeNodeLocalTransform(Vector3 translation, Vector3 rotation, Vector3 scaling, Vector3 preRotation, Vector3 postRotation, Vector3 rotationOffset, Vector3 rotationPivot, Vector3 scalingOffset, Vector3 scalingPivot, int rotationOrder)
    {
        Matrix4x4 t = Matrix4x4.CreateTranslation(translation);
        Matrix4x4 roff = Matrix4x4.CreateTranslation(rotationOffset);
        Matrix4x4 rp = Matrix4x4.CreateTranslation(rotationPivot);
        Matrix4x4 rpInv = Matrix4x4.CreateTranslation(-rotationPivot);
        Matrix4x4 soff = Matrix4x4.CreateTranslation(scalingOffset);
        Matrix4x4 sp = Matrix4x4.CreateTranslation(scalingPivot);
        Matrix4x4 spInv = Matrix4x4.CreateTranslation(-scalingPivot);
        Matrix4x4 pre = Matrix4x4.CreateFromQuaternion(ComposeEulerRotation(preRotation, rotationOrder));
        Matrix4x4 localRot = Matrix4x4.CreateFromQuaternion(ComposeEulerRotation(rotation, rotationOrder));
        Matrix4x4 post = Matrix4x4.CreateFromQuaternion(ComposeEulerRotation(postRotation, rotationOrder));
        Matrix4x4 postInv = Matrix4x4.Invert(post, out Matrix4x4 invertedPost) ? invertedPost : Matrix4x4.Identity;
        Matrix4x4 s = Matrix4x4.CreateScale(scaling);
        return t * roff * rp * pre * localRot * postInv * rpInv * soff * sp * s * spInv;
    }

    /// <summary>
    /// Composes the FBX geometric offset transform matrix from its TRS components.
    /// </summary>
    /// <param name="translation">Geometric translation.</param>
    /// <param name="rotation">Geometric rotation in Euler degrees.</param>
    /// <param name="scaling">Geometric scale.</param>
    /// <param name="rotationOrder">FBX rotation order index.</param>
    /// <returns>The composed geometric transform matrix.</returns>
    public static Matrix4x4 ComposeGeometricTransform(Vector3 translation, Vector3 rotation, Vector3 scaling, int rotationOrder)
    {
        Matrix4x4 t = Matrix4x4.CreateTranslation(translation);
        Matrix4x4 r = Matrix4x4.CreateFromQuaternion(ComposeEulerRotation(rotation, rotationOrder));
        Matrix4x4 s = Matrix4x4.CreateScale(scaling);
        return t * r * s;
    }

    /// <summary>
    /// Composes a quaternion from Euler degrees applying the FBX rotation order.
    /// </summary>
    /// <param name="eulerDegrees">Euler rotation in degrees.</param>
    /// <param name="rotationOrder">FBX rotation order integer (0=XYZ, 1=XZY, 2=YXZ, 3=YZX, 4=ZXY, 5=ZYX).</param>
    /// <returns>The normalised composed quaternion.</returns>
    public static Quaternion ComposeEulerRotation(Vector3 eulerDegrees, int rotationOrder)
    {
        Quaternion qx = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI * eulerDegrees.X / 180f);
        Quaternion qy = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI * eulerDegrees.Y / 180f);
        Quaternion qz = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI * eulerDegrees.Z / 180f);

        Quaternion composed = rotationOrder switch
        {
            1 => qy * qz * qx,
            2 => qx * qz * qy,
            3 => qz * qx * qy,
            4 => qy * qx * qz,
            5 => qx * qy * qz,
            _ => qz * qy * qx,
        };

        return Quaternion.Normalize(composed);
    }

    /// <summary>
    /// Flattens a <see cref="Matrix4x4"/> into a row-major double array for FBX serialisation.
    /// </summary>
    /// <param name="matrix">The source matrix.</param>
    /// <returns>A sixteen-element double array in row-major order.</returns>
    public static double[] MatrixToArray(Matrix4x4 matrix)
    {
        return
        [
            matrix.M11, matrix.M12, matrix.M13, matrix.M14,
            matrix.M21, matrix.M22, matrix.M23, matrix.M24,
            matrix.M31, matrix.M32, matrix.M33, matrix.M34,
            matrix.M41, matrix.M42, matrix.M43, matrix.M44,
        ];
    }

    /// <summary>
    /// Creates a <see cref="Camera"/> scene node from an FBX Model object and registers it.
    /// </summary>
    /// <param name="objectNode">The FBX Model node with type Camera.</param>
    /// <param name="objectId">The unique object identifier.</param>
    /// <param name="modelRoot">The parent container for the new camera node.</param>
    /// <param name="modelNodes">Map updated with the new camera node.</param>
    /// <param name="camerasByModelId">Map updated with the new camera.</param>
    public static void CreateCameraNode(FbxNode objectNode, long objectId, Model modelRoot, Dictionary<long, SceneNode> modelNodes, Dictionary<long, Camera> camerasByModelId)
    {
        string name = GetNodeObjectName(objectNode);
        Camera camera = modelRoot.AddNode(new Camera(name));
        ApplyModelTransform(camera, objectNode);
        ApplyCameraProperties(camera, objectNode);
        modelNodes[objectId] = camera;
        camerasByModelId[objectId] = camera;
    }

    /// <summary>
    /// Creates a <see cref="Light"/> scene node from an FBX Model object and registers it.
    /// </summary>
    /// <param name="objectNode">The FBX Model node with type Light.</param>
    /// <param name="objectId">The unique object identifier.</param>
    /// <param name="modelRoot">The parent container for the new light node.</param>
    /// <param name="modelNodes">Map updated with the new light node.</param>
    /// <param name="lightsByModelId">Map updated with the new light.</param>
    public static void CreateLightNode(FbxNode objectNode, long objectId, Model modelRoot, Dictionary<long, SceneNode> modelNodes, Dictionary<long, Light> lightsByModelId)
    {
        string name = GetNodeObjectName(objectNode);
        Light light = modelRoot.AddNode(new Light(name));
        ApplyModelTransform(light, objectNode);
        ApplyLightProperties(light, objectNode);
        modelNodes[objectId] = light;
        lightsByModelId[objectId] = light;
    }

    /// <summary>
    /// Reads camera properties from an FBX node's Properties70 and applies them to a <see cref="Camera"/>.
    /// </summary>
    /// <param name="camera">The target camera.</param>
    /// <param name="objectNode">The source FBX node (Model or NodeAttribute).</param>
    public static void ApplyCameraProperties(Camera camera, FbxNode objectNode)
    {
        FbxNode? properties70 = objectNode.FirstChild("Properties70");
        if (properties70 is null)
        {
            return;
        }

        camera.FieldOfView = (float)GetPropertyDouble(properties70, "FieldOfView", camera.FieldOfView);
        camera.NearPlane = (float)GetPropertyDouble(properties70, "NearPlane", camera.NearPlane);
        camera.FarPlane = (float)GetPropertyDouble(properties70, "FarPlane", camera.FarPlane);
        camera.AspectRatio = (float)GetPropertyDouble(properties70, "FilmAspectRatio", camera.AspectRatio);
        camera.OrthographicSize = (float)GetPropertyDouble(properties70, "OrthoZoom", camera.OrthographicSize);
        int projectionType = GetPropertyInt(properties70, "CameraProjectionType", 0);
        camera.Projection = projectionType == 1 ? CameraProjection.Orthographic : CameraProjection.Perspective;
    }

    /// <summary>
    /// Reads light properties from an FBX node's Properties70 and applies them to a <see cref="Light"/>.
    /// </summary>
    /// <param name="light">The target light.</param>
    /// <param name="objectNode">The source FBX node (Model or NodeAttribute).</param>
    public static void ApplyLightProperties(Light light, FbxNode objectNode)
    {
        FbxNode? properties70 = objectNode.FirstChild("Properties70");
        if (properties70 is null)
        {
            return;
        }

        Vector3 color = GetPropertyVector3(properties70, "Color", Vector3.One);
        light.Color = color;
        double intensity = GetPropertyDouble(properties70, "Intensity", 100.0);
        light.Intensity = (float)(intensity / 100.0);
        light.Enabled = GetPropertyInt(properties70, "CastLight", 1) != 0;
    }

    /// <summary>
    /// Scans FBX connections for NodeAttribute objects connected to cameras and lights and applies their properties.
    /// </summary>
    /// <param name="camerasByModelId">Camera nodes keyed by model id.</param>
    /// <param name="lightsByModelId">Light nodes keyed by model id.</param>
    /// <param name="objectsById">All FBX objects keyed by id.</param>
    /// <param name="connections">The full FBX connection list.</param>
    public static void AttachCameraAndLightNodeAttributes(Dictionary<long, Camera> camerasByModelId, Dictionary<long, Light> lightsByModelId, Dictionary<long, FbxNode> objectsById, IReadOnlyList<FbxConnection> connections)
    {
        for (int i = 0; i < connections.Count; i++)
        {
            FbxConnection connection = connections[i];
            if (!string.Equals(connection.ConnectionType, "OO", StringComparison.Ordinal))
            {
                continue;
            }

            if (!objectsById.TryGetValue(connection.ChildId, out FbxNode? nodeAttr) || !string.Equals(nodeAttr.Name, "NodeAttribute", StringComparison.Ordinal))
            {
                continue;
            }

            if (camerasByModelId.TryGetValue(connection.ParentId, out Camera? camera))
            {
                ApplyCameraProperties(camera, nodeAttr);
                continue;
            }

            if (lightsByModelId.TryGetValue(connection.ParentId, out Light? light))
            {
                ApplyLightProperties(light, nodeAttr);
            }
        }
    }

    /// <summary>
    /// Reads a double value from a named typed property in a Properties70 node.
    /// </summary>
    /// <param name="properties70">The Properties70 node to search.</param>
    /// <param name="name">The property name to locate.</param>
    /// <param name="defaultValue">The fallback value when the property is absent.</param>
    /// <returns>The resolved double or <paramref name="defaultValue"/>.</returns>
    public static double GetPropertyDouble(FbxNode properties70, string name, double defaultValue)
    {
        foreach (FbxNode propertyNode in properties70.ChildrenNamed("P"))
        {
            if (propertyNode.Properties.Count < 5 || !string.Equals(propertyNode.Properties[0].AsString(), name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return propertyNode.Properties[4].AsDouble();
        }

        return defaultValue;
    }

    /// <summary>
    /// Appends a double-typed Properties70 entry to the given properties node.
    /// </summary>
    /// <param name="properties">The Properties70 node to append to.</param>
    /// <param name="propertyName">The FBX property name.</param>
    /// <param name="propertyType">The FBX property type string.</param>
    /// <param name="value">The double value to write.</param>
    public static void AddDoubleProperty(FbxNode properties, string propertyName, string propertyType, double value)
    {
        FbxNode property = properties.AddChild("P");
        property.Properties.Add(new FbxProperty('S', propertyName));
        property.Properties.Add(new FbxProperty('S', propertyType));
        property.Properties.Add(new FbxProperty('S', string.Empty));
        property.Properties.Add(new FbxProperty('S', "A"));
        property.Properties.Add(new FbxProperty('D', value));
    }

    /// <summary>
    /// Creates an FBX NodeAttribute node for a camera.
    /// </summary>
    /// <param name="id">The FBX object identifier.</param>
    /// <param name="camera">The source camera.</param>
    /// <returns>The generated NodeAttribute node.</returns>
    public static FbxNode CreateCameraNodeAttribute(long id, Camera camera)
    {
        FbxNode attr = new("NodeAttribute");
        attr.Properties.Add(new FbxProperty('L', id));
        attr.Properties.Add(new FbxProperty('S', camera.Name + "\0\u0001NodeAttribute"));
        attr.Properties.Add(new FbxProperty('S', "Camera"));
        FbxNode properties = attr.AddChild("Properties70");
        AddDoubleProperty(properties, "FilmAspectRatio", "double", camera.AspectRatio);
        AddDoubleProperty(properties, "FieldOfView", "FieldOfView", camera.FieldOfView);
        AddDoubleProperty(properties, "NearPlane", "double", camera.NearPlane);
        AddDoubleProperty(properties, "FarPlane", "double", camera.FarPlane);
        AddIntProperty(properties, "CameraProjectionType", "enum", camera.Projection == CameraProjection.Orthographic ? 1 : 0);
        AddDoubleProperty(properties, "OrthoZoom", "double", camera.OrthographicSize);
        return attr;
    }

    /// <summary>
    /// Creates an FBX NodeAttribute node for a light.
    /// </summary>
    /// <param name="id">The FBX object identifier.</param>
    /// <param name="light">The source light.</param>
    /// <returns>The generated NodeAttribute node.</returns>
    public static FbxNode CreateLightNodeAttribute(long id, Light light)
    {
        FbxNode attr = new("NodeAttribute");
        attr.Properties.Add(new FbxProperty('L', id));
        attr.Properties.Add(new FbxProperty('S', light.Name + "\0\u0001NodeAttribute"));
        attr.Properties.Add(new FbxProperty('S', "Light"));
        FbxNode properties = attr.AddChild("Properties70");
        AddVectorProperty(properties, "Color", "Color", light.Color);
        AddDoubleProperty(properties, "Intensity", "Number", light.Intensity * 100.0);
        AddIntProperty(properties, "CastLight", "bool", light.Enabled ? 1 : 0);
        AddIntProperty(properties, "LightType", "enum", 0);
        return attr;
    }
}
