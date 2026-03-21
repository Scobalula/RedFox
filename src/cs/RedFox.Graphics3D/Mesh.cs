using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Skeletal;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// Represents a renderable mesh node with optional vertex attributes, skinning metadata,
    /// morph target data, and polygon topology information.
    /// </summary>
    public class Mesh : SceneNode
    {
        private SkeletonBone[]? _skinnedBones;
        private Matrix4x4[]? _inverseBindMatrices;

        /// <summary>
        /// Gets or sets the data buffer that contains vertex position information for the mesh.
        /// </summary>
        public DataBuffer? Positions { get; set; }

        /// <summary>
        /// Gets or sets the normals data associated with the geometry.
        /// </summary>
        public DataBuffer? Normals { get; set; }

        /// <summary>
        /// Gets or sets the vertex tangent buffer. When present, uses dimension 3 or 4
        /// (X, Y, Z [, W]) per vertex. The optional W component encodes the bitangent sign.
        /// </summary>
        public DataBuffer? Tangents { get; set; }

        /// <summary>
        /// Gets or sets the vertex bitangent (binormal) buffer. When present, uses dimension 3
        /// (X, Y, Z) per vertex. Together with normals and tangents, forms the TBN matrix.
        /// </summary>
        public DataBuffer? BiTangents { get; set; }

        /// <summary>
        /// Gets or sets the color layers associated with the data buffer.
        /// </summary>
        public DataBuffer? ColorLayers { get; set; }

        /// <summary>
        /// Gets or sets the UV layer data buffer associated with the mesh.
        /// </summary>
        public DataBuffer? UVLayers { get; set; }

        /// <summary>
        /// Gets or sets the buffer containing the indices of bones used in the skeletal structure.
        /// When <see cref="SkinnedBones"/> is present, values map into that ordered bone table.
        /// Importers that preserve source skeleton order can populate <see cref="SkinnedBones"/> with the full ordered skeleton.
        /// </summary>
        public DataBuffer? BoneIndices { get; set; }

        /// <summary>
        /// Gets or sets the bone weights associated with the mesh, which determine the influence of each bone on the
        /// mesh's vertices.
        /// </summary>
        public DataBuffer? BoneWeights { get; set; }

        /// <summary>
        /// Gets or Sets the delta positions for use in morph animations. The index points to the morph target in the main model that owns this mesh.
        /// </summary>
        public DataBuffer? DeltaPositions { get; set; }

        /// <summary>
        /// Gets or sets the delta normals for use in morph animations. The value index maps to the morph target
        /// in the owning mesh, matching the target order used by <see cref="DeltaPositions"/>.
        /// </summary>
        public DataBuffer? DeltaNormals { get; set; }

        /// <summary>
        /// Gets or sets the delta tangents for use in morph animations. The value index maps to the morph target
        /// in the owning mesh, matching the target order used by <see cref="DeltaPositions"/>.
        /// </summary>
        public DataBuffer? DeltaTangents { get; set; }

        /// <summary>
        /// Gets or sets the vertex indices that define the faces in the mesh.
        /// When <see cref="FaceVertexCounts"/> is <see langword="null"/>, indices are interpreted as a triangle list.
        /// When <see cref="FaceVertexCounts"/> is present, indices are interpreted as a concatenated polygon corner list.
        /// </summary>
        public DataBuffer? FaceIndices { get; set; }

        /// <summary>
        /// Gets or sets the number of vertices that belong to each face.
        /// This enables polygon meshes with mixed face sizes, similar to the topology editors preserve.
        /// </summary>
        public DataBuffer? FaceVertexCounts { get; set; }

        /// <summary>
        /// Gets or sets the material slot index assigned to each face.
        /// This mirrors per-face shading assignments used by DCC tools such as Maya and Blender.
        /// </summary>
        public DataBuffer? FaceMaterialIndices { get; set; }

        /// <summary>
        /// Gets or sets the skeleton targeted by this mesh's skin binding.
        /// This is the mesh-level equivalent of the armature or skin deformer target in DCC tools.
        /// </summary>
        public Skeleton? SkinSkeleton { get; set; }

        /// <summary>
        /// Gets or sets the import-time name of the mesh skin binding.
        /// This can preserve deformer identity from tools such as Maya skin clusters or Blender armature modifiers
        /// without introducing a dedicated deformer type yet.
        /// </summary>
        public string? SkinBindingName { get; set; }

        /// <summary>
        /// Gets or sets the ordered influence palette used by <see cref="BoneIndices"/>.
        /// When present, each entry identifies the skeleton bone referenced by the corresponding index.
        /// This may be either a compact mesh-local palette or the full source skeleton order, depending on the importer.
        /// </summary>
        public IReadOnlyList<SkeletonBone>? SkinnedBones
        {
            get => _skinnedBones;
            set => SetSkinBinding(SkinSkeleton, value);
        }

        /// <summary>
        /// Gets or sets the inverse bind matrices aligned with <see cref="SkinnedBones"/>.
        /// Each matrix maps a vertex from mesh/object space into the corresponding bone's bind space.
        /// </summary>
        public IReadOnlyList<Matrix4x4>? InverseBindMatrices
        {
            get => _inverseBindMatrices;
            set
            {
                if (value is null)
                {
                    _inverseBindMatrices = null;
                    return;
                }

                if (_skinnedBones is { Length: > 0 } palette)
                    _inverseBindMatrices = CopyAndFillInverseBind(palette, value);
                else
                    _inverseBindMatrices = CopyToArray(value);
            }
        }

        /// <summary>
        /// Sets the skin binding for this mesh and keeps the inverse bind palette aligned.
        /// </summary>
        /// <param name="skeleton">The skeleton targeted by this skin binding.</param>
        /// <param name="skinnedBones">The ordered bone palette used by <see cref="BoneIndices"/>.</param>
        /// <param name="inverseBindMatrices">
        /// Optional inverse bind matrices aligned with <paramref name="skinnedBones"/>.
        /// If omitted, inverse bind matrices are generated from the current bind transforms.
        /// </param>
        public void SetSkinBinding(
            Skeleton? skeleton,
            IReadOnlyList<SkeletonBone>? skinnedBones)
            => SetSkinBinding(skeleton, skinnedBones, inverseBindMatrices: null);

        /// <summary>
        /// Sets the skin binding for this mesh and keeps the inverse bind palette aligned.
        /// </summary>
        /// <param name="skeleton">The skeleton targeted by this skin binding.</param>
        /// <param name="skinnedBones">The ordered bone palette used by <see cref="BoneIndices"/>.</param>
        /// <param name="inverseBindMatrices">
        /// Optional inverse bind matrices aligned with <paramref name="skinnedBones"/>.
        /// If omitted, inverse bind matrices are generated from the current bind transforms.
        /// </param>
        public void SetSkinBinding(
            Skeleton? skeleton,
            IReadOnlyList<SkeletonBone>? skinnedBones,
            IReadOnlyList<Matrix4x4>? inverseBindMatrices)
        {
            if (skinnedBones is null || skinnedBones.Count == 0)
            {
                SkinSkeleton = skeleton;
                _skinnedBones = null;
                _inverseBindMatrices = null;
                return;
            }

            var palette = CopyToArray(skinnedBones);
            _skinnedBones = palette;
            SkinSkeleton = skeleton ?? ResolveOwningSkeleton(palette);

            if (inverseBindMatrices is not null)
            {
                _inverseBindMatrices = CopyAndFillInverseBind(palette, inverseBindMatrices);
                return;
            }

            _inverseBindMatrices = BuildInverseBindPalette(palette);
        }

        /// <summary>
        /// Ensures an inverse bind palette exists and is aligned with <see cref="SkinnedBones"/>.
        /// </summary>
        public void EnsureInverseBindPalette()
        {
            if (_skinnedBones is not { Length: > 0 } palette)
                return;

            if (_inverseBindMatrices is { Length: var count } && count == palette.Length)
                return;

            _inverseBindMatrices = BuildInverseBindPalette(palette);
        }

        /// <summary>
        /// Rebuilds the inverse bind palette from the current bind transforms.
        /// </summary>
        public void RebuildInverseBindPalette()
        {
            if (_skinnedBones is not { Length: > 0 } palette)
            {
                _inverseBindMatrices = null;
                return;
            }

            _inverseBindMatrices = BuildInverseBindPalette(palette);
        }

        /// <summary>
        /// Adds a bone to the skin palette and appends a matching inverse bind matrix.
        /// </summary>
        /// <param name="bone">The bone to add.</param>
        /// <param name="inverseBindMatrix">Optional explicit inverse bind matrix for the added bone.</param>
        /// <returns>The palette index of the added (or existing) bone.</returns>
        public int AddSkinnedBone(SkeletonBone bone)
            => AddSkinnedBone(bone, inverseBindMatrix: null);

        /// <summary>
        /// Adds a bone to the skin palette and appends a matching inverse bind matrix.
        /// </summary>
        /// <param name="bone">The bone to add.</param>
        /// <param name="inverseBindMatrix">Optional explicit inverse bind matrix for the added bone.</param>
        /// <returns>The palette index of the added (or existing) bone.</returns>
        public int AddSkinnedBone(SkeletonBone bone, Matrix4x4? inverseBindMatrix)
        {
            ArgumentNullException.ThrowIfNull(bone);

            if (_skinnedBones is { Length: > 0 } oldPalette)
            {
                for (int i = 0; i < oldPalette.Length; i++)
                {
                    if (ReferenceEquals(oldPalette[i], bone))
                        return i;
                }

                EnsureInverseBindPalette();

                var newPalette = new SkeletonBone[oldPalette.Length + 1];
                var newInverseBind = new Matrix4x4[newPalette.Length];

                Array.Copy(oldPalette, newPalette, oldPalette.Length);
                Array.Copy(_inverseBindMatrices!, newInverseBind, oldPalette.Length);

                newPalette[^1] = bone;
                newInverseBind[^1] = inverseBindMatrix ?? ComputeInverseBindMatrix(bone, new());

                _skinnedBones = newPalette;
                _inverseBindMatrices = newInverseBind;
            }
            else
            {
                _skinnedBones = [bone];
                _inverseBindMatrices = [inverseBindMatrix ?? ComputeInverseBindMatrix(bone, new())];
            }

            if (SkinSkeleton is null)
                SkinSkeleton = ResolveOwningSkeleton(_skinnedBones);

            return _skinnedBones.Length - 1;
        }

        /// <summary>
        /// Removes a bone from the skin palette and updates skin indices/weights accordingly.
        /// </summary>
        /// <param name="bone">The bone to remove.</param>
        /// <returns><see langword="true"/> when removed; otherwise <see langword="false"/>.</returns>
        public bool RemoveSkinnedBone(SkeletonBone bone)
        {
            ArgumentNullException.ThrowIfNull(bone);

            if (_skinnedBones is not { Length: > 0 } palette)
                return false;

            for (int i = 0; i < palette.Length; i++)
            {
                if (ReferenceEquals(palette[i], bone))
                    return RemoveSkinnedBoneAt(i);
            }

            return false;
        }

        /// <summary>
        /// Removes the palette entry at the specified index and rewrites influenced skin data.
        /// </summary>
        /// <param name="paletteIndex">The palette index to remove.</param>
        /// <returns><see langword="true"/> when removed; otherwise <see langword="false"/>.</returns>
        public bool RemoveSkinnedBoneAt(int paletteIndex)
        {
            if (_skinnedBones is not { Length: > 0 } palette)
                return false;

            if ((uint)paletteIndex >= (uint)palette.Length)
                throw new ArgumentOutOfRangeException(nameof(paletteIndex), $"Palette index must be between 0 and {palette.Length - 1}.");

            int oldPaletteCount = palette.Length;

            if (oldPaletteCount == 1)
            {
                _skinnedBones = null;
                _inverseBindMatrices = null;
                SkinSkeleton = null;
                ZeroAllSkinInfluences();
                return true;
            }

            EnsureInverseBindPalette();

            var newPalette = new SkeletonBone[oldPaletteCount - 1];
            var newInverseBind = new Matrix4x4[oldPaletteCount - 1];

            int dst = 0;
            for (int src = 0; src < oldPaletteCount; src++)
            {
                if (src == paletteIndex)
                    continue;

                newPalette[dst] = palette[src];
                newInverseBind[dst] = _inverseBindMatrices![src];
                dst++;
            }

            _skinnedBones = newPalette;
            _inverseBindMatrices = newInverseBind;

            RemapSkinInfluencesAfterPaletteRemoval(paletteIndex, oldPaletteCount);

            var owner = ResolveOwningSkeleton(newPalette);
            if (owner is not null)
                SkinSkeleton = owner;

            return true;
        }

        /// <summary>
        /// Gets the number of vertices described by the mesh buffers.
        /// </summary>
        public int VertexCount => Positions?.ElementCount
            ?? Normals?.ElementCount
            ?? Tangents?.ElementCount
            ?? BiTangents?.ElementCount
            ?? ColorLayers?.ElementCount
            ?? UVLayers?.ElementCount
            ?? BoneIndices?.ElementCount
            ?? BoneWeights?.ElementCount
            ?? DeltaPositions?.ElementCount
            ?? DeltaNormals?.ElementCount
            ?? DeltaTangents?.ElementCount
            ?? 0;

        /// <summary>
        /// Gets the number of vertex references stored in <see cref="FaceIndices"/>.
        /// </summary>
        public int IndexCount => FaceIndices?.ElementCount ?? 0;

        /// <summary>
        /// Gets the number of faces described by the mesh topology.
        /// </summary>
        public int FaceCount => FaceVertexCounts?.ElementCount ?? (FaceIndices?.ElementCount ?? 0) / 3;

        /// <summary>
        /// Gets the number of UV layers stored on the mesh.
        /// </summary>
        public int UVLayerCount => UVLayers?.ValueCount ?? 0;

        /// <summary>
        /// Gets the number of color layers stored on the mesh.
        /// </summary>
        public int ColorLayerCount => ColorLayers?.ValueCount ?? 0;

        /// <summary>
        /// Gets the number of skin influences stored per vertex.
        /// </summary>
        public int SkinInfluenceCount => BoneIndices?.ValueCount ?? BoneWeights?.ValueCount ?? 0;

        /// <summary>
        /// Gets the number of morph targets described by the delta buffers.
        /// </summary>
        public int MorphTargetCount => DeltaPositions?.ValueCount
            ?? DeltaNormals?.ValueCount
            ?? DeltaTangents?.ValueCount
            ?? 0;

        /// <summary>
        /// Gets a value indicating whether the mesh contains indexed topology.
        /// </summary>
        public bool IsIndexed => FaceIndices is not null;

        /// <summary>
        /// Gets a value indicating whether the mesh contains explicit polygon face sizes.
        /// </summary>
        public bool HasPolygonFaces => FaceVertexCounts is not null;

        /// <summary>
        /// Gets a value indicating whether the mesh contains skinning data.
        /// </summary>
        public bool HasSkinning => BoneIndices is not null && BoneWeights is not null;

        /// <summary>
        /// Gets a value indicating whether the mesh contains morph target data.
        /// </summary>
        public bool HasMorphTargets => DeltaPositions is not null || DeltaNormals is not null || DeltaTangents is not null;

        /// <summary>
        /// Gets a value indicating whether the mesh uses a named influence palette for skinning.
        /// </summary>
        public bool HasExplicitSkinPalette => _skinnedBones is not null && _skinnedBones.Length > 0;

        /// <summary>
        /// Gets a value indicating whether the mesh has explicit inverse bind matrices.
        /// </summary>
        public bool HasInverseBindPalette =>
            _inverseBindMatrices is not null
            && _skinnedBones is not null
            && _inverseBindMatrices.Length == _skinnedBones.Length;

        /// <summary>
        /// Gets a value indicating whether the mesh contains per-face material assignments.
        /// </summary>
        public bool HasPerFaceMaterials => FaceMaterialIndices is not null;

        private static T[] CopyToArray<T>(IReadOnlyList<T> source)
        {
            var result = new T[source.Count];
            for (int i = 0; i < source.Count; i++)
                result[i] = source[i];
            return result;
        }

        private static Skeleton? ResolveOwningSkeleton(IReadOnlyList<SkeletonBone> palette)
        {
            for (int i = 0; i < palette.Count; i++)
            {
                foreach (var ancestor in palette[i].EnumerateAncestors())
                {
                    if (ancestor is Skeleton owner && ancestor is not SkeletonBone)
                        return owner;
                }
            }

            return null;
        }

        private void ZeroAllSkinInfluences()
        {
            if (BoneIndices is not null)
            {
                if (BoneIndices.IsReadOnly)
                    BoneIndices = CloneToWritableIntBuffer(BoneIndices);

                for (int e = 0; e < BoneIndices.ElementCount; e++)
                {
                    for (int v = 0; v < BoneIndices.ValueCount; v++)
                    {
                        for (int c = 0; c < BoneIndices.ComponentCount; c++)
                        {
                            BoneIndices.Set(e, v, c, 0);
                        }
                    }
                }
            }

            if (BoneWeights is not null)
            {
                if (BoneWeights.IsReadOnly)
                    BoneWeights = CloneToWritableFloatBuffer(BoneWeights);

                for (int e = 0; e < BoneWeights.ElementCount; e++)
                {
                    for (int v = 0; v < BoneWeights.ValueCount; v++)
                    {
                        for (int c = 0; c < BoneWeights.ComponentCount; c++)
                        {
                            BoneWeights.Set(e, v, c, 0f);
                        }
                    }
                }
            }
        }

        private void RemapSkinInfluencesAfterPaletteRemoval(int removedIndex, int oldPaletteCount)
        {
            if (BoneIndices is null)
                return;

            if (BoneIndices.IsReadOnly)
                BoneIndices = CloneToWritableIntBuffer(BoneIndices);

            if (BoneWeights is not null && BoneWeights.IsReadOnly)
                BoneWeights = CloneToWritableFloatBuffer(BoneWeights);

            int influenceCount = BoneIndices.ValueCount;
            int weightedInfluenceCount = BoneWeights is not null
                ? Math.Min(influenceCount, BoneWeights.ValueCount)
                : 0;

            for (int vertexIndex = 0; vertexIndex < BoneIndices.ElementCount; vertexIndex++)
            {
                for (int influenceIndex = 0; influenceIndex < influenceCount; influenceIndex++)
                {
                    int oldIndex = BoneIndices.Get<int>(vertexIndex, influenceIndex, 0);
                    int newIndex;
                    bool removedReference = false;

                    if (oldIndex == removedIndex)
                    {
                        newIndex = 0;
                        removedReference = true;
                    }
                    else if (oldIndex > removedIndex && oldIndex < oldPaletteCount)
                    {
                        newIndex = oldIndex - 1;
                    }
                    else if (oldIndex < 0 || oldIndex >= oldPaletteCount)
                    {
                        newIndex = 0;
                        removedReference = true;
                    }
                    else
                    {
                        newIndex = oldIndex;
                    }

                    if (newIndex != oldIndex)
                        BoneIndices.Set(vertexIndex, influenceIndex, 0, newIndex);

                    if (removedReference && BoneWeights is not null && influenceIndex < weightedInfluenceCount)
                        BoneWeights.Set(vertexIndex, influenceIndex, 0, 0f);
                }

                if (BoneWeights is null || weightedInfluenceCount == 0)
                    continue;

                float totalWeight = 0f;
                for (int influenceIndex = 0; influenceIndex < weightedInfluenceCount; influenceIndex++)
                {
                    totalWeight += MathF.Max(0f, BoneWeights.Get<float>(vertexIndex, influenceIndex, 0));
                }

                if (totalWeight <= 0f)
                    continue;

                float invTotalWeight = 1f / totalWeight;
                for (int influenceIndex = 0; influenceIndex < weightedInfluenceCount; influenceIndex++)
                {
                    float normalizedWeight = MathF.Max(0f, BoneWeights.Get<float>(vertexIndex, influenceIndex, 0)) * invTotalWeight;
                    BoneWeights.Set(vertexIndex, influenceIndex, 0, normalizedWeight);
                }
            }
        }

        private static DataBuffer<int> CloneToWritableIntBuffer(DataBuffer source)
        {
            var clone = new DataBuffer<int>(source.ElementCount, source.ValueCount, source.ComponentCount);

            for (int e = 0; e < source.ElementCount; e++)
            {
                for (int v = 0; v < source.ValueCount; v++)
                {
                    for (int c = 0; c < source.ComponentCount; c++)
                    {
                        clone.Add(e, v, c, source.Get<int>(e, v, c));
                    }
                }
            }

            return clone;
        }

        private static DataBuffer<float> CloneToWritableFloatBuffer(DataBuffer source)
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

        private static Matrix4x4[] CopyAndFillInverseBind(IReadOnlyList<SkeletonBone> palette, IReadOnlyList<Matrix4x4> source)
        {
            var result = new Matrix4x4[palette.Count];
            int copyCount = Math.Min(source.Count, palette.Count);

            for (int i = 0; i < copyCount; i++)
                result[i] = source[i];

            if (copyCount < palette.Count)
            {
                var cache = new Dictionary<SceneNode, Matrix4x4>(palette.Count);

                for (int i = copyCount; i < palette.Count; i++)
                    result[i] = ComputeInverseBindMatrix(palette[i], cache);
            }

            return result;
        }

        private static Matrix4x4[] BuildInverseBindPalette(IReadOnlyList<SkeletonBone> palette)
        {
            var result = new Matrix4x4[palette.Count];
            var cache = new Dictionary<SceneNode, Matrix4x4>(palette.Count);

            for (int i = 0; i < palette.Count; i++)
                result[i] = ComputeInverseBindMatrix(palette[i], cache);

            return result;
        }

        private static Matrix4x4 ComputeInverseBindMatrix(
            SkeletonBone bone,
            Dictionary<SceneNode, Matrix4x4> cache)
        {
            var bindWorld = ComputeBindWorldMatrix(bone, cache);
            return Matrix4x4.Invert(bindWorld, out var inverse)
                ? inverse
                : Matrix4x4.Identity;
        }

        private static Matrix4x4 ComputeBindWorldMatrix(
            SkeletonBone bone,
            Dictionary<SceneNode, Matrix4x4> cache)
        {
            if (cache.TryGetValue(bone, out var cached))
                return cached;

            var local = Matrix4x4.CreateScale(bone.GetBindLocalScale())
                      * Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(bone.GetBindLocalRotation()))
                      * Matrix4x4.CreateTranslation(bone.GetBindLocalPosition());

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
