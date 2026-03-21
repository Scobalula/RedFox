using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;

using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.IO
{
    /// <summary>
    /// Manages scene translators and coordinates import/export plus manager-side merge behavior.
    /// </summary>
    public class SceneTranslatorManager
    {
        private readonly List<SceneTranslator> _translators = [];

        private const int DefaultHeaderSize = 256;

        /// <summary>
        /// Gets a read-only list of all registered translators.
        /// </summary>
        public IReadOnlyList<SceneTranslator> Translators => _translators;

        public void Register(SceneTranslator translator)
        {
            _translators.RemoveAll(t => t.Name == translator.Name);
            _translators.Add(translator);
        }

        public bool TryGetTranslator(string filePath, string extension, SceneTranslatorOptions options, [NotNullWhen(true)] out SceneTranslator? translator)
        {
            foreach (var potentialTranslator in Translators)
            {
                if (potentialTranslator.IsValid(filePath, extension, options))
                {
                    translator = potentialTranslator;
                    return true;
                }
            }

            translator = null;
            return false;
        }

        public bool TryGetTranslator(
            string filePath,
            string extension,
            ReadOnlySpan<byte> header,
            SceneTranslatorOptions options,
            [NotNullWhen(true)] out SceneTranslator? translator)
        {
            foreach (var potentialTranslator in Translators)
            {
                if (potentialTranslator.IsValid(header, filePath, extension, options))
                {
                    translator = potentialTranslator;
                    return true;
                }
            }

            translator = null;
            return false;
        }

        /// <summary>
        /// Removes a previously registered translator by name.
        /// </summary>
        /// <param name="name">The name of the translator to remove.</param>
        /// <returns><c>true</c> if a translator was removed; otherwise, <c>false</c>.</returns>
        public bool Unregister(string name)
        {
            return _translators.RemoveAll(t => t.Name == name) > 0;
        }

        public Scene Read(string filePath, SceneTranslatorOptions options, CancellationToken? token)
        {
            var scene = new Scene(Path.GetFileName(filePath));
            Read(filePath, scene, options, token);
            return scene;
        }

        public void Read(string filePath, Scene scene, SceneTranslatorOptions options, CancellationToken? token)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            using var stream = File.OpenRead(filePath);
            Read(stream, filePath, scene, options, token);
        }

        public Scene Read(Stream stream, string filePath, SceneTranslatorOptions options, CancellationToken? token)
        {
            var scene = new Scene(Path.GetFileName(filePath));
            Read(stream, filePath, scene, options, token);
            return scene;
        }

        public void Read(Stream stream, string filePath, Scene scene, SceneTranslatorOptions options, CancellationToken? token)
        {
            if (!stream.CanRead)
                throw new IOException("The supplied stream is not readable.");
            if (!stream.CanSeek)
                throw new IOException("The supplied stream must support seeking.");

            var extension = Path.GetExtension(filePath);
            var readStart = stream.Position;

            Span<byte> header = stackalloc byte[DefaultHeaderSize];
            var headerSize = stream.Read(header);

            if (headerSize <= 0)
                throw new IOException($"Failed to read header from stream for file: {filePath}");

            if (!TryGetTranslator(filePath, extension, header[..headerSize], options, out var translator))
                throw new IOException($"No suitable translator found for file: {filePath}");

            stream.Position = readStart;

            var importName = Path.GetFileNameWithoutExtension(filePath);
            if (string.IsNullOrWhiteSpace(importName))
                importName = "ImportedScene";

            // Stage into a temporary scene first, then merge into the destination scene.
            var importedScene = new Scene(importName);
            translator.Read(importedScene, stream, importName, options, token);
            MergeImportedScene(scene, importedScene, options);
        }

        public void Write(string filePath, Scene scene, SceneTranslatorOptions options, CancellationToken? token)
        {
            var extension = Path.GetExtension(filePath);

            if (!TryGetTranslator(filePath, extension, options, out var translator))
                throw new IOException($"No suitable translator found for file: {filePath}");

            using var stream = File.Create(filePath);
            translator.Write(scene, stream, Path.GetFileNameWithoutExtension(filePath), options, token);
        }

        public void Write(Stream stream, string filePath, Scene scene, SceneTranslatorOptions options, CancellationToken? token)
        {
            var extension = Path.GetExtension(filePath);

            if (!TryGetTranslator(filePath, extension, options, out var translator))
                throw new IOException($"No suitable translator found for file: {filePath}");

            translator.Write(scene, stream, Path.GetFileNameWithoutExtension(filePath), options, token);
        }

        private static void MergeImportedScene(Scene destination, Scene imported, SceneTranslatorOptions options)
        {
            var importedRootChildren = imported.RootNode.EnumerateChildren().ToArray();
            if (importedRootChildren.Length == 0)
                return;

            var importedSkeletons = imported.EnumerateDescendants<Skeleton>()
                .Where(static s => s is not SkeletonBone)
                .ToArray();
            var importedMeshes = imported.EnumerateDescendants<Mesh>().ToArray();

            if (!string.IsNullOrEmpty(options.BoneNamespace))
            {
                foreach (var skeleton in importedSkeletons)
                    skeleton.ApplyNamespace(options.BoneNamespace);
            }

            var boneRemap = new Dictionary<SkeletonBone, SkeletonBone>();
            var skeletonRemap = new Dictionary<Skeleton, Skeleton>();

            if (importedSkeletons.Length > 0
                && options.BoneConnection is BoneConnectionMode.MergeByName or BoneConnectionMode.ConnectToTarget)
            {
                var destinationSkeleton = ResolveDestinationSkeleton(destination, options);

                if (destinationSkeleton is not null)
                {
                    foreach (var importedSkeleton in importedSkeletons)
                    {
                        // Skeleton may already have been detached by previous merge work.
                        if (importedSkeleton.Parent is null)
                            continue;

                        if (options.BoneConnection == BoneConnectionMode.MergeByName)
                            MergeSkeletonByName(destinationSkeleton, importedSkeleton, boneRemap, options);
                        else
                            ConnectSkeletonToTarget(destinationSkeleton, importedSkeleton, boneRemap, options);

                        skeletonRemap[importedSkeleton] = destinationSkeleton;
                    }
                }
            }

            bool forceDuplicateMerge = options.BoneConnection == BoneConnectionMode.MergeRoots;

            foreach (var importedRootChild in importedRootChildren)
            {
                // Node may already be consumed/removed during skeleton merge.
                if (!ReferenceEquals(importedRootChild.Parent, imported.RootNode))
                    continue;

                MergeNodeHierarchy(destination.RootNode, importedRootChild, options, forceDuplicateMerge);
            }

            RemapImportedMeshSkinning(destination, importedMeshes, boneRemap, skeletonRemap, options);
        }

        private static Skeleton? ResolveDestinationSkeleton(Scene destination, SceneTranslatorOptions options)
        {
            var destinationSkeletons = destination.EnumerateDescendants<Skeleton>()
                .Where(static s => s is not SkeletonBone)
                .ToArray();

            if (destinationSkeletons.Length == 0)
                return null;

            if (string.IsNullOrWhiteSpace(options.TargetSkeletonName))
                return destinationSkeletons[0];

            var target = destinationSkeletons.FirstOrDefault(s =>
                s.Name.Equals(options.TargetSkeletonName, options.NameComparison));

            if (target is not null)
                return target;

            throw new InvalidOperationException(
                $"Target skeleton '{options.TargetSkeletonName}' was not found in destination scene.");
        }

        private static void MergeSkeletonByName(
            Skeleton destinationSkeleton,
            Skeleton importedSkeleton,
            Dictionary<SkeletonBone, SkeletonBone> boneRemap,
            SceneTranslatorOptions options)
        {
            var children = importedSkeleton.EnumerateChildren().ToArray();

            foreach (var child in children)
            {
                if (child is SkeletonBone importedBone)
                    MergeIncomingBoneByName(destinationSkeleton, destinationSkeleton, importedBone, boneRemap, options);
                else
                    MergeNodeHierarchy(destinationSkeleton, child, options);
            }

            if (!importedSkeleton.EnumerateChildren().Any())
                importedSkeleton.Detach();
        }

        private static void ConnectSkeletonToTarget(
            Skeleton destinationSkeleton,
            Skeleton importedSkeleton,
            Dictionary<SkeletonBone, SkeletonBone> boneRemap,
            SceneTranslatorOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.TargetAttachBone))
            {
                throw new InvalidOperationException(
                    $"{nameof(BoneConnectionMode.ConnectToTarget)} requires {nameof(SceneTranslatorOptions.TargetAttachBone)} to be set.");
            }

            if (!destinationSkeleton.TryFindBone(options.TargetAttachBone, options.NameComparison, out var targetBone) || targetBone is null)
            {
                throw new KeyNotFoundException(
                    $"Target bone '{options.TargetAttachBone}' was not found in destination skeleton '{destinationSkeleton.Name}'.");
            }

            var sourceBones = new List<SkeletonBone>();

            if (!string.IsNullOrWhiteSpace(options.SourceAttachBone))
            {
                if (!importedSkeleton.TryFindBone(options.SourceAttachBone, options.NameComparison, out var sourceBone) || sourceBone is null)
                {
                    throw new KeyNotFoundException(
                        $"Source bone '{options.SourceAttachBone}' was not found in imported skeleton '{importedSkeleton.Name}'.");
                }

                sourceBones.Add(sourceBone);
            }
            else
            {
                sourceBones.AddRange(importedSkeleton.EnumerateChildren().OfType<SkeletonBone>());
            }

            foreach (var sourceBone in sourceBones.ToArray())
                MergeIncomingBoneByName(destinationSkeleton, targetBone, sourceBone, boneRemap, options);

            // Move any non-bone leftovers to the destination skeleton root.
            var leftovers = importedSkeleton.EnumerateChildren().ToArray();
            foreach (var leftover in leftovers)
                MergeNodeHierarchy(destinationSkeleton, leftover, options);

            if (!importedSkeleton.EnumerateChildren().Any())
                importedSkeleton.Detach();
        }

        private static void MergeIncomingBoneByName(
            Skeleton destinationSkeleton,
            SceneNode destinationParent,
            SkeletonBone incomingBone,
            Dictionary<SkeletonBone, SkeletonBone> boneRemap,
            SceneTranslatorOptions options)
        {
            if (destinationSkeleton.TryFindBone(incomingBone.Name, options.NameComparison, out var existingBoneByName)
                && existingBoneByName is not null
                && !ReferenceEquals(existingBoneByName, incomingBone))
            {
                switch (options.BoneDuplicates)
                {
                    case BoneDuplicateMode.Merge:
                    case BoneDuplicateMode.SkipIncoming:
                        boneRemap[incomingBone] = existingBoneByName;

                        var mergedChildren = incomingBone.EnumerateChildren().ToArray();
                        foreach (var child in mergedChildren)
                        {
                            if (child is SkeletonBone childBone)
                                MergeIncomingBoneByName(destinationSkeleton, existingBoneByName, childBone, boneRemap, options);
                            else
                                MergeNodeHierarchy(existingBoneByName, child, options);
                        }

                        incomingBone.Detach();
                        return;

                    case BoneDuplicateMode.Throw:
                        throw new SceneNodeDuplicateException(
                            $"Bone '{incomingBone.Name}' already exists in destination skeleton '{destinationSkeleton.Name}'.");

                    case BoneDuplicateMode.RenameIncoming:
                        incomingBone.Name = MakeUniqueBoneName(destinationSkeleton, incomingBone.Name, options);
                        break;
                }
            }

            // SceneNode duplicate checks are case-insensitive at parent scope.
            if (FindDuplicateChild(destinationParent, incomingBone.Name) is SkeletonBone siblingDuplicate
                && !ReferenceEquals(siblingDuplicate, incomingBone))
            {
                switch (options.BoneDuplicates)
                {
                    case BoneDuplicateMode.Merge:
                    case BoneDuplicateMode.SkipIncoming:
                        boneRemap[incomingBone] = siblingDuplicate;

                        var children = incomingBone.EnumerateChildren().ToArray();
                        foreach (var child in children)
                        {
                            if (child is SkeletonBone childBone)
                                MergeIncomingBoneByName(destinationSkeleton, siblingDuplicate, childBone, boneRemap, options);
                            else
                                MergeNodeHierarchy(siblingDuplicate, child, options);
                        }

                        incomingBone.Detach();
                        return;

                    case BoneDuplicateMode.Throw:
                        throw new SceneNodeDuplicateException(
                            $"Bone '{incomingBone.Name}' already exists under '{destinationParent.Name}'.");

                    case BoneDuplicateMode.RenameIncoming:
                        incomingBone.Name = MakeUniqueBoneName(destinationSkeleton, incomingBone.Name, options);
                        break;
                }
            }

            incomingBone.MoveTo(destinationParent, options.ImportReparentTransformMode);
            boneRemap[incomingBone] = incomingBone;

            // Recurse children after possible move so deeper duplicates can be resolved too.
            var postMoveChildren = incomingBone.EnumerateChildren().ToArray();
            foreach (var child in postMoveChildren)
            {
                if (child is SkeletonBone childBone)
                    MergeIncomingBoneByName(destinationSkeleton, incomingBone, childBone, boneRemap, options);
                else
                    MergeNodeHierarchy(incomingBone, child, options);
            }
        }

        private static void MergeNodeHierarchy(
            SceneNode destinationParent,
            SceneNode incomingNode,
            SceneTranslatorOptions options,
            bool forceMergeChildren = false)
        {
            var existing = FindDuplicateChild(destinationParent, incomingNode.Name);

            if (existing is null)
            {
                incomingNode.MoveTo(destinationParent, options.ImportReparentTransformMode);
                return;
            }

            var duplicateMode = forceMergeChildren ? DuplicateNodeMode.MergeChildren : options.NodeDuplicates;

            switch (duplicateMode)
            {
                case DuplicateNodeMode.Throw:
                    throw new SceneNodeDuplicateException(
                        $"A node named '{incomingNode.Name}' already exists under '{destinationParent.Name}'.");

                case DuplicateNodeMode.SkipIncoming:
                    incomingNode.Detach();
                    break;

                case DuplicateNodeMode.RenameIncoming:
                    incomingNode.Name = MakeUniqueNodeName(destinationParent, incomingNode.Name, options);
                    incomingNode.MoveTo(destinationParent, options.ImportReparentTransformMode);
                    break;

                case DuplicateNodeMode.MergeChildren:
                    var children = incomingNode.EnumerateChildren().ToArray();
                    foreach (var child in children)
                        MergeNodeHierarchy(existing, child, options, forceMergeChildren);

                    incomingNode.Detach();
                    break;
            }
        }

        private static SceneNode? FindDuplicateChild(SceneNode parent, string name)
        {
            return parent.EnumerateChildren()
                .FirstOrDefault(c => c.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
        }

        private static string MakeUniqueNodeName(SceneNode destinationParent, string baseName, SceneTranslatorOptions options)
        {
            var safeBaseName = string.IsNullOrWhiteSpace(baseName) ? "Node" : baseName;
            var index = Math.Max(0, options.DuplicateNameStartIndex);
            var candidate = safeBaseName;

            while (FindDuplicateChild(destinationParent, candidate) is not null)
            {
                candidate = safeBaseName + FormatDuplicateSuffix(options, index);
                index++;
            }

            return candidate;
        }

        private static string MakeUniqueBoneName(Skeleton destinationSkeleton, string baseName, SceneTranslatorOptions options)
        {
            var safeBaseName = string.IsNullOrWhiteSpace(baseName) ? "Bone" : baseName;
            var index = Math.Max(0, options.DuplicateNameStartIndex);
            var candidate = safeBaseName;

            while (destinationSkeleton.TryFindBone(candidate, options.NameComparison, out _))
            {
                candidate = safeBaseName + FormatDuplicateSuffix(options, index);
                index++;
            }

            return candidate;
        }

        private static string FormatDuplicateSuffix(SceneTranslatorOptions options, int index)
        {
            try
            {
                var suffix = string.Format(
                    CultureInfo.InvariantCulture,
                    options.DuplicateNameSuffixFormat,
                    index);

                return string.IsNullOrEmpty(suffix)
                    ? $"_{index.ToString(CultureInfo.InvariantCulture)}"
                    : suffix;
            }
            catch (FormatException)
            {
                return $"_{index.ToString(CultureInfo.InvariantCulture)}";
            }
        }

        private static void RemapImportedMeshSkinning(
            Scene destination,
            Mesh[] importedMeshes,
            Dictionary<SkeletonBone, SkeletonBone> boneRemap,
            Dictionary<Skeleton, Skeleton> skeletonRemap,
            SceneTranslatorOptions options)
        {
            foreach (var mesh in importedMeshes)
            {
                if (!IsNodeAttachedToScene(mesh, destination))
                    continue;

                if (mesh.SkinnedBones is { Count: > 0 } skinnedBones)
                {
                    var oldPalette = skinnedBones.ToArray();
                    var newPalette = new SkeletonBone[oldPalette.Length];
                    bool remappedAnyBone = false;
                    var oldInverseBind = mesh.InverseBindMatrices is { Count: > 0 }
                        ? mesh.InverseBindMatrices.ToArray()
                        : null;
                    var newInverseBind = oldInverseBind is not null
                        ? new Matrix4x4[oldPalette.Length]
                        : null;

                    for (int i = 0; i < oldPalette.Length; i++)
                    {
                        var oldBone = oldPalette[i];
                        if (boneRemap.TryGetValue(oldBone, out var mappedBone) && !ReferenceEquals(oldBone, mappedBone))
                        {
                            newPalette[i] = mappedBone;
                            remappedAnyBone = true;
                        }
                        else
                        {
                            newPalette[i] = oldBone;
                        }

                        if (newInverseBind is not null)
                        {
                            var sourceInverseBind = i < oldInverseBind!.Length ? oldInverseBind[i] : Matrix4x4.Identity;
                            var oldBindWorld = ComputeBindWorldMatrix(oldBone, new());
                            var newBindWorld = ComputeBindWorldMatrix(newPalette[i], new());

                            if (!Matrix4x4.Invert(newBindWorld, out var invNewBindWorld))
                                invNewBindWorld = Matrix4x4.Identity;

                            // Preserve skin result when remapping to a different bind frame:
                            // newInvBind = inv(newBindWorld) * oldBindWorld * oldInvBind
                            newInverseBind[i] = invNewBindWorld * oldBindWorld * sourceInverseBind;
                        }
                    }

                    if (remappedAnyBone && options.RebaseSkinnedMeshVerticesForRemappedBones)
                        RebaseSkinnedMeshForRemappedBones(mesh, oldPalette, newPalette);

                    mesh.SetSkinBinding(
                        ResolveOwningSkeleton(newPalette) ?? mesh.SkinSkeleton,
                        newPalette,
                        newInverseBind);

                    if (options.CollapseDuplicateSkinnedBones)
                        CollapseSkinnedBonePalette(mesh);

                    mesh.EnsureInverseBindPalette();

                    var owningSkeleton = ResolveOwningSkeleton(mesh.SkinnedBones);
                    if (owningSkeleton is not null)
                        mesh.SkinSkeleton = owningSkeleton;
                }
                else if (mesh.SkinSkeleton is not null
                    && skeletonRemap.TryGetValue(mesh.SkinSkeleton, out var mappedSkeleton))
                {
                    mesh.SkinSkeleton = mappedSkeleton;
                }
            }
        }

        private static bool IsNodeAttachedToScene(SceneNode node, Scene scene)
        {
            return ReferenceEquals(node.GetRoot(), scene.RootNode);
        }

        private static Skeleton? ResolveOwningSkeleton(IReadOnlyList<SkeletonBone>? palette)
        {
            if (palette is null || palette.Count == 0)
                return null;

            foreach (var bone in palette)
            {
                var owner = bone.EnumerateAncestors()
                    .OfType<Skeleton>()
                    .FirstOrDefault(static s => s is not SkeletonBone);

                if (owner is not null)
                    return owner;
            }

            return null;
        }

        private static void CollapseSkinnedBonePalette(Mesh mesh)
        {
            if (mesh.SkinnedBones is not { Count: > 0 } palette)
                return;

            var oldPalette = palette.ToArray();
            var oldInverseBind = mesh.InverseBindMatrices?.ToArray();
            var newPalette = new List<SkeletonBone>(oldPalette.Length);
            var newInverseBind = oldInverseBind is not null
                ? new List<Matrix4x4>(oldInverseBind.Length)
                : null;
            var existingByBone = new Dictionary<SkeletonBone, int>(oldPalette.Length);
            var oldToNew = new int[oldPalette.Length];
            bool hadDuplicates = false;

            for (int i = 0; i < oldPalette.Length; i++)
            {
                var bone = oldPalette[i];
                if (existingByBone.TryGetValue(bone, out int mappedIndex))
                {
                    oldToNew[i] = mappedIndex;
                    hadDuplicates = true;
                    continue;
                }

                int newIndex = newPalette.Count;
                existingByBone.Add(bone, newIndex);
                newPalette.Add(bone);
                if (newInverseBind is not null)
                    newInverseBind.Add(i < oldInverseBind!.Length ? oldInverseBind[i] : Matrix4x4.Identity);
                oldToNew[i] = newIndex;
            }

            if (!hadDuplicates)
                return;

            if (mesh.BoneIndices is not null)
            {
                var oldIndices = mesh.BoneIndices;
                var remappedIndices = new DataBuffer<int>(
                    oldIndices.ElementCount,
                    oldIndices.ValueCount,
                    oldIndices.ComponentCount);

                for (int e = 0; e < oldIndices.ElementCount; e++)
                {
                    for (int v = 0; v < oldIndices.ValueCount; v++)
                    {
                        for (int c = 0; c < oldIndices.ComponentCount; c++)
                        {
                            int oldIndex = oldIndices.Get<int>(e, v, c);
                            int newIndex = oldIndex >= 0 && oldIndex < oldToNew.Length
                                ? oldToNew[oldIndex]
                                : oldIndex;
                            remappedIndices.Add(e, v, c, newIndex);
                        }
                    }
                }

                mesh.BoneIndices = remappedIndices;
            }

            mesh.SetSkinBinding(
                ResolveOwningSkeleton(newPalette) ?? mesh.SkinSkeleton,
                newPalette,
                newInverseBind);
        }

        private static void RebaseSkinnedMeshForRemappedBones(
            Mesh mesh,
            IReadOnlyList<SkeletonBone> oldPalette,
            IReadOnlyList<SkeletonBone> newPalette)
        {
            if (mesh.Positions is null
                || mesh.BoneIndices is null
                || mesh.BoneWeights is null
                || oldPalette.Count != newPalette.Count
                || mesh.Positions.ComponentCount < 3)
            {
                return;
            }

            var deltaByPaletteIndex = new Dictionary<int, Matrix4x4>();
            var worldCache = new Dictionary<SceneNode, Matrix4x4>();

            for (int i = 0; i < oldPalette.Count; i++)
            {
                var oldBone = oldPalette[i];
                var newBone = newPalette[i];

                if (ReferenceEquals(oldBone, newBone))
                    continue;

                var oldBind = ComputeBindWorldMatrix(oldBone, worldCache);
                var newBind = ComputeBindWorldMatrix(newBone, worldCache);

                if (!Matrix4x4.Invert(oldBind, out var invOldBind))
                    continue;

                // Row-vector convention in System.Numerics:
                // v' = v * Inv(OldBind) * NewBind
                deltaByPaletteIndex[i] = invOldBind * newBind;
            }

            if (deltaByPaletteIndex.Count == 0)
                return;

            var positions = mesh.Positions;
            if (positions.IsReadOnly)
            {
                positions = CloneToWritableFloatBuffer(positions);
                mesh.Positions = positions;
            }

            DataBuffer? normals = mesh.Normals;
            if (normals is not null && normals.ComponentCount >= 3 && normals.IsReadOnly)
            {
                normals = CloneToWritableFloatBuffer(normals);
                mesh.Normals = normals;
            }

            int influenceCount = Math.Min(mesh.BoneIndices.ValueCount, mesh.BoneWeights.ValueCount);

            for (int vertexIndex = 0; vertexIndex < positions.ElementCount; vertexIndex++)
            {
                var basePosition = positions.GetVector3(vertexIndex, 0);
                var accumPosition = Vector3.Zero;
                float remappedWeight = 0f;

                Vector3 baseNormal = Vector3.Zero;
                var accumNormal = Vector3.Zero;
                bool canAdjustNormal = normals is not null && normals.ComponentCount >= 3;

                if (canAdjustNormal)
                    baseNormal = normals!.GetVector3(vertexIndex, 0);

                for (int influenceIndex = 0; influenceIndex < influenceCount; influenceIndex++)
                {
                    float weight = mesh.BoneWeights.Get<float>(vertexIndex, influenceIndex, 0);
                    if (weight <= 0f)
                        continue;

                    int paletteIndex = mesh.BoneIndices.Get<int>(vertexIndex, influenceIndex, 0);
                    if (!deltaByPaletteIndex.TryGetValue(paletteIndex, out var delta))
                        continue;

                    remappedWeight += weight;
                    accumPosition += Vector3.Transform(basePosition, delta) * weight;

                    if (canAdjustNormal)
                    {
                        var transformedNormal = Vector3.TransformNormal(baseNormal, delta);
                        if (transformedNormal.LengthSquared() > 0f)
                            transformedNormal = Vector3.Normalize(transformedNormal);

                        accumNormal += transformedNormal * weight;
                    }
                }

                if (remappedWeight <= 0f)
                    continue;

                float untouchedWeight = MathF.Max(0f, 1f - remappedWeight);
                var finalPosition = accumPosition + (basePosition * untouchedWeight);

                positions.Set(vertexIndex, 0, 0, finalPosition.X);
                positions.Set(vertexIndex, 0, 1, finalPosition.Y);
                positions.Set(vertexIndex, 0, 2, finalPosition.Z);

                if (canAdjustNormal)
                {
                    var finalNormal = accumNormal + (baseNormal * untouchedWeight);
                    if (finalNormal.LengthSquared() > 0f)
                        finalNormal = Vector3.Normalize(finalNormal);
                    else
                        finalNormal = baseNormal;

                    normals!.Set(vertexIndex, 0, 0, finalNormal.X);
                    normals.Set(vertexIndex, 0, 1, finalNormal.Y);
                    normals.Set(vertexIndex, 0, 2, finalNormal.Z);
                }
            }
        }

        private static DataBuffer CloneToWritableFloatBuffer(DataBuffer source)
        {
            var clone = new DataBuffer<float>(source.ElementCount, source.ValueCount, source.ComponentCount);

            for (int e = 0; e < source.ElementCount; e++)
            {
                for (int v = 0; v < source.ValueCount; v++)
                {
                    for (int c = 0; c < source.ComponentCount; c++)
                    {
                        clone.Add(e, v, c, source.Get<float>(e, v, c));
                    }
                }
            }

            return clone;
        }

        private static Matrix4x4 ComputeBindWorldMatrix(
            SkeletonBone bone,
            Dictionary<SceneNode, Matrix4x4> cache)
        {
            if (cache.TryGetValue(bone, out var cached))
                return cached;

            var localPos = bone.GetBindLocalPosition();
            var localRot = bone.GetBindLocalRotation();
            var localScale = bone.GetBindLocalScale();

            var local = Matrix4x4.CreateScale(localScale)
                      * Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(localRot))
                      * Matrix4x4.CreateTranslation(localPos);

            Matrix4x4 world;
            if (bone.Parent is SkeletonBone parentBone)
                world = local * ComputeBindWorldMatrix(parentBone, cache);
            else
                world = local;

            cache[bone] = world;
            return world;
        }
    }
}
