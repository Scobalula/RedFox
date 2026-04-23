using System.Globalization;
using System.Numerics;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.IO;

namespace RedFox.Graphics3D.KaydaraFbx;

/// <summary>
/// Maps between RedFox scene structures and FBX object graphs.
/// </summary>
public static class FbxSceneMapper
{
    /// <summary>
    /// Gets the root connection id used by FBX object graphs.
    /// </summary>
    public const long RootConnectionId = 0;

    /// <summary>
    /// Gets the default take name emitted for exported FBX scenes.
    /// </summary>
    public const string DefaultTakeName = "Take 001";

    /// <summary>
    /// Gets the default creation time string emitted for scene metadata.
    /// </summary>
    public const string DefaultFileCreationTime = "2026-03-24 15:33:49:359";



    /// <summary>
    /// Gets the pre-rotation applied to authored top-level export roots.
    /// </summary>
    public static readonly Vector3 s_authoredRootPreRotation = new(-90f, 0f, 0f);

    /// <summary>
    /// Gets the basis transform applied to authored top-level export roots.
    /// </summary>
    public static readonly Matrix4x4 s_authoredRootBasis = Matrix4x4.CreateRotationX(-MathF.PI / 2f);

    /// <summary>
    /// Gets the FBX import basis that converts FBX Y-up world transforms into RedFox Z-up.
    /// </summary>
    public static readonly Matrix4x4 s_importBasis = Matrix4x4.CreateRotationX(MathF.PI / 2f);

    /// <summary>
    /// Gets the default scene FileId used for RedFox-authored exports.
    /// </summary>
    public static readonly byte[] s_fileId =
    [
        0x2C, 0xBE, 0x27, 0xE4, 0xB9, 0x2E, 0xC4, 0xCF,
        0xB1, 0xC3, 0xB8, 0x2B, 0xAD, 0x29, 0xFD, 0xF3,
    ];

    /// <summary>
    /// Determines whether a node is a top-level root container (direct child of <see cref="SceneRoot"/>,
    /// not a bone or mesh) whose transform is approximately identity, indicating it was stripped of
    /// the FBX coordinate-system basis rotation during import. On export these nodes receive
    /// <see cref="s_authoredRootPreRotation"/> so the FBX file correctly converts to Y-up.
    /// </summary>
    /// <param name="node">The node to inspect.</param>
    /// <returns><see langword="true"/> when the node is a stripped or identity root container.</returns>
    public static bool IsIdentityRootContainer(SceneNode node)
    {
        if (node.Parent is not SceneRoot || node is SkeletonBone or Mesh)
        {
            return false;
        }

        return IsNearlyZero(node.GetBindLocalPosition())
            && Vector3.DistanceSquared(node.GetBindLocalScale(), Vector3.One) <= 1e-6f
            && MathF.Abs(Quaternion.Dot(node.GetBindLocalRotation(), Quaternion.Identity)) > 0.9999f;
    }

    /// <summary>
    /// Resolves the top-level container ancestor for a node by walking up to the first child of <see cref="SceneRoot"/>.
    /// </summary>
    /// <param name="node">The node whose export root should be located.</param>
    /// <returns>The top-level export root, or <see langword="null"/> when none can be resolved.</returns>
    public static SceneNode? GetTopLevelExportRoot(SceneNode node)
    {
        SceneNode current = node;
        while (current.Parent is not null and not SceneRoot)
        {
            current = current.Parent;
        }

        return current.Parent is SceneRoot ? current : null;
    }

    /// <summary>
    /// Computes the bind-world matrix that will be exported for the specified node.
    /// </summary>
    /// <param name="node">The node to evaluate.</param>
    /// <returns>The exported bind-world matrix.</returns>
    public static Matrix4x4 GetExportBindWorldMatrix(SceneNode node)
    {
        Matrix4x4 localMatrix = GetExportLocalBindMatrix(node);
        return node.Parent is not null and not SceneRoot
            ? localMatrix * GetExportBindWorldMatrix(node.Parent)
            : localMatrix;
    }

    /// <summary>
    /// Computes the world matrix implied by exported Model transform properties for the specified node.
    /// </summary>
    /// <param name="node">The node to evaluate.</param>
    /// <returns>The exported active/world matrix implied by serialized model properties.</returns>
    public static Matrix4x4 GetExportActiveWorldMatrix(SceneNode node)
    {
        Matrix4x4 localMatrix = GetExportLocalModelMatrix(node);
        return node.Parent is not null and not SceneRoot
            ? localMatrix * GetExportActiveWorldMatrix(node.Parent)
            : localMatrix;
    }

    /// <summary>
    /// Computes the bind-local matrix that will be exported for the specified node.
    /// Derives all FBX properties from the scene graph — no stored metadata is required.
    /// Identity root containers receive <see cref="s_authoredRootPreRotation"/> for Y-up conversion.
    /// Skeleton bones have their rotation promoted to PreRotation (rest pose) for Maya compatibility.
    /// </summary>
    /// <param name="node">The node to evaluate.</param>
    /// <returns>The exported bind-local matrix.</returns>
    public static Matrix4x4 GetExportLocalBindMatrix(SceneNode node)
    {
        (Vector3 localTranslation, Quaternion localRotation, Vector3 localScale) = ResolveExportBindLocalTransform(node);
        if (IsIdentityRootContainer(node))
        {
            return s_authoredRootBasis;
        }

        return Matrix4x4.CreateScale(localScale)
            * Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(localRotation))
            * Matrix4x4.CreateTranslation(localTranslation);
    }

    /// <summary>
    /// Determines whether a vector is numerically close to zero.
    /// </summary>
    /// <param name="value">The vector to test.</param>
    /// <returns><see langword="true"/> when the vector magnitude is within the zero tolerance.</returns>
    public static bool IsNearlyZero(Vector3 value)
        => value.LengthSquared() <= 1e-10f;

    /// <summary>
    /// Determines whether a vector is numerically close to <see cref="Vector3.One"/>.
    /// </summary>
    /// <param name="value">The vector to test.</param>
    /// <returns><see langword="true"/> when the vector is within the one tolerance.</returns>
    public static bool IsNearlyOne(Vector3 value)
        => Vector3.DistanceSquared(value, Vector3.One) <= 1e-10f;

    /// <summary>
    /// Strips coordinate-system conversion rotations from top-level imported containers.
    /// FBX files authored from Z-up sources embed a <c>-90°X</c> <see cref="s_authoredRootPreRotation"/>
    /// on every root container to convert into Y-up. This residual rotation is an FBX artifact
    /// that should not propagate into the format-neutral scene graph. The method detects top-level
    /// children of <paramref name="rootNode"/> whose only transform component is the authored
    /// pre-rotation, resets the container to identity, and rebuilds inverse-bind matrices on
    /// descendant meshes so that skin bindings remain consistent.
    /// </summary>
    /// <param name="rootNode">The scene root whose direct children should be inspected.</param>
    public static void StripImportedRootBasisTransforms(SceneNode rootNode, IReadOnlyDictionary<SkeletonBone, Matrix4x4>? boneBindWorldHints = null)
    {
        Quaternion basisRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, float.DegreesToRadians(-90f));
        SceneNode[] topLevelChildren = [.. rootNode.EnumerateChildren()];
        List<SceneNode> strippedContainers = [];

        for (int i = 0; i < topLevelChildren.Length; i++)
        {
            SceneNode child = topLevelChildren[i];

            if (child is SkeletonBone || child is Mesh)
            {
                continue;
            }

            // Detect containers whose only transform is the FBX basis rotation Rx(-90°).
            if (!IsNearlyZero(child.GetBindLocalPosition()))
            {
                continue;
            }

            if (!IsNearlyOne(child.GetBindLocalScale()))
            {
                continue;
            }

            if (MathF.Abs(Quaternion.Dot(child.GetBindLocalRotation(), basisRotation)) < 0.9999f)
            {
                continue;
            }

            strippedContainers.Add(child);
        }

        if (strippedContainers.Count == 0)
        {
            return;
        }

        // Capture every bone's current model-pose world matrix BEFORE stripping
        // the root basis transforms. Multiplying by s_importBasis cancels the
        // ImportedRoot's Rx(-90°), giving the bone's world in the stripped space.
        // This must be unconditional: when no BindPose exists, reconstruction from
        // explicit IBMs will overwrite BindTransform locals and the original
        // model-pose would be lost without this snapshot.
        Dictionary<SceneNode, Matrix4x4> importedLiveWorldHints = [];
        foreach (SceneNode strippedContainer in strippedContainers)
        {
            foreach (SkeletonBone bone in strippedContainer.EnumerateDescendants().OfType<SkeletonBone>())
            {
                Matrix4x4 importedWorld = bone.GetBindWorldMatrix() * s_importBasis;
                importedLiveWorldHints[bone] = importedWorld;
            }
        }

        foreach (SceneNode strippedContainer in strippedContainers)
        {
            strippedContainer.BindTransform.LocalPosition = Vector3.Zero;
            strippedContainer.BindTransform.LocalRotation = Quaternion.Identity;
            strippedContainer.BindTransform.Scale = Vector3.One;
        }

        // Invalidate cached world transforms so bones recompute through identity containers.
        foreach (SceneNode child in topLevelChildren)
        {
            child.BindTransform.WorldPosition = null;
            child.BindTransform.WorldRotation = null;
            foreach (SceneNode descendant in child.EnumerateDescendants())
            {
                descendant.BindTransform.WorldPosition = null;
                descendant.BindTransform.WorldRotation = null;
            }
        }

        ApplyBoneBindWorldHints(boneBindWorldHints);

        // When no bind-pose hints were provided, reconstruct bone locals from
        // explicit IBMs so bones are at their correct world positions.
        if (boneBindWorldHints is null || boneBindWorldHints.Count == 0)
        {
            ReconstructBoneLocalsFromExplicitSkinning(rootNode);
        }

        // Apply live overrides AFTER both bind-hint application and IBM reconstruction
        // so the comparison is against the final bind locals, not the pre-strip ones.
        if (importedLiveWorldHints.Count > 0)
        {
            ApplyImportedLiveWorldHints(importedLiveWorldHints);
        }

        // Rebuild IBMs for ALL skinned meshes from the current scene graph.
        // Imported cluster IBMs are in FBX Y-up world space, but after stripping
        // and applying BindPose hints the bone bind-world matrices are in the
        // engine's Z-up space.  Keeping the original Y-up IBMs would cause
        // v × IBM_yup × boneActiveWorld_zup — a coordinate-space mismatch that
        // produces wrong vertex positions.  Rebuilding makes IBMs consistent:
        // IBM = meshBindWorld × inv(boneBindWorld), both in the same post-strip space.
        foreach (Mesh mesh in rootNode.EnumerateDescendants().OfType<Mesh>())
        {
            if (mesh.HasSkinning)
            {
                mesh.RebuildInverseBindMatrices();
            }
        }
    }

    /// <summary>
    /// Applies transient imported bone bind-world hints by reconstructing each hinted bone's
    /// local transform from its parent bind world. Parents are applied before children.
    /// </summary>
    /// <param name="boneBindWorldHints">Bone bind-world hints resolved from raw FBX skin clusters.</param>
    private static void ApplyBoneBindWorldHints(IReadOnlyDictionary<SkeletonBone, Matrix4x4>? boneBindWorldHints)
    {
        if (boneBindWorldHints is null || boneBindWorldHints.Count == 0)
        {
            return;
        }

        List<(SkeletonBone bone, Matrix4x4 world, int depth)> sorted = [];
        foreach ((SkeletonBone bone, Matrix4x4 world) in boneBindWorldHints)
        {
            int depth = 0;
            for (SceneNode? p = bone.Parent; p is not null; p = p.Parent)
            {
                depth++;
            }

            sorted.Add((bone, world, depth));
        }

        sorted.Sort((a, b) => a.depth.CompareTo(b.depth));

        foreach ((SkeletonBone bone, Matrix4x4 bindWorld, _) in sorted)
        {
            Matrix4x4 parentWorld = bone.Parent is not null ? bone.Parent.GetBindWorldMatrix() : Matrix4x4.Identity;
            Matrix4x4 localMatrix = Matrix4x4.Invert(parentWorld, out Matrix4x4 invParent)
                ? bindWorld * invParent
                : bindWorld;

            if (Matrix4x4.Decompose(localMatrix, out Vector3 scale, out Quaternion rotation, out Vector3 translation))
            {
                bone.BindTransform.LocalPosition = translation;
                bone.BindTransform.LocalRotation = Quaternion.Normalize(rotation);
                bone.BindTransform.Scale = scale;
            }
            else
            {
                bone.BindTransform.LocalPosition = new Vector3(localMatrix.M41, localMatrix.M42, localMatrix.M43);
                bone.BindTransform.LocalRotation = Quaternion.Identity;
            }

            bone.BindTransform.WorldPosition = null;
            bone.BindTransform.WorldRotation = null;
        }
    }

    /// <summary>
    /// Applies imported authored world-pose hints into live transforms so active pose keeps
    /// pre-export adjustments after root-basis stripping while bind transforms remain skin-aligned.
    /// Parent worlds come from the same imported dictionary so that each bone's live local is
    /// computed in a consistent coordinate space (FBX Model chain), cancelling per-bone euler
    /// decomposition drift. Only TRS components that genuinely differ from bind are stored.
    /// </summary>
    /// <param name="importedLiveWorldHints">Imported live-world hints keyed by scene node.</param>
    private static void ApplyImportedLiveWorldHints(IReadOnlyDictionary<SceneNode, Matrix4x4> importedLiveWorldHints)
    {
        List<(SceneNode node, Matrix4x4 world, int depth)> sorted = [];
        foreach ((SceneNode node, Matrix4x4 world) in importedLiveWorldHints)
        {
            int depth = 0;
            for (SceneNode? p = node.Parent; p is not null; p = p.Parent)
            {
                depth++;
            }

            sorted.Add((node, world, depth));
        }

        sorted.Sort((a, b) => a.depth.CompareTo(b.depth));

        foreach ((SceneNode node, Matrix4x4 liveWorld, _) in sorted)
        {
            Matrix4x4 parentWorld = Matrix4x4.Identity;
            if (node.Parent is not null)
            {
                if (!importedLiveWorldHints.TryGetValue(node.Parent, out parentWorld))
                {
                    parentWorld = node.Parent.GetActiveWorldMatrix();
                }
            }

            Matrix4x4 localMatrix = Matrix4x4.Invert(parentWorld, out Matrix4x4 invParent)
                ? liveWorld * invParent
                : liveWorld;

            if (Matrix4x4.Decompose(localMatrix, out Vector3 scale, out Quaternion rotation, out Vector3 translation))
            {
                rotation = Quaternion.Normalize(rotation);

                bool positionDiffers = Vector3.DistanceSquared(translation, node.GetBindLocalPosition()) > 1e-4f;
                bool rotationDiffers = MathF.Abs(Quaternion.Dot(rotation, node.GetBindLocalRotation())) < 0.9999f;
                bool scaleDiffers = Vector3.DistanceSquared(scale, node.GetBindLocalScale()) > 1e-4f;

                if (!positionDiffers && !rotationDiffers && !scaleDiffers)
                {
                    continue;
                }

                if (positionDiffers)
                {
                    node.LiveTransform.LocalPosition = translation;
                }

                if (rotationDiffers)
                {
                    node.LiveTransform.LocalRotation = rotation;
                }

                if (scaleDiffers)
                {
                    node.LiveTransform.Scale = scale;
                }
            }
            else
            {
                Vector3 fallbackPosition = new(localMatrix.M41, localMatrix.M42, localMatrix.M43);
                if (Vector3.DistanceSquared(fallbackPosition, node.GetBindLocalPosition()) < 1e-4f)
                {
                    continue;
                }

                node.LiveTransform.LocalPosition = fallbackPosition;
            }

            node.LiveTransform.WorldPosition = null;
            node.LiveTransform.WorldRotation = null;
        }
    }

    /// <summary>
    /// Builds per-bone bind-world hints from FBX BindPose nodes.
    /// The bind-pose matrix is authored in FBX Y-up world space and converted to
    /// RedFox Z-up world space using <see cref="s_importBasis"/>.
    /// </summary>
    /// <param name="objectsById">All FBX objects keyed by id.</param>
    /// <param name="bonesByModelId">Imported bones keyed by FBX model id.</param>
    /// <returns>A map of bones to converted bind-world matrices.</returns>
    private static Dictionary<SkeletonBone, Matrix4x4> BuildBindPoseBoneWorldHints(Dictionary<long, FbxNode> objectsById, Dictionary<long, SkeletonBone> bonesByModelId)
    {
        Dictionary<SkeletonBone, Matrix4x4> hints = [];

        foreach ((_, FbxNode objectNode) in objectsById)
        {
            if (!string.Equals(objectNode.Name, "Pose", StringComparison.Ordinal))
            {
                continue;
            }

            FbxNode? typeNode = objectNode.FirstChild("Type");
            if (typeNode is null || typeNode.Properties.Count == 0 || !string.Equals(typeNode.Properties[0].AsString(), "BindPose", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (FbxNode poseNode in objectNode.ChildrenNamed("PoseNode"))
            {
                FbxNode? nodeIdNode = poseNode.FirstChild("Node");
                if (nodeIdNode is null || nodeIdNode.Properties.Count == 0)
                {
                    continue;
                }

                long modelId = nodeIdNode.Properties[0].AsInt64();
                if (!bonesByModelId.TryGetValue(modelId, out SkeletonBone? bone))
                {
                    continue;
                }

                Matrix4x4 poseWorld = FbxSkinningMapper.ReadNodeMatrix(poseNode, "Matrix");
                Matrix4x4 convertedWorld = poseWorld * s_importBasis;

                if (!hints.ContainsKey(bone))
                {
                    hints[bone] = convertedWorld;
                }
            }
        }

        return hints;
    }

    /// <summary>
    /// Reconstructs bind-local transforms for bones that participate in meshes with
    /// explicit inverse-bind matrices. For each bone influence this solves:
    /// <c>IBM = meshBindWorld × inv(boneBindWorld)</c>, therefore
    /// <c>boneBindWorld = inv(IBM) × meshBindWorld</c>. Using mesh bind WORLD (not local)
    /// keeps reconstruction correct for nested mesh parents.
    /// </summary>
    /// <param name="rootNode">The scene root whose descendant meshes and bones should be inspected.</param>
    private static void ReconstructBoneLocalsFromExplicitSkinning(SceneNode rootNode)
    {
        Dictionary<SkeletonBone, Matrix4x4> corrections = [];

        foreach (Mesh mesh in rootNode.EnumerateDescendants().OfType<Mesh>())
        {
            if (!mesh.HasSkinning
                || !mesh.HasExplicitInverseBindMatrices
                || mesh.InverseBindMatrices is not { Count: > 0 } inverseBindMatrices
                || mesh.SkinnedBones is not { Count: > 0 } skinnedBones)
            {
                continue;
            }
            Matrix4x4 meshBindWorld = mesh.GetBindWorldMatrix();

            for (int i = 0; i < skinnedBones.Count; i++)
            {
                SkeletonBone bone = skinnedBones[i];

                if (!Matrix4x4.Invert(inverseBindMatrices[i], out Matrix4x4 invIbm))
                {
                    continue;
                }

                Matrix4x4 candidateWorld = invIbm * meshBindWorld;
                if (corrections.TryGetValue(bone, out Matrix4x4 existingWorld))
                {
                    Vector3 existingTranslation = new(existingWorld.M41, existingWorld.M42, existingWorld.M43);
                    Vector3 candidateTranslation = new(candidateWorld.M41, candidateWorld.M42, candidateWorld.M43);
                    Vector3 currentTranslation = bone.GetBindWorldPosition();

                    float existingDelta = Vector3.DistanceSquared(existingTranslation, currentTranslation);
                    float candidateDelta = Vector3.DistanceSquared(candidateTranslation, currentTranslation);

                    if (candidateDelta < existingDelta)
                    {
                        corrections[bone] = candidateWorld;
                    }

                    continue;
                }

                corrections[bone] = candidateWorld;
            }
        }

        if (corrections.Count == 0)
        {
            return;
        }

        // Sort corrected bones top-down (by ancestor depth) so that parents are processed
        // before children. This ensures GetBindWorldMatrix() on a parent already reflects
        // any prior correction when we compute the child's local.
        List<(SkeletonBone bone, Matrix4x4 correctWorld, int depth)> sorted = [];
        foreach ((SkeletonBone bone, Matrix4x4 correctWorld) in corrections)
        {
            int depth = 0;
            for (SceneNode? p = bone.Parent; p is not null; p = p.Parent)
            {
                depth++;
            }

            sorted.Add((bone, correctWorld, depth));
        }

        sorted.Sort((a, b) => a.depth.CompareTo(b.depth));

        // Apply corrections: compute local = correctWorld × inv(parentWorld), decompose,
        // and replace the bone's local transform. Clear cached world so it recomputes.
        foreach ((SkeletonBone bone, Matrix4x4 correctWorld, _) in sorted)
        {
            Matrix4x4 parentWorld = bone.Parent is not null
                ? bone.Parent.GetBindWorldMatrix()
                : Matrix4x4.Identity;

            Matrix4x4 localMatrix = Matrix4x4.Invert(parentWorld, out Matrix4x4 invParent)
                ? correctWorld * invParent
                : correctWorld;

            if (Matrix4x4.Decompose(localMatrix, out Vector3 scale, out Quaternion rotation, out Vector3 translation))
            {
                bone.BindTransform.LocalPosition = translation;
                bone.BindTransform.LocalRotation = Quaternion.Normalize(rotation);
                bone.BindTransform.Scale = scale;
            }
            else
            {
                bone.BindTransform.LocalPosition = new Vector3(localMatrix.M41, localMatrix.M42, localMatrix.M43);
                bone.BindTransform.LocalRotation = Quaternion.Identity;
            }

            // Clear cached worlds so GetBindWorldMatrix re-derives from the corrected local chain.
            bone.BindTransform.WorldPosition = null;
            bone.BindTransform.WorldRotation = null;
        }
    }

    /// <summary>
    /// Determines whether a node should export an FBX Null node attribute.
    /// Container-type nodes (non-bone, non-mesh, non-light, non-camera) always get one.
    /// </summary>
    /// <param name="node">The node to inspect.</param>
    /// <returns><see langword="true"/> when a Null node attribute should be written.</returns>
    public static bool ShouldExportNullNodeAttribute(SceneNode node)
        => node is not (SkeletonBone or Mesh or Camera or Light);

    /// <summary>
    /// Resolves the FBX Null node-attribute name for a node.
    /// </summary>
    /// <param name="node">The node to inspect.</param>
    /// <returns>The generated Null node-attribute name.</returns>
    public static string GetNullNodeAttributeName(SceneNode node)
        => $"{node.Name}\0\u0001NodeAttribute";

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
        FbxNode? objectsNode = document.FirstNode("Objects") ?? document.FirstNodeRecursive("Objects");
        FbxNode? connectionsNode = document.FirstNode("Connections") ?? document.FirstNodeRecursive("Connections");
        if (objectsNode is null)
        {
            return scene;
        }

        Dictionary<long, FbxNode> objectsById = BuildObjectMap(objectsNode);
        List<FbxConnection> connections = BuildConnections(connectionsNode);
        Dictionary<long, SceneNode> modelNodes = [];
        Dictionary<long, Mesh> meshesByModelId = [];
        Dictionary<long, FbxNode> geometryNodes = [];
        Dictionary<long, Material> materialsById = [];
        Dictionary<long, SkeletonBone> bonesByModelId = [];
        Dictionary<long, Camera> camerasByModelId = [];
        Dictionary<long, Light> lightsByModelId = [];
        Dictionary<long, FbxNode> constraintNodes = [];
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
                        CreateCameraNode(objectNode, objectId, modelNodes, camerasByModelId);
                    }
                    else if (string.Equals(modelType, "Light", StringComparison.OrdinalIgnoreCase))
                    {
                        CreateLightNode(objectNode, objectId, modelNodes, lightsByModelId);
                    }
                    else
                    {
                        CreateModelNode(objectNode, objectId, modelNodes, meshesByModelId, bonesByModelId);
                    }

                    break;
                }

                case "Geometry":
                    geometryNodes[objectId] = objectNode;
                    break;

                case "Material":
                    CreateMaterialNode(objectNode, objectId, materialsById);
                    break;

                case "Constraint":
                    constraintNodes[objectId] = objectNode;
                    break;

                default:
                    break;
            }
        }

        ApplyModelHierarchy(modelNodes, connections);
        AttachImportedRootNodes(scene.RootNode, modelNodes.Values);
        AttachImportedRootNodes(scene.RootNode, materialsById.Values);
        ClassifyNullContainers(modelNodes);
        AttachGeometry(meshesByModelId, geometryNodes, connections, perTriangleMaterials);
        AttachMaterials(meshesByModelId, materialsById, connections);
        SplitMeshesByMaterial(perTriangleMaterials);
        FbxSkinningMapper.ImportSkinning(meshesByModelId, objectsById, connections, bonesByModelId);
        AttachNullNodeAttributes(modelNodes, objectsById, connections);
        AttachCameraAndLightNodeAttributes(camerasByModelId, lightsByModelId, objectsById, connections);
        ImportConstraints(scene.RootNode, modelNodes, constraintNodes, connections);
        Dictionary<SkeletonBone, Matrix4x4> bindPoseBoneHints = BuildBindPoseBoneWorldHints(objectsById, bonesByModelId);
        StripImportedRootBasisTransforms(scene.RootNode, bindPoseBoneHints);
        return scene;
    }


    /// <summary>
    /// Exports a scene into an FBX document.
    /// </summary>
    /// <param name="scene">The source scene.</param>
    /// <param name="format">The target FBX format.</param>
    /// <returns>The exported FBX document.</returns>
    public static FbxDocument ExportScene(Scene scene, FbxFormat format)
        => ExportScene(scene, format, new SceneTranslationSelection(scene, SceneNodeFlags.None));

    /// <summary>
    /// Exports a scene into an FBX document using a filtered scene selection.
    /// </summary>
    /// <param name="scene">The source scene.</param>
    /// <param name="format">The target FBX format.</param>
    /// <param name="selection">The filtered scene selection for this export.</param>
    /// <returns>The exported FBX document.</returns>
    public static FbxDocument ExportScene(Scene scene, FbxFormat format, SceneTranslationSelection selection)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(selection);

        if (!ReferenceEquals(selection.Scene, scene))
        {
            throw new ArgumentException("The supplied selection does not belong to the provided scene.", nameof(selection));
        }

        FbxDocument document = new() { Format = format, Version = 7700 };
        FbxNode objectsNode = new("Objects");
        FbxNode connectionsNode = new("Connections");

        long nextId = 100000;
        Dictionary<SceneNode, long> modelIds = [];
        Dictionary<Mesh, long> geometryIds = [];
        Dictionary<Material, long> materialIds = [];
        Dictionary<SkeletonBone, long> boneIds = [];
        Dictionary<SkeletonBone, long> boneAttributeIds = [];

        Model[] models = selection.GetDescendants<Model>();
        Group[] groups = selection.GetDescendants<Group>();
        Mesh[] meshes = selection.GetDescendants<Mesh>();
        Material[] materials = selection.GetDescendants<Material>();
        SkeletonBone[] bones = selection.GetDescendants<SkeletonBone>();
        Camera[] cameras = selection.GetDescendants<Camera>();
        Light[] lights = selection.GetDescendants<Light>();

        List<SceneNode> exportedModelNodeList =
        [
            .. models,
            .. groups,
            .. bones,
            .. meshes,
            .. cameras,
            .. lights,
        ];
        SceneNode[] exportedModelNodes = [.. exportedModelNodeList];

        for (int i = 0; i < models.Length; i++)
        {
            Model model = models[i];
            long id = nextId++;
            modelIds[model] = id;
            SceneNode? exportedParent = SceneNode.GetBestParent(model, exportedModelNodes);
            objectsNode.Children.Add(CreateModelObject(id, model.Name, "Null", model, exportedParent));
            ExportNullNodeAttributeIfNeeded(model, id, objectsNode, connectionsNode, ref nextId);
        }

        for (int i = 0; i < groups.Length; i++)
        {
            Group group = groups[i];
            long id = nextId++;
            modelIds[group] = id;
            SceneNode? exportedParent = SceneNode.GetBestParent(group, exportedModelNodes);
            objectsNode.Children.Add(CreateModelObject(id, group.Name, "Null", group, exportedParent));
            ExportNullNodeAttributeIfNeeded(group, id, objectsNode, connectionsNode, ref nextId);
        }

        for (int i = 0; i < bones.Length; i++)
        {
            SkeletonBone bone = bones[i];
            long modelId = nextId++;
            long nodeAttributeId = nextId++;
            boneIds[bone] = modelId;
            boneAttributeIds[bone] = nodeAttributeId;
            modelIds[bone] = modelId;
            SceneNode? exportedParent = SceneNode.GetBestParent(bone, exportedModelNodes);
            objectsNode.Children.Add(CreateModelObject(modelId, bone.Name, "LimbNode", bone, exportedParent));
            objectsNode.Children.Add(CreateBoneNodeAttribute(nodeAttributeId, bone));
            AddConnection(connectionsNode, "OO", nodeAttributeId, modelId);
        }

        long animationStackId = nextId++;
        long animationLayerId = nextId++;
        objectsNode.Children.Add(CreateAnimationStackObject(animationStackId));
        objectsNode.Children.Add(CreateAnimationLayerObject(animationLayerId));
        AddConnection(connectionsNode, "OO", animationLayerId, animationStackId);

        for (int i = 0; i < meshes.Length; i++)
        {
            Mesh mesh = meshes[i];
            long modelId = nextId++;
            long geometryId = nextId++;
            modelIds[mesh] = modelId;
            geometryIds[mesh] = geometryId;
            SceneNode? exportedParent = SceneNode.GetBestParent(mesh, exportedModelNodes);
            objectsNode.Children.Add(CreateModelObject(modelId, mesh.Name, "Mesh", mesh, exportedParent));
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
            SceneNode? exportedParent = SceneNode.GetBestParent(camera, exportedModelNodes);
            objectsNode.Children.Add(CreateModelObject(modelId, camera.Name, "Camera", camera, exportedParent));
            objectsNode.Children.Add(CreateCameraNodeAttribute(nodeAttributeId, camera));
            AddConnection(connectionsNode, "OO", nodeAttributeId, modelId);
        }

        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            long modelId = nextId++;
            long nodeAttributeId = nextId++;
            modelIds[light] = modelId;
            SceneNode? exportedParent = SceneNode.GetBestParent(light, exportedModelNodes);
            objectsNode.Children.Add(CreateModelObject(modelId, light.Name, "Light", light, exportedParent));
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

            ValidateExportMaterialConnections(mesh, materialIds);

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
                ValidateExportSkinning(mesh, boneIds);
                FbxSkinningMapper.ExportSkinning(objectsNode, connectionsNode, mesh, geometryId, boneIds, ref nextId);

                long bindPoseId = nextId++;
                objectsNode.Children.Add(CreateBindPoseObject(bindPoseId, mesh, modelIds, boneIds));
            }
        }

        foreach ((SceneNode node, long childId) in modelIds)
        {
            long parentId = RootConnectionId;
            SceneNode? exportedParent = SceneNode.GetBestParent(node, exportedModelNodes);
            if (exportedParent is not null && modelIds.TryGetValue(exportedParent, out long resolvedParentId))
            {
                parentId = resolvedParentId;
            }

            AddConnection(connectionsNode, "OO", childId, parentId);
        }

        AddHeaderNodes(document, scene.Name);
        AddDocumentsNode(document, scene.Name);
        AddReferencesNode(document);
        AddDefinitionsNode(document, objectsNode);
        document.Nodes.Add(objectsNode);
        document.Nodes.Add(connectionsNode);
        AddTakesNode(document);

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
                mesh.BindTransform.CopyTo(splitMesh.BindTransform);
                mesh.LiveTransform.CopyTo(splitMesh.LiveTransform);
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
                    if (mesh.HasExplicitInverseBindMatrices)
                    {
                        splitMesh.SetSkinBinding(mesh.SkinnedBones, mesh.InverseBindMatrices);
                    }
                    else
                    {
                        splitMesh.SetSkinBinding(mesh.SkinnedBones);
                    }
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

            string propertyName = connectionNode.Properties.Count > 3 ? connectionNode.Properties[3].AsString() : string.Empty;
            connections.Add(new FbxConnection(connectionNode.Properties[0].AsString(), connectionNode.Properties[1].AsInt64(), connectionNode.Properties[2].AsInt64(), propertyName));
        }

        return connections;
    }

    /// <summary>
    /// Creates a scene node from an FBX Model object and registers it in the relevant tracking maps.
    /// </summary>
    /// <param name="objectNode">The FBX Model node.</param>
    /// <param name="objectId">The unique object identifier.</param>
    /// <param name="modelNodes">Map updated with the new scene node.</param>
    /// <param name="meshesByModelId">Map updated when the node is a mesh.</param>
    /// <param name="bonesByModelId">Map updated when the node is a bone.</param>
    public static void CreateModelNode(FbxNode objectNode, long objectId, Dictionary<long, SceneNode> modelNodes, Dictionary<long, Mesh> meshesByModelId, Dictionary<long, SkeletonBone> bonesByModelId)
    {
        string modelName = GetNodeObjectName(objectNode);
        string modelType = objectNode.Properties.Count > 2 ? objectNode.Properties[2].AsString() : "Null";

        if (string.Equals(modelType, "Mesh", StringComparison.OrdinalIgnoreCase))
        {
            Mesh mesh = new() { Name = modelName };
            ApplyModelTransform(mesh, objectNode);
            modelNodes[objectId] = mesh;
            meshesByModelId[objectId] = mesh;
            return;
        }

        if (string.Equals(modelType, "LimbNode", StringComparison.OrdinalIgnoreCase) || string.Equals(modelType, "Root", StringComparison.OrdinalIgnoreCase))
        {
            SkeletonBone bone = new(modelName);
            ApplyModelTransform(bone, objectNode);
            modelNodes[objectId] = bone;
            bonesByModelId[objectId] = bone;
            return;
        }

        Group container = new(modelName);
        ApplyModelTransform(container, objectNode);
        modelNodes[objectId] = container;
    }

    /// <summary>
    /// Attaches any imported nodes that remain parentless after FBX hierarchy construction to the scene root.
    /// </summary>
    /// <param name="sceneRoot">The destination scene root.</param>
    /// <param name="nodes">The imported nodes to inspect.</param>
    public static void AttachImportedRootNodes(SceneRoot sceneRoot, IEnumerable<SceneNode> nodes)
    {
        foreach (SceneNode node in nodes)
        {
            if (node.Parent is null)
            {
                sceneRoot.AddNode(node);
            }
        }
    }

    /// <summary>
    /// Reclassifies imported Null containers after hierarchy construction so skeleton-shaped and model-shaped branches map to the correct scene-node types.
    /// </summary>
    /// <param name="modelNodes">All imported scene nodes keyed by model id.</param>
    public static void ClassifyNullContainers(Dictionary<long, SceneNode> modelNodes)
    {
        KeyValuePair<long, SceneNode>[] groupsByDepth = [.. modelNodes
            .Where(static pair => pair.Value is Group)
            .OrderByDescending(static pair => pair.Value.GetAncestors().Length)];

        for (int i = 0; i < groupsByDepth.Length; i++)
        {
            long objectId = groupsByDepth[i].Key;
            if (modelNodes[objectId] is not Group group)
            {
                continue;
            }

            SceneNode replacement = CreateClassifiedNullContainer(group);
            if (ReferenceEquals(replacement, group))
            {
                continue;
            }

            ReplaceImportedNullContainer(group, replacement);
            modelNodes[objectId] = replacement;
        }
    }

    /// <summary>
    /// Creates the scene-node type that best matches an imported Null container based on its immediate children.
    /// </summary>
    /// <param name="group">The imported generic group node.</param>
    /// <returns>A replacement node when the group should become a specific container type; otherwise the original group.</returns>
    public static SceneNode CreateClassifiedNullContainer(Group group)
    {
        SceneNode[] children = [.. group.EnumerateChildren()];
        if (children.Length == 0)
        {
            return group;
        }

        bool allModelChildren = true;

        for (int i = 0; i < children.Length; i++)
        {
            SceneNode child = children[i];
            allModelChildren &= child is Mesh or Model or Camera or Light;
        }

        if (allModelChildren)
        {
            return new Model { Name = group.Name };
        }

        return group;
    }

    /// <summary>
    /// Replaces an imported generic group node with a specialized scene-node type while preserving transforms and children.
    /// </summary>
    /// <param name="source">The source group being replaced.</param>
    /// <param name="replacement">The replacement node.</param>
    public static void ReplaceImportedNullContainer(Group source, SceneNode replacement)
    {
        SceneNode? originalParent = source.Parent;
        if (originalParent is null)
        {
            return;
        }

        originalParent.RemoveNode(source);
        originalParent.AddNode(replacement);
        CopySceneNodeState(source, replacement);

        SceneNode[] children = [.. source.EnumerateChildren()];
        for (int i = 0; i < children.Length; i++)
        {
            children[i].MoveTo(replacement, ReparentTransformMode.PreserveLocal);
        }
    }

    /// <summary>
    /// Copies transform state between two scene nodes during importer-driven node replacement.
    /// </summary>
    /// <param name="source">The source scene node.</param>
    /// <param name="destination">The destination scene node.</param>
    public static void CopySceneNodeState(SceneNode source, SceneNode destination)
    {
        source.BindTransform.CopyTo(destination.BindTransform);
        source.LiveTransform.CopyTo(destination.LiveTransform);
        destination.Flags = source.Flags;
        destination.GraphicsHandle = source.GraphicsHandle;
    }

    /// <summary>
    /// Creates a <see cref="Material"/> scene node from an FBX Material object.
    /// </summary>
    /// <param name="objectNode">The FBX Material node.</param>
    /// <param name="objectId">The unique object identifier.</param>
    /// <param name="materialsById">Map updated with the new material.</param>
    public static void CreateMaterialNode(FbxNode objectNode, long objectId, Dictionary<long, Material> materialsById)
    {
        string materialName = GetNodeObjectName(objectNode);
        Material material = new(materialName);
        ApplyMaterialProperties(material, objectNode);
        materialsById[objectId] = material;
    }

    /// <summary>
    /// Gets the effective FBX constraint type for a constraint object node.
    /// </summary>
    /// <param name="objectNode">The FBX object node to inspect.</param>
    /// <returns>The FBX constraint type token, or an empty string when unavailable.</returns>
    public static string GetConstraintType(FbxNode objectNode)
    {
        if (!string.Equals(objectNode.Name, "Constraint", StringComparison.Ordinal) || objectNode.Properties.Count < 3)
        {
            return string.Empty;
        }

        string? type = GetNodeString(objectNode, "Type");
        return !string.IsNullOrWhiteSpace(type) ? type : objectNode.Properties[2].AsString();
    }

    /// <summary>
    /// Determines whether an FBX constraint type maps to a Maya-style parent constraint.
    /// </summary>
    /// <param name="constraintType">The FBX constraint type token.</param>
    /// <returns><see langword="true"/> when the type is a supported parent-constraint form.</returns>
    public static bool IsParentConstraintType(string constraintType)
        => string.Equals(constraintType, "Parent-Child", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether an FBX constraint type maps to a supported orient constraint.
    /// </summary>
    /// <param name="constraintType">The FBX constraint type token.</param>
    /// <returns><see langword="true"/> when the type is a supported orient-constraint form.</returns>
    public static bool IsOrientConstraintType(string constraintType)
        => string.Equals(constraintType, "Orientation", StringComparison.OrdinalIgnoreCase)
            || string.Equals(constraintType, "Orient", StringComparison.OrdinalIgnoreCase)
            || string.Equals(constraintType, "Rotation", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Imports supported FBX constraint objects as runtime solver scene nodes.
    /// </summary>
    /// <param name="sceneRoot">The destination scene root.</param>
    /// <param name="modelNodes">Imported model nodes keyed by FBX object id.</param>
    /// <param name="constraintNodes">Constraint FBX nodes keyed by object id.</param>
    /// <param name="connections">The full FBX connection list.</param>
    public static void ImportConstraints(SceneRoot sceneRoot, Dictionary<long, SceneNode> modelNodes, Dictionary<long, FbxNode> constraintNodes, IReadOnlyList<FbxConnection> connections)
    {
        foreach ((long constraintId, FbxNode constraintNode) in constraintNodes)
        {
            if (!TryResolveConstraintNodes(constraintId, modelNodes, connections, out SceneNode? constrainedNode, out SceneNode? sourceNode))
            {
                continue;
            }

            string constraintType = GetConstraintType(constraintNode);
            ConstraintNode? constraint = CreateImportedConstraint(constraintType, constraintNode, constrainedNode!, sourceNode!);
            if (constraint is null)
            {
                continue;
            }

            SceneNode parentNode = ResolveConstraintParentNode(sceneRoot, constraintId, constrainedNode!, modelNodes, connections);
            parentNode.AddNode(constraint);
        }
    }

    /// <summary>
    /// Resolves the imported scene-graph parent for a constraint node.
    /// </summary>
    /// <param name="sceneRoot">The destination scene root.</param>
    /// <param name="constraintId">The FBX constraint object identifier.</param>
    /// <param name="constrainedNode">The constrained scene node.</param>
    /// <param name="modelNodes">Imported model nodes keyed by FBX object id.</param>
    /// <param name="connections">The full FBX connection list.</param>
    /// <returns>The scene node that should own the imported constraint.</returns>
    public static SceneNode ResolveConstraintParentNode(SceneRoot sceneRoot, long constraintId, SceneNode constrainedNode, Dictionary<long, SceneNode> modelNodes, IReadOnlyList<FbxConnection> connections)
    {
        for (int i = 0; i < connections.Count; i++)
        {
            FbxConnection connection = connections[i];
            if (!string.Equals(connection.ConnectionType, "OO", StringComparison.Ordinal) || connection.ChildId != constraintId)
            {
                continue;
            }

            if (modelNodes.TryGetValue(connection.ParentId, out SceneNode? explicitParent))
            {
                return explicitParent;
            }

            if (connection.ParentId == RootConnectionId)
            {
                return sceneRoot;
            }
        }

        return constrainedNode;
    }

    /// <summary>
    /// Creates a runtime solver node for a supported imported FBX constraint object.
    /// </summary>
    /// <param name="constraintType">The FBX constraint type token.</param>
    /// <param name="constraintNode">The source FBX constraint node.</param>
    /// <param name="constrainedNode">The constrained scene node.</param>
    /// <param name="sourceNode">The source scene node.</param>
    /// <returns>The created constraint node, or <see langword="null"/> when the type is unsupported.</returns>
    public static ConstraintNode? CreateImportedConstraint(string constraintType, FbxNode constraintNode, SceneNode constrainedNode, SceneNode sourceNode)
    {
        float weight = GetConstraintWeight(constraintNode);
        string constraintName = GetNodeObjectName(constraintNode);

        if (IsParentConstraintType(constraintType))
        {
            return new ParentConstraintNode(constraintName, constrainedNode, sourceNode)
            {
                Weight = weight,
                TranslationOffset = GetConstraintOffsetTranslation(constraintNode),
                RotationOffset = Quaternion.Normalize(FbxRotation.FromEulerDegreesXyz(GetConstraintOffsetRotation(constraintNode))),
            };
        }

        if (IsOrientConstraintType(constraintType))
        {
            return new OrientConstraintNode(constraintName, constrainedNode, sourceNode)
            {
                Weight = weight,
                RotationOffset = Quaternion.Normalize(FbxRotation.FromEulerDegreesXyz(GetConstraintOffsetRotation(constraintNode))),
            };
        }

        return null;
    }

    /// <summary>
    /// Resolves the constrained and source nodes referenced by a supported FBX constraint.
    /// </summary>
    /// <param name="constraintId">The FBX constraint object identifier.</param>
    /// <param name="modelNodes">Imported model nodes keyed by FBX object id.</param>
    /// <param name="connections">The full FBX connection list.</param>
    /// <param name="constrainedNode">Receives the constrained child node.</param>
    /// <param name="sourceNode">Receives the source parent node.</param>
    /// <returns><see langword="true"/> when both nodes were resolved.</returns>
    public static bool TryResolveConstraintNodes(long constraintId, Dictionary<long, SceneNode> modelNodes, IReadOnlyList<FbxConnection> connections, out SceneNode? constrainedNode, out SceneNode? sourceNode)
    {
        constrainedNode = null;
        sourceNode = null;

        for (int i = 0; i < connections.Count; i++)
        {
            FbxConnection connection = connections[i];
            if (!string.Equals(connection.ConnectionType, "OP", StringComparison.Ordinal) || connection.ParentId != constraintId)
            {
                continue;
            }

            if (string.Equals(connection.PropertyName, "Constrained object (Child)", StringComparison.OrdinalIgnoreCase)
                && modelNodes.TryGetValue(connection.ChildId, out SceneNode? resolvedChild))
            {
                constrainedNode = resolvedChild;
                continue;
            }

            if (string.Equals(connection.PropertyName, "Source (Parent)", StringComparison.OrdinalIgnoreCase)
                && modelNodes.TryGetValue(connection.ChildId, out SceneNode? resolvedSource))
            {
                sourceNode = resolvedSource;
            }
        }

        return constrainedNode is not null && sourceNode is not null;
    }

    /// <summary>
    /// Reads the normalized weight from an FBX constraint node.
    /// </summary>
    /// <param name="constraintNode">The source FBX constraint node.</param>
    /// <returns>The normalized constraint weight in the range 0 to 1.</returns>
    public static float GetConstraintWeight(FbxNode constraintNode)
    {
        FbxNode? properties70 = constraintNode.FirstChild("Properties70");
        if (properties70 is null)
        {
            return 1.0f;
        }

        foreach (FbxNode propertyNode in properties70.ChildrenNamed("P"))
        {
            if (propertyNode.Properties.Count > 4 && propertyNode.Properties[0].AsString().EndsWith(".Weight", StringComparison.Ordinal))
            {
                return (float)(propertyNode.Properties[4].AsDouble() / 100.0);
            }
        }

        return 1.0f;
    }

    /// <summary>
    /// Reads the translation offset from an FBX constraint node.
    /// </summary>
    /// <param name="constraintNode">The source FBX constraint node.</param>
    /// <returns>The imported translation offset.</returns>
    public static Vector3 GetConstraintOffsetTranslation(FbxNode constraintNode)
        => GetConstraintVector3PropertyBySuffix(constraintNode, ".Offset T", Vector3.Zero);

    /// <summary>
    /// Reads the Euler rotation offset in degrees from an FBX constraint node.
    /// </summary>
    /// <param name="constraintNode">The source FBX constraint node.</param>
    /// <returns>The imported XYZ Euler rotation offset in degrees.</returns>
    public static Vector3 GetConstraintOffsetRotation(FbxNode constraintNode)
        => GetConstraintVector3PropertyBySuffix(constraintNode, ".Offset R", Vector3.Zero);

    /// <summary>
    /// Reads a vector constraint property whose name ends with the specified suffix.
    /// </summary>
    /// <param name="constraintNode">The source FBX constraint node.</param>
    /// <param name="propertyNameSuffix">The property-name suffix to match.</param>
    /// <param name="fallback">The fallback vector returned when the property is absent.</param>
    /// <returns>The decoded vector value or <paramref name="fallback"/>.</returns>
    public static Vector3 GetConstraintVector3PropertyBySuffix(FbxNode constraintNode, string propertyNameSuffix, Vector3 fallback)
    {
        FbxNode? properties70 = constraintNode.FirstChild("Properties70");
        if (properties70 is null)
        {
            return fallback;
        }

        foreach (FbxNode propertyNode in properties70.ChildrenNamed("P"))
        {
            if (propertyNode.Properties.Count < 7)
            {
                continue;
            }

            string propertyName = propertyNode.Properties[0].AsString();
            if (!propertyName.EndsWith(propertyNameSuffix, StringComparison.Ordinal))
            {
                continue;
            }

            return new Vector3(
                (float)propertyNode.Properties[4].AsDouble(),
                (float)propertyNode.Properties[5].AsDouble(),
                (float)propertyNode.Properties[6].AsDouble());
        }

        return fallback;
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
                childBone.MoveTo(parentBone, ReparentTransformMode.PreserveLocal);
                continue;
            }

            bool shouldReparent = (child, parent) switch
            {
                (SkeletonBone, Model or Group) => true,
                (Mesh or Group or Model, Model or Group) => true,
                (Camera or Light, _) => true,
                _ => false,
            };

            if (shouldReparent)
            {
                child.MoveTo(parent, ReparentTransformMode.PreserveLocal);
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
            Quaternion preQuat = ComposeEulerRotation(preRotation, 0);
            Quaternion localQuat = ComposeEulerRotation(localRotation, rotationOrder);
            Quaternion postQuat = ComposeEulerRotation(postRotation, 0);
            Quaternion postInvQuat = Quaternion.Inverse(postQuat);
            resolvedRotation = Quaternion.Normalize(postInvQuat * localQuat * preQuat);
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
    /// <param name="sceneName">The scene name written into document metadata.</param>
    public static void AddHeaderNodes(FbxDocument document, string sceneName)
    {
        DateTime utcNow = DateTime.UtcNow;
        FbxNode header = new("FBXHeaderExtension");
        header.Children.Add(new FbxNode("FBXHeaderVersion") { Properties = { new FbxProperty('I', 1004) } });
        header.Children.Add(new FbxNode("FBXVersion") { Properties = { new FbxProperty('I', document.Version) } });
        header.Children.Add(new FbxNode("EncryptionType") { Properties = { new FbxProperty('I', 0) } });
        FbxNode otherFlags = new("OtherFlags");
        otherFlags.Children.Add(new FbxNode("TCDefinition") { Properties = { new FbxProperty('I', 127) } });
        header.Children.Add(otherFlags);
        FbxNode creationTimeStamp = new("CreationTimeStamp");
        creationTimeStamp.Children.Add(new FbxNode("Version") { Properties = { new FbxProperty('I', 1000) } });
        creationTimeStamp.Children.Add(new FbxNode("Year") { Properties = { new FbxProperty('I', utcNow.Year) } });
        creationTimeStamp.Children.Add(new FbxNode("Month") { Properties = { new FbxProperty('I', utcNow.Month) } });
        creationTimeStamp.Children.Add(new FbxNode("Day") { Properties = { new FbxProperty('I', utcNow.Day) } });
        creationTimeStamp.Children.Add(new FbxNode("Hour") { Properties = { new FbxProperty('I', utcNow.Hour) } });
        creationTimeStamp.Children.Add(new FbxNode("Minute") { Properties = { new FbxProperty('I', utcNow.Minute) } });
        creationTimeStamp.Children.Add(new FbxNode("Second") { Properties = { new FbxProperty('I', utcNow.Second) } });
        creationTimeStamp.Children.Add(new FbxNode("Millisecond") { Properties = { new FbxProperty('I', utcNow.Millisecond) } });
        header.Children.Add(creationTimeStamp);
        header.Children.Add(new FbxNode("Creator") { Properties = { new FbxProperty('S', "RedFox Graphics3D Kaydara FBX") } });
        header.Children.Add(CreateSceneInfoNode(sceneName));
        document.Nodes.Add(header);

        document.Nodes.Add(new FbxNode("FileId") { Properties = { new FbxProperty('R', (byte[])s_fileId.Clone()) } });
        document.Nodes.Add(new FbxNode("CreationTime") { Properties = { new FbxProperty('S', DefaultFileCreationTime) } });
        document.Nodes.Add(new FbxNode("Creator") { Properties = { new FbxProperty('S', "RedFox Graphics3D Kaydara FBX") } });

        FbxNode globalSettings = new("GlobalSettings");
        globalSettings.Children.Add(new FbxNode("Version") { Properties = { new FbxProperty('I', 1000) } });
        FbxNode globalProperties = globalSettings.AddChild("Properties70");
        AddGlobalProperty(globalProperties, "UpAxis", "int", new FbxProperty('I', 1));
        AddGlobalProperty(globalProperties, "UpAxisSign", "int", new FbxProperty('I', 1));
        AddGlobalProperty(globalProperties, "FrontAxis", "int", new FbxProperty('I', 2));
        AddGlobalProperty(globalProperties, "FrontAxisSign", "int", new FbxProperty('I', 1));
        AddGlobalProperty(globalProperties, "CoordAxis", "int", new FbxProperty('I', 0));
        AddGlobalProperty(globalProperties, "CoordAxisSign", "int", new FbxProperty('I', 1));
        AddGlobalProperty(globalProperties, "OriginalUpAxis", "int", new FbxProperty('I', 1));
        AddGlobalProperty(globalProperties, "OriginalUpAxisSign", "int", new FbxProperty('I', 1));
        AddGlobalProperty(globalProperties, "UnitScaleFactor", "double", new FbxProperty('D', 1.0));
        AddGlobalProperty(globalProperties, "OriginalUnitScaleFactor", "double", new FbxProperty('D', 1.0));
        AddGlobalProperty(globalProperties, "AmbientColor", "ColorRGB", new FbxProperty('D', 0.0));
        AddStringProperty(globalProperties, "DefaultCamera", "KString", "Producer Perspective");
        AddIntProperty(globalProperties, "TimeMode", "enum", 11);
        AddIntProperty(globalProperties, "TimeProtocol", "enum", 2);
        AddIntProperty(globalProperties, "SnapOnFrameMode", "enum", 0);
        AddGlobalProperty(globalProperties, "TimeSpanStart", "KTime", new FbxProperty('L', 0L));
        AddGlobalProperty(globalProperties, "TimeSpanStop", "KTime", new FbxProperty('L', 0L));
        AddDoubleProperty(globalProperties, "CustomFrameRate", "double", -1.0);
        document.Nodes.Add(globalSettings);
    }

    /// <summary>
    /// Appends a Documents node with a single scene document entry.
    /// </summary>
    /// <param name="document">The target FBX document.</param>
    /// <param name="sceneName">The source scene name.</param>
    public static void AddDocumentsNode(FbxDocument document, string sceneName)
    {
        FbxNode documents = new("Documents");
        documents.Children.Add(new FbxNode("Count") { Properties = { new FbxProperty('I', 1) } });

        FbxNode doc = new("Document");
        doc.Properties.Add(new FbxProperty('L', 1L));
        doc.Properties.Add(new FbxProperty('S', string.Empty));
        doc.Properties.Add(new FbxProperty('S', "Scene"));
        FbxNode properties = doc.AddChild("Properties70");
        AddGlobalProperty(properties, "SourceObject", "object", new FbxProperty('L', 0L));
        AddStringProperty(properties, "ActiveAnimStackName", "KString", DefaultTakeName);
        doc.Children.Add(new FbxNode("RootNode") { Properties = { new FbxProperty('L', 0L) } });
        documents.Children.Add(doc);
        document.Nodes.Add(documents);
    }

    /// <summary>
    /// Appends an empty References node.
    /// </summary>
    /// <param name="document">The target FBX document.</param>
    public static void AddReferencesNode(FbxDocument document)
    {
        document.Nodes.Add(new FbxNode("References"));
    }

    /// <summary>
    /// Appends an FBX Definitions node based on the exported Objects payload.
    /// </summary>
    /// <param name="document">The target FBX document.</param>
    /// <param name="objectsNode">The populated Objects node.</param>
    public static void AddDefinitionsNode(FbxDocument document, FbxNode objectsNode)
    {
        Dictionary<string, int> counts = [];
        Dictionary<string, int> nodeAttributeCounts = [];
        foreach (FbxNode child in objectsNode.Children)
        {
            counts[child.Name] = counts.TryGetValue(child.Name, out int count) ? count + 1 : 1;

            if (string.Equals(child.Name, "NodeAttribute", StringComparison.Ordinal) && child.Properties.Count > 2)
            {
                string subtype = child.Properties[2].AsString();
                nodeAttributeCounts[subtype] = nodeAttributeCounts.TryGetValue(subtype, out int subtypeCount) ? subtypeCount + 1 : 1;
            }
        }

        FbxNode definitions = new("Definitions");
        definitions.Children.Add(new FbxNode("Version") { Properties = { new FbxProperty('I', 100) } });
        definitions.Children.Add(new FbxNode("Count") { Properties = { new FbxProperty('I', counts.Values.Sum() + 1) } });

        FbxNode globalSettingsType = new("ObjectType");
        globalSettingsType.Properties.Add(new FbxProperty('S', "GlobalSettings"));
        globalSettingsType.Children.Add(new FbxNode("Count") { Properties = { new FbxProperty('I', 1) } });
        definitions.Children.Add(globalSettingsType);

        foreach ((string objectTypeName, int count) in counts.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            FbxNode objectType = new("ObjectType");
            objectType.Properties.Add(new FbxProperty('S', objectTypeName));
            objectType.Children.Add(new FbxNode("Count") { Properties = { new FbxProperty('I', count) } });

            if (string.Equals(objectTypeName, "Model", StringComparison.Ordinal))
            {
                objectType.Children.Add(CreateDefinitionTemplate("FbxNode", static properties =>
                {
                    AddVectorProperty(properties, "Lcl Translation", "Lcl Translation", Vector3.Zero);
                    AddVectorProperty(properties, "Lcl Rotation", "Lcl Rotation", Vector3.Zero);
                    AddVectorProperty(properties, "Lcl Scaling", "Lcl Scaling", Vector3.One);
                    AddDoubleProperty(properties, "Visibility", "Visibility", 1.0);
                    AddIntProperty(properties, "Visibility Inheritance", "Visibility Inheritance", 1);
                    AddIntProperty(properties, "DefaultAttributeIndex", "int", 0);
                    AddIntProperty(properties, "InheritType", "enum", 1);
                }));
            }
            else if (string.Equals(objectTypeName, "AnimationStack", StringComparison.Ordinal))
            {
                objectType.Children.Add(CreateDefinitionTemplate("FbxAnimStack", static properties =>
                {
                    AddStringProperty(properties, "Description", "KString", string.Empty);
                    AddGlobalProperty(properties, "LocalStart", "KTime", new FbxProperty('L', 0L));
                    AddGlobalProperty(properties, "LocalStop", "KTime", new FbxProperty('L', 0L));
                    AddGlobalProperty(properties, "ReferenceStart", "KTime", new FbxProperty('L', 0L));
                    AddGlobalProperty(properties, "ReferenceStop", "KTime", new FbxProperty('L', 0L));
                }));
            }
            else if (string.Equals(objectTypeName, "AnimationLayer", StringComparison.Ordinal))
            {
                objectType.Children.Add(CreateDefinitionTemplate("FbxAnimLayer", static properties =>
                {
                    AddDoubleProperty(properties, "Weight", "Number", 100.0);
                    AddIntProperty(properties, "Mute", "bool", 0);
                    AddIntProperty(properties, "Solo", "bool", 0);
                    AddIntProperty(properties, "Lock", "bool", 0);
                    AddVectorProperty(properties, "Color", "ColorRGB", new Vector3(0.8f, 0.8f, 0.8f));
                    AddIntProperty(properties, "BlendMode", "enum", 0);
                    AddIntProperty(properties, "RotationAccumulationMode", "enum", 0);
                    AddIntProperty(properties, "ScaleAccumulationMode", "enum", 0);
                    AddGlobalProperty(properties, "BlendModeBypass", "ULongLong", new FbxProperty('L', 0L));
                }));
            }
            else if (string.Equals(objectTypeName, "Geometry", StringComparison.Ordinal))
            {
                objectType.Children.Add(CreateDefinitionTemplate("FbxMesh", static properties =>
                {
                    AddVectorProperty(properties, "Color", "ColorRGB", new Vector3(0.8f, 0.8f, 0.8f));
                    AddVectorProperty(properties, "BBoxMin", "Vector3D", Vector3.Zero);
                    AddVectorProperty(properties, "BBoxMax", "Vector3D", Vector3.Zero);
                    AddIntProperty(properties, "Primary Visibility", "bool", 1);
                    AddIntProperty(properties, "Casts Shadows", "bool", 1);
                    AddIntProperty(properties, "Receive Shadows", "bool", 1);
                }));
            }
            else if (string.Equals(objectTypeName, "Material", StringComparison.Ordinal))
            {
                objectType.Children.Add(CreateDefinitionTemplate("FbxSurfaceLambert", static properties =>
                {
                    AddStringProperty(properties, "ShadingModel", "KString", "Lambert");
                    AddIntProperty(properties, "MultiLayer", "bool", 0);
                    AddVectorProperty(properties, "EmissiveColor", "Color", Vector3.Zero);
                    AddDoubleProperty(properties, "EmissiveFactor", "Number", 1.0);
                    AddVectorProperty(properties, "AmbientColor", "Color", new Vector3(0.2f, 0.2f, 0.2f));
                    AddDoubleProperty(properties, "AmbientFactor", "Number", 1.0);
                    AddVectorProperty(properties, "DiffuseColor", "Color", new Vector3(0.8f, 0.8f, 0.8f));
                    AddDoubleProperty(properties, "DiffuseFactor", "Number", 1.0);
                    AddVectorProperty(properties, "Bump", "Vector3D", Vector3.Zero);
                    AddVectorProperty(properties, "NormalMap", "Vector3D", Vector3.Zero);
                    AddDoubleProperty(properties, "BumpFactor", "double", 1.0);
                    AddVectorProperty(properties, "TransparentColor", "Color", Vector3.Zero);
                    AddDoubleProperty(properties, "TransparencyFactor", "Number", 0.0);
                    AddVectorProperty(properties, "DisplacementColor", "ColorRGB", Vector3.Zero);
                    AddDoubleProperty(properties, "DisplacementFactor", "double", 1.0);
                    AddVectorProperty(properties, "VectorDisplacementColor", "ColorRGB", Vector3.Zero);
                    AddDoubleProperty(properties, "VectorDisplacementFactor", "double", 1.0);
                }));
            }
            else if (string.Equals(objectTypeName, "NodeAttribute", StringComparison.Ordinal))
            {
                foreach ((string subtype, int subtypeCount) in nodeAttributeCounts.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
                {
                    _ = subtypeCount;
                    objectType.Children.Add(CreateNodeAttributeTemplate(subtype));
                }
            }

            definitions.Children.Add(objectType);
        }

        document.Nodes.Add(definitions);
    }

    /// <summary>
    /// Appends a Takes node with the default take entry used by Maya exports.
    /// </summary>
    /// <param name="document">The target FBX document.</param>
    public static void AddTakesNode(FbxDocument document)
    {
        FbxNode takes = new("Takes");
        takes.Children.Add(new FbxNode("Current") { Properties = { new FbxProperty('S', DefaultTakeName) } });

        FbxNode take = new("Take");
        take.Properties.Add(new FbxProperty('S', DefaultTakeName));
        take.Children.Add(new FbxNode("FileName") { Properties = { new FbxProperty('S', "Take_001.tak") } });
        take.Children.Add(new FbxNode("LocalTime") { Properties = { new FbxProperty('L', 0L), new FbxProperty('L', 0L) } });
        take.Children.Add(new FbxNode("ReferenceTime") { Properties = { new FbxProperty('L', 0L), new FbxProperty('L', 0L) } });
        takes.Children.Add(take);

        document.Nodes.Add(takes);
    }

    /// <summary>
    /// Creates the default animation stack object.
    /// </summary>
    /// <param name="id">The FBX object identifier.</param>
    /// <returns>The generated AnimationStack node.</returns>
    public static FbxNode CreateAnimationStackObject(long id)
    {
        FbxNode animationStack = new("AnimationStack");
        animationStack.Properties.Add(new FbxProperty('L', id));
        animationStack.Properties.Add(new FbxProperty('S', DefaultTakeName + "\0\u0001AnimStack"));
        animationStack.Properties.Add(new FbxProperty('S', string.Empty));
        FbxNode properties = animationStack.AddChild("Properties70");
        AddGlobalProperty(properties, "LocalStart", "KTime", new FbxProperty('L', 0L));
        AddGlobalProperty(properties, "LocalStop", "KTime", new FbxProperty('L', 0L));
        AddGlobalProperty(properties, "ReferenceStart", "KTime", new FbxProperty('L', 0L));
        AddGlobalProperty(properties, "ReferenceStop", "KTime", new FbxProperty('L', 0L));
        return animationStack;
    }

    /// <summary>
    /// Creates the default animation layer object.
    /// </summary>
    /// <param name="id">The FBX object identifier.</param>
    /// <returns>The generated AnimationLayer node.</returns>
    public static FbxNode CreateAnimationLayerObject(long id)
    {
        FbxNode animationLayer = new("AnimationLayer");
        animationLayer.Properties.Add(new FbxProperty('L', id));
        animationLayer.Properties.Add(new FbxProperty('S', "BaseLayer\0\u0001AnimLayer"));
        animationLayer.Properties.Add(new FbxProperty('S', string.Empty));
        return animationLayer;
    }

    /// <summary>
    /// Appends a typed property entry to a Properties70 node.
    /// </summary>
    /// <param name="properties">The Properties70 node to append to.</param>
    /// <param name="name">The property name.</param>
    /// <param name="type">The FBX property type string.</param>
    /// <param name="value">The value property.</param>
    public static void AddGlobalProperty(
        FbxNode properties,
        string name,
        string type,
        FbxProperty value)
    {
        FbxNode property = properties.AddChild("P");
        property.Properties.Add(new FbxProperty('S', name));
        property.Properties.Add(new FbxProperty('S', type));
        property.Properties.Add(new FbxProperty('S', GetPropertyAttributeType(type)));
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
        => CreateModelObject(id, name, type, sourceNode, sourceNode.Parent);

    /// <summary>
    /// Creates an FBX Model object for export using the node's nearest exported parent as the local transform basis.
    /// </summary>
    /// <param name="id">The FBX object identifier.</param>
    /// <param name="name">The scene node name.</param>
    /// <param name="type">The FBX model type token.</param>
    /// <param name="sourceNode">The source scene node.</param>
    /// <param name="exportedParent">The nearest exported ancestor, or <see langword="null"/> when the node is exported at the root.</param>
    /// <returns>The generated FBX Model node.</returns>
    public static FbxNode CreateModelObject(long id, string name, string type, SceneNode sourceNode, SceneNode? exportedParent)
    {
        FbxNode modelNode = new("Model");
        modelNode.Properties.Add(new FbxProperty('L', id));
        modelNode.Properties.Add(new FbxProperty('S', name + "\0\u0001Model"));
        modelNode.Properties.Add(new FbxProperty('S', type));
        modelNode.Children.Add(new FbxNode("Version") { Properties = { new FbxProperty('I', 232) } });
        FbxNode properties = modelNode.AddChild("Properties70");
        (Vector3 localTranslation, Quaternion localRotation, Vector3 localScale) = ResolveExportLocalTransform(sourceNode, exportedParent);
        Vector3 exportedLocalTranslation = localTranslation;
        Vector3 exportedLocalRotation = FbxRotation.ToEulerDegreesXyz(localRotation);
        Vector3 exportedLocalScale = localScale;
        Vector3 exportedPreRotation = Vector3.Zero;

        if (IsIdentityRootContainer(sourceNode))
        {
            exportedPreRotation = s_authoredRootPreRotation;
            exportedLocalTranslation = Vector3.Zero;
            exportedLocalRotation = Vector3.Zero;
            exportedLocalScale = Vector3.One;
        }
        else if (sourceNode is SkeletonBone
            && sourceNode.LiveTransform.LocalRotation is null
            && IsNearlyZero(exportedPreRotation)
            && !IsNearlyZero(exportedLocalRotation))
        {
            exportedPreRotation = exportedLocalRotation;
            exportedLocalRotation = Vector3.Zero;
        }

        if (sourceNode is Mesh)
        {
            AddIntProperty(properties, "RotationActive", "bool", 1);
            AddIntProperty(properties, "InheritType", "enum", 1);
            AddVectorProperty(properties, "ScalingMax", "Vector3D", Vector3.Zero);
            AddIntProperty(properties, "DefaultAttributeIndex", "int", 0);
            AddVectorProperty(properties, "Lcl Translation", "Lcl Translation", exportedLocalTranslation);
            AddVectorProperty(properties, "Lcl Rotation", "Lcl Rotation", exportedLocalRotation);
            AddVectorProperty(properties, "Lcl Scaling", "Lcl Scaling", exportedLocalScale);
            AddStringProperty(properties, "currentUVSet", "KString", "map1");
        }
        else
        {
            AddIntProperty(properties, "RotationActive", "bool", 1);
            AddVectorProperty(properties, "ScalingMax", "Vector3D", Vector3.Zero);
            AddIntProperty(properties, "InheritType", "enum", 1);
            AddIntProperty(properties, "DefaultAttributeIndex", "int", 0);

            if (!IsNearlyZero(exportedPreRotation))
            {
                AddVectorProperty(properties, "PreRotation", "Vector3D", exportedPreRotation);
            }

            if (!IsNearlyZero(exportedLocalTranslation))
            {
                AddVectorProperty(properties, "Lcl Translation", "Lcl Translation", exportedLocalTranslation);
            }

            if (!IsNearlyZero(exportedLocalRotation))
            {
                AddVectorProperty(properties, "Lcl Rotation", "Lcl Rotation", exportedLocalRotation);
            }

            if (!IsNearlyOne(exportedLocalScale))
            {
                AddVectorProperty(properties, "Lcl Scaling", "Lcl Scaling", exportedLocalScale);
            }
        }

        modelNode.Children.Add(new FbxNode("Shading") { Properties = { new FbxProperty('C', true) } });
        modelNode.Children.Add(new FbxNode("Culling") { Properties = { new FbxProperty('S', "CullingOff") } });
        return modelNode;
    }

    /// <summary>
    /// Creates an FBX NodeAttribute node for a skeleton limb bone.
    /// </summary>
    /// <param name="id">The FBX object identifier.</param>
    /// <param name="bone">The source bone.</param>
    /// <returns>The generated NodeAttribute node.</returns>
    public static FbxNode CreateBoneNodeAttribute(long id, SkeletonBone bone)
    {
        ArgumentNullException.ThrowIfNull(bone);

        FbxNode nodeAttribute = new("NodeAttribute");
        nodeAttribute.Properties.Add(new FbxProperty('L', id));
        nodeAttribute.Properties.Add(new FbxProperty('S', bone.Name + "\0\u0001NodeAttribute"));
        nodeAttribute.Properties.Add(new FbxProperty('S', "LimbNode"));
        nodeAttribute.Children.Add(new FbxNode("TypeFlags") { Properties = { new FbxProperty('S', "Skeleton") } });
        FbxNode properties = nodeAttribute.AddChild("Properties70");
        AddGlobalProperty(properties, "Size", "double", new FbxProperty('D', GetBoneDisplaySize(bone)));
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
        materialNode.Children.Add(new FbxNode("Version") { Properties = { new FbxProperty('I', 102) } });
        materialNode.Children.Add(new FbxNode("ShadingModel") { Properties = { new FbxProperty('S', "lambert") } });
        materialNode.Children.Add(new FbxNode("MultiLayer") { Properties = { new FbxProperty('I', 0) } });
        FbxNode properties = materialNode.AddChild("Properties70");
        Vector4 diffuse = material.DiffuseColor ?? Vector4.One;
        AddVectorProperty(properties, "AmbientColor", "Color", Vector3.Zero);
        AddVectorProperty(properties, "DiffuseColor", "Color", new Vector3(diffuse.X, diffuse.Y, diffuse.Z));
        AddDoubleProperty(properties, "DiffuseFactor", "Number", 0.8);
        AddDoubleProperty(properties, "TransparencyFactor", "Number", 1.0);
        Vector4 emissive = material.EmissiveColor ?? Vector4.Zero;
        AddVectorProperty(properties, "Emissive", "Vector3D", new Vector3(emissive.X, emissive.Y, emissive.Z));
        AddVectorProperty(properties, "Ambient", "Vector3D", Vector3.Zero);
        AddVectorProperty(properties, "Diffuse", "Vector3D", new Vector3(diffuse.X * 0.5f, diffuse.Y * 0.5f, diffuse.Z * 0.5f));
        AddDoubleProperty(properties, "Opacity", "double", diffuse.W);
        return materialNode;
    }

    /// <summary>
    /// Creates an FBX BindPose node for the given mesh and its skinned bone palette.
    /// </summary>
    /// <param name="id">The FBX object identifier.</param>
    /// <param name="mesh">The source mesh.</param>
    /// <param name="modelIds">All exported model ids keyed by scene node.</param>
    /// <param name="boneIds">All exported bone model ids keyed by skeleton bone.</param>
    /// <returns>The generated FBX Pose node.</returns>
    public static FbxNode CreateBindPoseObject(long id, Mesh mesh, Dictionary<SceneNode, long> modelIds, Dictionary<SkeletonBone, long> boneIds)
    {
        FbxNode poseNode = new("Pose");
        poseNode.Properties.Add(new FbxProperty('L', id));
        poseNode.Properties.Add(new FbxProperty('S', mesh.Name + "_BindPose\0\u0001Pose"));
        poseNode.Properties.Add(new FbxProperty('S', "BindPose"));
        poseNode.Children.Add(new FbxNode("Type") { Properties = { new FbxProperty('S', "BindPose") } });
        poseNode.Children.Add(new FbxNode("Version") { Properties = { new FbxProperty('I', 100) } });

        List<SceneNode> bindPoseNodes = CollectBindPoseNodes(mesh, modelIds, boneIds);
        poseNode.Children.Add(new FbxNode("NbPoseNodes") { Properties = { new FbxProperty('I', bindPoseNodes.Count) } });

        for (int nodeIndex = 0; nodeIndex < bindPoseNodes.Count; nodeIndex++)
        {
            SceneNode node = bindPoseNodes[nodeIndex];
            poseNode.Children.Add(CreatePoseEntry(modelIds[node], GetExportBindWorldMatrix(node)));
        }

        return poseNode;
    }

    /// <summary>
    /// Collects the exported nodes that must appear in a mesh bind pose, including mesh and bone ancestors.
    /// </summary>
    /// <param name="mesh">The mesh being exported.</param>
    /// <param name="modelIds">All exported model ids keyed by scene node.</param>
    /// <param name="boneIds">All exported bone model ids keyed by skeleton bone.</param>
    /// <returns>The ordered bind-pose node list with duplicates removed.</returns>
    public static List<SceneNode> CollectBindPoseNodes(Mesh mesh, Dictionary<SceneNode, long> modelIds, Dictionary<SkeletonBone, long> boneIds)
    {
        List<SceneNode> nodes = [];
        HashSet<SceneNode> seen = [];

        AddBindPoseChain(nodes, seen, mesh, modelIds);

        if (mesh.SkinnedBones is { Count: > 0 } bones)
        {
            for (int i = 0; i < bones.Count; i++)
            {
                SkeletonBone bone = bones[i];
                if (!boneIds.ContainsKey(bone))
                {
                    continue;
                }

                AddBindPoseChain(nodes, seen, bone, modelIds);
            }
        }

        return nodes;
    }

    /// <summary>
    /// Adds a scene node and its exported ancestors to the ordered bind-pose list.
    /// </summary>
    /// <param name="nodes">The destination ordered node list.</param>
    /// <param name="seen">The de-duplication set.</param>
    /// <param name="startNode">The node whose chain should be added.</param>
    /// <param name="modelIds">All exported model ids keyed by scene node.</param>
    public static void AddBindPoseChain(List<SceneNode> nodes, HashSet<SceneNode> seen, SceneNode? startNode, Dictionary<SceneNode, long> modelIds)
    {
        SceneNode? current = startNode;
        while (current is not null)
        {
            if (modelIds.ContainsKey(current) && seen.Add(current))
            {
                nodes.Add(current);
            }

            current = current.Parent;
        }
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
    /// Resolves the exact bind-local transform to export for a scene node.
    /// </summary>
    /// <param name="sourceNode">The scene node to query.</param>
    /// <returns>A tuple of translation, rotation, and scale in local space.</returns>
    public static (Vector3 Translation, Quaternion Rotation, Vector3 Scale) ResolveExportLocalTransform(SceneNode sourceNode)
    {
        if (sourceNode is Mesh)
        {
            return ResolveExportBindLocalTransform(sourceNode);
        }

        Vector3? livePosition = sourceNode.LiveTransform.LocalPosition;
        Quaternion? liveRotation = sourceNode.LiveTransform.LocalRotation;
        Vector3? liveScale = sourceNode.LiveTransform.Scale;

        if (livePosition is not null || liveRotation is not null || liveScale is not null)
        {
            return
            (
                livePosition ?? sourceNode.GetBindLocalPosition(),
                Quaternion.Normalize(liveRotation ?? sourceNode.GetBindLocalRotation()),
                liveScale ?? sourceNode.GetBindLocalScale()
            );
        }

        return
        (
            sourceNode.GetBindLocalPosition(),
            Quaternion.Normalize(sourceNode.GetBindLocalRotation()),
            sourceNode.GetBindLocalScale()
        );
    }

    /// <summary>
    /// Resolves the exported local transform for a node relative to its nearest exported ancestor.
    /// </summary>
    /// <param name="sourceNode">The node being exported.</param>
    /// <param name="exportedParent">The nearest exported ancestor, or <see langword="null"/> when the node is exported at the scene root.</param>
    /// <returns>The exported local transform relative to <paramref name="exportedParent"/>.</returns>
    public static (Vector3 Translation, Quaternion Rotation, Vector3 Scale) ResolveExportLocalTransform(SceneNode sourceNode, SceneNode? exportedParent)
    {
        ArgumentNullException.ThrowIfNull(sourceNode);

        if (ReferenceEquals(exportedParent, sourceNode.Parent))
        {
            return ResolveExportLocalTransform(sourceNode);
        }

        Matrix4x4 sourceWorld = GetExportActiveWorldMatrix(sourceNode);
        Matrix4x4 parentWorld = exportedParent is null ? Matrix4x4.Identity : GetExportActiveWorldMatrix(exportedParent);
        Matrix4x4 localMatrix = Matrix4x4.Invert(parentWorld, out Matrix4x4 inverseParent)
            ? sourceWorld * inverseParent
            : sourceWorld;

        if (Matrix4x4.Decompose(localMatrix, out Vector3 scale, out Quaternion rotation, out Vector3 translation))
        {
            return (translation, Quaternion.Normalize(rotation), scale);
        }

        return (new Vector3(localMatrix.M41, localMatrix.M42, localMatrix.M43), Quaternion.Identity, Vector3.One);
    }

    /// <summary>
    /// Computes the local matrix implied by exported Model transform properties for the specified node.
    /// </summary>
    /// <param name="node">The node to evaluate.</param>
    /// <returns>The exported local matrix implied by serialized model properties.</returns>
    public static Matrix4x4 GetExportLocalModelMatrix(SceneNode node)
    {
        (Vector3 localTranslation, Quaternion localRotation, Vector3 localScale) = ResolveExportLocalTransform(node);
        Vector3 exportedLocalTranslation = localTranslation;
        Vector3 exportedLocalRotation = FbxRotation.ToEulerDegreesXyz(localRotation);
        Vector3 exportedLocalScale = localScale;
        Vector3 exportedPreRotation = Vector3.Zero;

        if (IsIdentityRootContainer(node))
        {
            exportedPreRotation = s_authoredRootPreRotation;
            exportedLocalTranslation = Vector3.Zero;
            exportedLocalRotation = Vector3.Zero;
            exportedLocalScale = Vector3.One;
        }
        else if (node is SkeletonBone
            && node.LiveTransform.LocalRotation is null
            && IsNearlyZero(exportedPreRotation)
            && !IsNearlyZero(exportedLocalRotation))
        {
            exportedPreRotation = exportedLocalRotation;
            exportedLocalRotation = Vector3.Zero;
        }

        return ComposeNodeLocalTransform(
            exportedLocalTranslation,
            exportedLocalRotation,
            exportedLocalScale,
            exportedPreRotation,
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            0);
    }

    /// <summary>
    /// Resolves the bind-local transform used for bind-space export data such as
    /// cluster Transform/TransformLink and BindPose matrices.
    /// </summary>
    /// <param name="sourceNode">The scene node to query.</param>
    /// <returns>A tuple of bind translation, rotation, and scale in local space.</returns>
    public static (Vector3 Translation, Quaternion Rotation, Vector3 Scale) ResolveExportBindLocalTransform(SceneNode sourceNode)
    {
        return
        (
            sourceNode.GetBindLocalPosition(),
            Quaternion.Normalize(sourceNode.GetBindLocalRotation()),
            sourceNode.GetBindLocalScale()
        );
    }

    /// <summary>
    /// Computes a display size for an exported FBX limb node based on the bone hierarchy.
    /// </summary>
    /// <param name="bone">The source bone.</param>
    /// <returns>The display size to store in the FBX limb node attribute.</returns>
    public static double GetBoneDisplaySize(SkeletonBone bone)
    {
        ArgumentNullException.ThrowIfNull(bone);

        float bestChildDistance = float.MaxValue;
        Vector3 bonePosition = bone.GetBindWorldPosition();

        foreach (SceneNode child in bone.EnumerateChildren())
        {
            if (child is not SkeletonBone childBone)
            {
                continue;
            }

            float childDistance = Vector3.Distance(bonePosition, childBone.GetBindWorldPosition());
            if (childDistance > 1e-5f)
            {
                bestChildDistance = MathF.Min(bestChildDistance, childDistance);
            }
        }

        if (bestChildDistance != float.MaxValue)
        {
            return Math.Max(bestChildDistance, 1e-3f);
        }

        if (bone.Parent is SkeletonBone parentBone)
        {
            float parentDistance = Vector3.Distance(parentBone.GetBindWorldPosition(), bonePosition) * 0.5f;
            if (parentDistance > 1e-5f)
            {
                return Math.Max(parentDistance, 1e-3f);
            }
        }

        return 1.0;
    }

    /// <summary>
    /// Creates an FBX NodeAttribute node for a Null transform helper.
    /// </summary>
    /// <param name="id">The FBX object identifier.</param>
    /// <param name="rawName">The raw FBX node-attribute name.</param>
    /// <returns>The generated NodeAttribute node.</returns>
    public static FbxNode CreateNullNodeAttribute(long id, string rawName)
    {
        FbxNode nodeAttribute = new("NodeAttribute");
        nodeAttribute.Properties.Add(new FbxProperty('L', id));
        nodeAttribute.Properties.Add(new FbxProperty('S', rawName));
        nodeAttribute.Properties.Add(new FbxProperty('S', "Null"));
        FbxNode properties = nodeAttribute.AddChild("Properties70");
        AddIntProperty(properties, "Look", "enum", 0);
        nodeAttribute.Children.Add(new FbxNode("TypeFlags") { Properties = { new FbxProperty('S', "Null") } });
        return nodeAttribute;
    }

    /// <summary>
    /// Walks a mesh's skinned bone parent chain to locate the owning <see cref="Skeleton"/> (armature).
    /// </summary>
    /// <param name="mesh">The skinned mesh to inspect.</param>
    /// <returns>The owning skeleton node, or <see langword="null"/> when not found.</returns>
    public static SkeletonBone? FindBindPoseArmature(Mesh mesh)
    {
        if (mesh.SkinnedBones is not { Count: > 0 } bones)
        {
            return null;
        }

        SceneNode? current = bones[0].Parent;
        while (current is not null)
        {
            if (current is SkeletonBone rootBone && rootBone.Parent is not SkeletonBone)
            {
                return rootBone;
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
        // "Lcl Translation/Rotation/Scaling" and "Color" type properties are animatable
        // with empty attribute type and "A" flags. Other vector types like "Vector3D" and
        // "ColorRGB" use the standard attribute type mapping with empty flags.
        bool isAnimatableType = propertyType.StartsWith("Lcl ", StringComparison.Ordinal)
            || string.Equals(propertyType, "Color", StringComparison.Ordinal)
            || string.Equals(propertyType, "Visibility", StringComparison.Ordinal);
        string attributeType = isAnimatableType ? string.Empty : GetPropertyAttributeType(propertyType);
        string flags = isAnimatableType ? "A" : string.Empty;

        FbxNode property = properties.AddChild("P");
        property.Properties.Add(new FbxProperty('S', propertyName));
        property.Properties.Add(new FbxProperty('S', propertyType));
        property.Properties.Add(new FbxProperty('S', attributeType));
        property.Properties.Add(new FbxProperty('S', flags));
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
    public static void AddIntProperty(
        FbxNode properties,
        string propertyName,
        string propertyType,
        int value)
    {
        FbxNode property = properties.AddChild("P");
        property.Properties.Add(new FbxProperty('S', propertyName));
        property.Properties.Add(new FbxProperty('S', propertyType));
        property.Properties.Add(new FbxProperty('S', GetPropertyAttributeType(propertyType)));
        property.Properties.Add(new FbxProperty('S', string.Empty));
        property.Properties.Add(new FbxProperty('I', value));
    }

    /// <summary>
    /// Appends a string-typed Properties70 entry to the given properties node.
    /// </summary>
    /// <param name="properties">The Properties70 node to append to.</param>
    /// <param name="propertyName">The FBX property name.</param>
    /// <param name="propertyType">The FBX property type string.</param>
    /// <param name="value">The string value to write.</param>
    public static void AddStringProperty(
        FbxNode properties,
        string propertyName,
        string propertyType,
        string value)
    {
        FbxNode property = properties.AddChild("P");
        property.Properties.Add(new FbxProperty('S', propertyName));
        property.Properties.Add(new FbxProperty('S', propertyType));
        property.Properties.Add(new FbxProperty('S', GetPropertyAttributeType(propertyType)));
        property.Properties.Add(new FbxProperty('S', string.Empty));
        property.Properties.Add(new FbxProperty('S', value));
    }

    /// <summary>
    /// Creates a Definitions PropertyTemplate node.
    /// </summary>
    /// <param name="templateName">The FBX template class name.</param>
    /// <param name="populate">Callback that fills the template Properties70 node.</param>
    /// <returns>The generated PropertyTemplate node.</returns>
    public static FbxNode CreateDefinitionTemplate(string templateName, Action<FbxNode> populate)
    {
        FbxNode template = new("PropertyTemplate");
        template.Properties.Add(new FbxProperty('S', templateName));
        FbxNode properties = template.AddChild("Properties70");
        populate(properties);
        return template;
    }

    /// <summary>
    /// Creates a NodeAttribute PropertyTemplate node for the specified subtype.
    /// </summary>
    /// <param name="subtype">The node attribute subtype.</param>
    /// <returns>The generated PropertyTemplate node.</returns>
    public static FbxNode CreateNodeAttributeTemplate(string subtype)
    {
        if (string.Equals(subtype, "Camera", StringComparison.Ordinal))
        {
            return CreateDefinitionTemplate("FbxCamera", static properties =>
            {
                AddDoubleProperty(properties, "FilmAspectRatio", "double", 1.7777777778);
                AddDoubleProperty(properties, "FieldOfView", "FieldOfView", 60.0);
                AddDoubleProperty(properties, "NearPlane", "double", 10.0);
                AddDoubleProperty(properties, "FarPlane", "double", 100000.0);
                AddIntProperty(properties, "CameraProjectionType", "enum", 0);
            });
        }

        if (string.Equals(subtype, "Light", StringComparison.Ordinal))
        {
            return CreateDefinitionTemplate("FbxLight", static properties =>
            {
                AddIntProperty(properties, "LightType", "enum", 0);
                AddIntProperty(properties, "CastLight", "bool", 1);
                AddVectorProperty(properties, "Color", "Color", Vector3.One);
                AddDoubleProperty(properties, "Intensity", "Number", 100.0);
                AddIntProperty(properties, "DecayType", "enum", 2);
            });
        }

        if (string.Equals(subtype, "LimbNode", StringComparison.Ordinal))
        {
            return CreateDefinitionTemplate("LimbNode", static properties =>
            {
                AddDoubleProperty(properties, "Size", "double", 1.0);
            });
        }

        return CreateDefinitionTemplate("FbxNull", static properties =>
        {
            AddVectorProperty(properties, "Color", "ColorRGB", new Vector3(0.8f, 0.8f, 0.8f));
            AddDoubleProperty(properties, "Size", "double", 100.0);
        });
    }

    /// <summary>
    /// Creates a minimal SceneInfo node for the FBX header extension.
    /// </summary>
    /// <param name="sceneName">The source scene name.</param>
    /// <returns>The generated SceneInfo node.</returns>
    public static FbxNode CreateSceneInfoNode(string sceneName)
    {
        FbxNode sceneInfo = new("SceneInfo");
        sceneInfo.Properties.Add(new FbxProperty('S', "GlobalInfo\0\u0001SceneInfo"));
        sceneInfo.Properties.Add(new FbxProperty('S', "UserData"));
        sceneInfo.Children.Add(new FbxNode("Type") { Properties = { new FbxProperty('S', "UserData") } });
        sceneInfo.Children.Add(new FbxNode("Version") { Properties = { new FbxProperty('I', 100) } });

        FbxNode metaData = new("MetaData");
        metaData.Children.Add(new FbxNode("Version") { Properties = { new FbxProperty('I', 100) } });
        metaData.Children.Add(new FbxNode("Title") { Properties = { new FbxProperty('S', sceneName) } });
        metaData.Children.Add(new FbxNode("Subject") { Properties = { new FbxProperty('S', string.Empty) } });
        metaData.Children.Add(new FbxNode("Author") { Properties = { new FbxProperty('S', "RedFox") } });
        metaData.Children.Add(new FbxNode("Keywords") { Properties = { new FbxProperty('S', string.Empty) } });
        metaData.Children.Add(new FbxNode("Revision") { Properties = { new FbxProperty('S', string.Empty) } });
        metaData.Children.Add(new FbxNode("Comment") { Properties = { new FbxProperty('S', string.Empty) } });
        sceneInfo.Children.Add(metaData);
        return sceneInfo;
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
        Matrix4x4 pre = Matrix4x4.CreateFromQuaternion(ComposeEulerRotation(preRotation, 0));
        Matrix4x4 localRot = Matrix4x4.CreateFromQuaternion(ComposeEulerRotation(rotation, rotationOrder));
        Matrix4x4 post = Matrix4x4.CreateFromQuaternion(ComposeEulerRotation(postRotation, 0));
        Matrix4x4 postInv = Matrix4x4.Invert(post, out Matrix4x4 invertedPost) ? invertedPost : Matrix4x4.Identity;
        Matrix4x4 s = Matrix4x4.CreateScale(scaling);
        return spInv * s * sp * soff * rpInv * postInv * localRot * pre * rp * roff * t;
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
        return s * r * t;
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
    /// <param name="modelNodes">Map updated with the new camera node.</param>
    /// <param name="camerasByModelId">Map updated with the new camera.</param>
    public static void CreateCameraNode(FbxNode objectNode, long objectId, Dictionary<long, SceneNode> modelNodes, Dictionary<long, Camera> camerasByModelId)
    {
        string name = GetNodeObjectName(objectNode);
        Camera camera = new(name);
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
    /// <param name="modelNodes">Map updated with the new light node.</param>
    /// <param name="lightsByModelId">Map updated with the new light.</param>
    public static void CreateLightNode(FbxNode objectNode, long objectId, Dictionary<long, SceneNode> modelNodes, Dictionary<long, Light> lightsByModelId)
    {
        string name = GetNodeObjectName(objectNode);
        Light light = new(name);
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
    /// No-op during import — null node attribute information is derived from node type on export.
    /// </summary>
    public static void AttachNullNodeAttributes(Dictionary<long, SceneNode> modelNodes, Dictionary<long, FbxNode> objectsById, IReadOnlyList<FbxConnection> connections)
    {
        // Null node attribute metadata is no longer stored. On export, ShouldExportNullNodeAttribute
        // determines eligibility from node type, and GetNullNodeAttributeName generates the name.
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
    public static void AddDoubleProperty(
        FbxNode properties,
        string propertyName,
        string propertyType,
        double value)
    {
        FbxNode property = properties.AddChild("P");
        property.Properties.Add(new FbxProperty('S', propertyName));
        property.Properties.Add(new FbxProperty('S', propertyType));
        property.Properties.Add(new FbxProperty('S', GetPropertyAttributeType(propertyType)));
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
        attr.Children.Add(new FbxNode("TypeFlags") { Properties = { new FbxProperty('S', "Camera") } });
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
        attr.Children.Add(new FbxNode("TypeFlags") { Properties = { new FbxProperty('S', "Light") } });
        FbxNode properties = attr.AddChild("Properties70");
        AddVectorProperty(properties, "Color", "Color", light.Color);
        AddDoubleProperty(properties, "Intensity", "Number", light.Intensity * 100.0);
        AddIntProperty(properties, "CastLight", "bool", light.Enabled ? 1 : 0);
        AddIntProperty(properties, "LightType", "enum", 0);
        return attr;
    }

    /// <summary>
    /// Conditionally creates and connects a Null node attribute for the given model when applicable.
    /// </summary>
    /// <param name="node">The scene node to inspect.</param>
    /// <param name="modelId">The FBX model object identifier.</param>
    /// <param name="objectsNode">The FBX Objects node.</param>
    /// <param name="connectionsNode">The FBX Connections node.</param>
    /// <param name="nextId">The mutable object id counter.</param>
    private static void ExportNullNodeAttributeIfNeeded(SceneNode node, long modelId, FbxNode objectsNode, FbxNode connectionsNode, ref long nextId)
    {
        if (!ShouldExportNullNodeAttribute(node))
        {
            return;
        }

        long nodeAttributeId = nextId++;
        string nodeAttributeName = GetNullNodeAttributeName(node);
        objectsNode.Children.Add(CreateNullNodeAttribute(nodeAttributeId, nodeAttributeName));
        AddConnection(connectionsNode, "OO", nodeAttributeId, modelId);
    }

    /// <summary>
    /// Returns the FBX Properties70 attribute-type string for the given property data-type.
    /// Blender's importer asserts this field matches the expected type (e.g. <c>"Number"</c>
    /// for <c>"double"</c>, <c>"Integer"</c> for <c>"int"</c>).
    /// </summary>
    /// <param name="propertyType">The FBX property type string (field [1] of a P node).</param>
    /// <returns>The attribute-type string for field [2] of the P node.</returns>
    private static string GetPropertyAttributeType(string propertyType) => propertyType switch
    {
        "int" => "Integer",
        "double" or "float" => "Number",
        "KTime" => "Time",
        "ColorRGB" => "Color",
        "Vector3D" => "Vector",
        _ => string.Empty,
    };

    private static void ValidateExportMaterialConnections(Mesh mesh, IReadOnlyDictionary<Material, long> materialIds)
    {
        if (mesh.Materials is not { Count: > 0 })
        {
            return;
        }

        for (int materialIndex = 0; materialIndex < mesh.Materials.Count; materialIndex++)
        {
            Material material = mesh.Materials[materialIndex];
            if (!materialIds.ContainsKey(material))
            {
                throw new InvalidDataException(
                    $"Cannot write FBX: mesh '{mesh.Name}' references material '{material.Name}' for slot {materialIndex} that is not included in the export selection.");
            }
        }
    }

    private static void ValidateExportSkinning(Mesh mesh, IReadOnlyDictionary<SkeletonBone, long> boneIds)
    {
        if (!mesh.HasSkinning || mesh.SkinnedBones is not { Count: > 0 } skinnedBones)
        {
            return;
        }

        List<string> missingBones = [];
        for (int boneIndex = 0; boneIndex < skinnedBones.Count; boneIndex++)
        {
            SkeletonBone bone = skinnedBones[boneIndex];
            if (!boneIds.ContainsKey(bone))
            {
                missingBones.Add(bone.Name);
            }
        }

        if (missingBones.Count > 0)
        {
            throw new InvalidDataException(
                $"Cannot write FBX: mesh '{mesh.Name}' references skinned bones that are not included in the export selection: {string.Join(", ", missingBones)}.");
        }
    }
}
