using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.Rendering.Handles;
using RedFox.Graphics3D.Rendering.Materials;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// Represents a renderable mesh node with optional vertex attributes, skinning metadata,
    /// and morph target data.
    /// </summary>
    public class Mesh : SceneNode
    {
        private SkeletonBone[]? _skinnedBones;
        private Matrix4x4[]? _inverseBindMatrices;
        private SceneBounds[]? _cachedSkinBounds;
        private bool _hasExplicitInverseBindMatrices;

        /// <summary>
        /// Gets or sets the data buffer that contains vertex position information for the mesh.
        /// </summary>
        public DataBuffer? Positions
        {
            get;
            set
            {
                field = value;
                InvalidateSkinBoundsCache();
            }
        }

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
        public DataBuffer? BoneIndices
        {
            get;
            set
            {
                field = value;
                InvalidateSkinBoundsCache();
            }
        }

        /// <summary>
        /// Gets or sets the bone weights associated with the mesh, which determine the influence of each bone on the
        /// mesh's vertices.
        /// </summary>
        public DataBuffer? BoneWeights
        {
            get;
            set
            {
                field = value;
                InvalidateSkinBoundsCache();
            }
        }

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
        /// Gets or sets the vertex indices that define the faces in the mesh as a triangle list.
        /// </summary>
        public DataBuffer? FaceIndices { get; set; }

        /// <summary>
        /// Gets or sets the import-time name of the mesh skin binding.
        /// This can preserve deformer identity from tools such as Maya skin clusters or Blender armature modifiers
        /// without introducing a dedicated deformer type yet.
        /// </summary>
        public string? SkinBindingName { get; set; }

        /// <summary>
        /// Gets or sets the ordered skinned bones used by <see cref="BoneIndices"/>.
        /// When present, each entry identifies the skeleton bone referenced by the corresponding index.
        /// This may be either a compact mesh-local list or the full source skeleton order, depending on the importer.
        /// </summary>
        public IReadOnlyList<SkeletonBone>? SkinnedBones
        {
            get => _skinnedBones;
            set => SetSkinBinding(value);
        }

        /// <summary>
        /// Gets or sets the collection of materials associated with this mesh.
        /// </summary>
        public List<Material>? Materials { get; set; }

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
                    _hasExplicitInverseBindMatrices = false;
                    return;
                }

                if (_skinnedBones is { Length: > 0 } skinnedBones)
                    _inverseBindMatrices = CopyAndFillInverseBindMatrices(skinnedBones, value);
                else
                    _inverseBindMatrices = CopyToArray(value);

                _cachedSkinBounds = null;
                _hasExplicitInverseBindMatrices = true;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current inverse bind matrices were supplied explicitly
        /// rather than generated from the active bind transforms.
        /// </summary>
        public bool HasExplicitInverseBindMatrices => _hasExplicitInverseBindMatrices;

        /// <summary>
        /// Invalidates the cached animated bounds. Call this after mutating skinning-related buffers in place.
        /// </summary>
        public void InvalidateSkinBoundsCache()
            => _cachedSkinBounds = null;

        /// <inheritdoc/>
        public override IRenderHandle? CreateRenderHandle(IGraphicsDevice graphicsDevice, IMaterialTypeRegistry materialTypes)
        {
            return new MeshRenderHandle(graphicsDevice, materialTypes, this);
        }

        internal bool TryGetActiveSkinBounds(out SceneBounds bounds)
        {
            if (!HasSkinning
                || Positions is not { ElementCount: > 0 }
                || BoneIndices is null
                || BoneWeights is null
                || _skinnedBones is not { Length: > 0 } skinnedBones)
            {
                bounds = SceneBounds.Invalid;
                return false;
            }

            EnsureInverseBindMatrices();
            if (_inverseBindMatrices is not { Length: var inverseBindCount } || inverseBindCount != skinnedBones.Length)
            {
                bounds = SceneBounds.Invalid;
                return false;
            }

            _cachedSkinBounds ??= BuildSkinBounds();
            if (_cachedSkinBounds.Length == 0)
            {
                bounds = SceneBounds.Invalid;
                return false;
            }

            bool hasBounds = false;
            Vector3 min = new(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new(float.MinValue, float.MinValue, float.MinValue);

            for (int i = 0; i < _cachedSkinBounds.Length; i++)
            {
                SceneBounds boneBounds = _cachedSkinBounds[i];
                if (!boneBounds.IsValid)
                {
                    continue;
                }

                ExpandBounds(boneBounds, skinnedBones[i].GetActiveWorldMatrix(), ref hasBounds, ref min, ref max);
            }

            bounds = hasBounds ? new SceneBounds(min, max) : SceneBounds.Invalid;
            return bounds.IsValid;
        }

        /// <inheritdoc/>
        public override bool TryGetSceneBounds(out SceneBounds bounds)
        {
            if (TryGetActiveSkinBounds(out bounds))
            {
                return true;
            }

            if (Positions is not { ElementCount: > 0 })
            {
                bounds = SceneBounds.Invalid;
                return false;
            }

            bool hasBounds = false;
            Vector3 min = new(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new(float.MinValue, float.MinValue, float.MinValue);
            Matrix4x4 world = GetActiveWorldMatrix();

            for (int i = 0; i < Positions.ElementCount; i++)
            {
                Vector3 worldPosition = Vector3.Transform(Positions.GetVector3(i, 0), world);
                if (!hasBounds)
                {
                    min = worldPosition;
                    max = worldPosition;
                    hasBounds = true;
                }
                else
                {
                    min = Vector3.Min(min, worldPosition);
                    max = Vector3.Max(max, worldPosition);
                }
            }

            bounds = hasBounds ? new SceneBounds(min, max) : SceneBounds.Invalid;
            return bounds.IsValid;
        }

        /// <summary>
        /// Sets the skin binding for this mesh and keeps the inverse bind matrices aligned.
        /// </summary>
        /// <param name="skinnedBones">The ordered skinned bones used by <see cref="BoneIndices"/>.</param>
        /// <param name="inverseBindMatrices">
        /// Optional inverse bind matrices aligned with <paramref name="skinnedBones"/>.
        /// If omitted, inverse bind matrices are generated from the current bind transforms.
        /// </param>
        public void SetSkinBinding(IReadOnlyList<SkeletonBone>? skinnedBones)
            => SetSkinBinding(skinnedBones, inverseBindMatrices: null);

        /// <summary>
        /// Sets the skin binding for this mesh and keeps the inverse bind matrices aligned.
        /// </summary>
        /// <param name="skinnedBones">The ordered skinned bones used by <see cref="BoneIndices"/>.</param>
        /// <param name="inverseBindMatrices">
        /// Optional inverse bind matrices aligned with <paramref name="skinnedBones"/>.
        /// If omitted, inverse bind matrices are generated from the current bind transforms.
        /// </param>
        public void SetSkinBinding(IReadOnlyList<SkeletonBone>? skinnedBones, IReadOnlyList<Matrix4x4>? inverseBindMatrices)
        {
            if (skinnedBones is null || skinnedBones.Count == 0)
            {
                _skinnedBones = null;
                _inverseBindMatrices = null;
                _cachedSkinBounds = null;
                _hasExplicitInverseBindMatrices = false;
                return;
            }

            var skinnedBoneArray = CopyToArray(skinnedBones);
            _skinnedBones = skinnedBoneArray;

            if (inverseBindMatrices is not null)
            {
                _inverseBindMatrices = CopyAndFillInverseBindMatrices(skinnedBoneArray, inverseBindMatrices);
                _cachedSkinBounds = null;
                _hasExplicitInverseBindMatrices = true;
                return;
            }

            _inverseBindMatrices = CreateInverseBindMatrices(skinnedBoneArray);
            _cachedSkinBounds = null;
            _hasExplicitInverseBindMatrices = false;
        }

        /// <summary>
        /// Ensures inverse bind matrices exist and are aligned with <see cref="SkinnedBones"/>.
        /// </summary>
        public void EnsureInverseBindMatrices()
        {
            if (_skinnedBones is not { Length: > 0 } skinnedBones)
                return;

            if (_inverseBindMatrices is { Length: var count } && count == skinnedBones.Length)
                return;

            _inverseBindMatrices = CreateInverseBindMatrices(skinnedBones);
            _cachedSkinBounds = null;
            _hasExplicitInverseBindMatrices = false;
        }

        /// <summary>
        /// Rebuilds the inverse bind matrices from the current bind transforms.
        /// </summary>
        public void RebuildInverseBindMatrices()
        {
            if (_skinnedBones is not { Length: > 0 } skinnedBones)
            {
                _inverseBindMatrices = null;
                _hasExplicitInverseBindMatrices = false;
                return;
            }

            _inverseBindMatrices = CreateInverseBindMatrices(skinnedBones);
            _cachedSkinBounds = null;
            _hasExplicitInverseBindMatrices = false;
        }

        /// <summary>
        /// Rebuilds the inverse bind matrices using the specified mesh bind world matrix
        /// instead of the current scene graph transform. This preserves bind-time positioning
        /// transforms that the scene graph may not capture (e.g., FBX cluster geometric offsets).
        /// </summary>
        /// <param name="meshBindWorld">The mesh's world matrix at bind time.</param>
        public void RebuildInverseBindMatrices(Matrix4x4 meshBindWorld)
        {
            if (_skinnedBones is not { Length: > 0 } skinnedBones)
            {
                _inverseBindMatrices = null;
                _hasExplicitInverseBindMatrices = false;
                return;
            }

            var result = new Matrix4x4[skinnedBones.Length];
            Dictionary<SceneNode, Matrix4x4> cache = [];

            for (int i = 0; i < skinnedBones.Length; i++)
            {
                Matrix4x4 boneBindWorld = ComputeCachedBindWorldMatrix(skinnedBones[i], cache);
                result[i] = Matrix4x4.Invert(boneBindWorld, out Matrix4x4 inverseBoneBindWorld)
                    ? meshBindWorld * inverseBoneBindWorld
                    : Matrix4x4.Identity;
            }

            _inverseBindMatrices = result;
            _cachedSkinBounds = null;
            _hasExplicitInverseBindMatrices = false;
        }

        /// <summary>
        /// Adds a skinned bone and appends a matching inverse bind matrix.
        /// </summary>
        /// <param name="bone">The bone to add.</param>
        /// <param name="inverseBindMatrix">Optional explicit inverse bind matrix for the added bone.</param>
        /// <returns>The skin index of the added (or existing) bone.</returns>
        public int AddSkinnedBone(SkeletonBone bone)
            => AddSkinnedBone(bone, inverseBindMatrix: null);

        /// <summary>
        /// Adds a skinned bone and appends a matching inverse bind matrix.
        /// </summary>
        /// <param name="bone">The bone to add.</param>
        /// <param name="inverseBindMatrix">Optional explicit inverse bind matrix for the added bone.</param>
        /// <returns>The skin index of the added (or existing) bone.</returns>
        public int AddSkinnedBone(SkeletonBone bone, Matrix4x4? inverseBindMatrix)
        {
            ArgumentNullException.ThrowIfNull(bone);

            if (_skinnedBones is { Length: > 0 } oldSkinnedBones)
            {
                for (int i = 0; i < oldSkinnedBones.Length; i++)
                {
                    if (ReferenceEquals(oldSkinnedBones[i], bone))
                        return i;
                }

                EnsureInverseBindMatrices();

                var newSkinnedBones = new SkeletonBone[oldSkinnedBones.Length + 1];
                var newInverseBindMatrices = new Matrix4x4[newSkinnedBones.Length];

                Array.Copy(oldSkinnedBones, newSkinnedBones, oldSkinnedBones.Length);
                Array.Copy(_inverseBindMatrices!, newInverseBindMatrices, oldSkinnedBones.Length);

                newSkinnedBones[^1] = bone;
                newInverseBindMatrices[^1] = inverseBindMatrix ?? CreateInverseBindMatrix(bone, new());

                _skinnedBones = newSkinnedBones;
                _inverseBindMatrices = newInverseBindMatrices;
                _cachedSkinBounds = null;
                _hasExplicitInverseBindMatrices = _hasExplicitInverseBindMatrices && inverseBindMatrix.HasValue;
            }
            else
            {
                _skinnedBones = [bone];
                _inverseBindMatrices = [inverseBindMatrix ?? CreateInverseBindMatrix(bone, new())];
                _cachedSkinBounds = null;
                _hasExplicitInverseBindMatrices = inverseBindMatrix.HasValue;
            }

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

            if (_skinnedBones is not { Length: > 0 } skinnedBones)
                return false;

            for (int i = 0; i < skinnedBones.Length; i++)
            {
                if (ReferenceEquals(skinnedBones[i], bone))
                    return RemoveSkinnedBoneAt(i);
            }

            return false;
        }

        /// <summary>
        /// Removes the skinned bone at the specified index and rewrites influenced skin data.
        /// </summary>
        /// <param name="skinIndex">The skin index to remove.</param>
        /// <returns><see langword="true"/> when removed; otherwise <see langword="false"/>.</returns>
        public bool RemoveSkinnedBoneAt(int skinIndex)
        {
            if (_skinnedBones is not { Length: > 0 } skinnedBones)
                return false;

            if ((uint)skinIndex >= (uint)skinnedBones.Length)
                throw new ArgumentOutOfRangeException(nameof(skinIndex), $"Skin index must be between 0 and {skinnedBones.Length - 1}.");

            int oldSkinnedBoneCount = skinnedBones.Length;

            if (oldSkinnedBoneCount == 1)
            {
                _skinnedBones = null;
                _inverseBindMatrices = null;
                _cachedSkinBounds = null;
                ZeroAllSkinInfluences();
                return true;
            }

            EnsureInverseBindMatrices();

            var newSkinnedBones = new SkeletonBone[oldSkinnedBoneCount - 1];
            var newInverseBindMatrices = new Matrix4x4[oldSkinnedBoneCount - 1];

            int dst = 0;
            for (int src = 0; src < oldSkinnedBoneCount; src++)
            {
                if (src == skinIndex)
                    continue;

                newSkinnedBones[dst] = skinnedBones[src];
                newInverseBindMatrices[dst] = _inverseBindMatrices![src];
                dst++;
            }

            _skinnedBones = newSkinnedBones;
            _inverseBindMatrices = newInverseBindMatrices;
            _cachedSkinBounds = null;

            RemapSkinInfluencesAfterSkinRemoval(skinIndex, oldSkinnedBoneCount);

            return true;
        }

        /// <summary>
        /// Replaces skinned bone references in-place without modifying indices, weights, or inverse bind matrices.
        /// This is intended for skeleton merge operations where the same inverse bind matrix should follow a new bone reference.
        /// </summary>
        /// <param name="remap">The mapping from old bone references to new bone references.</param>
        /// <returns>The number of skinned bone entries that were updated.</returns>
        public int RemapSkinnedBones(IReadOnlyDictionary<SkeletonBone, SkeletonBone> remap)
        {
            ArgumentNullException.ThrowIfNull(remap);

            if (_skinnedBones is not { Length: > 0 } skinnedBones || remap.Count == 0)
                return 0;

            int remappedCount = 0;
            for (int i = 0; i < skinnedBones.Length; i++)
            {
                if (!remap.TryGetValue(skinnedBones[i], out SkeletonBone? replacement) || ReferenceEquals(replacement, skinnedBones[i]))
                    continue;

                skinnedBones[i] = replacement;
                remappedCount++;
            }

            if (remappedCount > 0)
                _cachedSkinBounds = null;

            return remappedCount;
        }

        /// <summary>
        /// Bakes the current skinned deformation into the vertex buffers and clears the skin binding.
        /// The current visual pose becomes the new raw mesh data.
        /// </summary>
        public void BakeCurrentSkinningToVertices()
        {
            if (!HasSkinning || _skinnedBones is not { Length: > 0 } skinnedBones)
                return;

            Matrix4x4[] skinTransforms = new Matrix4x4[skinnedBones.Length];
            CopySkinTransforms(skinTransforms);

            if (Positions is not null)
            {
                if (Positions.IsReadOnly)
                    Positions = DataBuffer.CloneToWritable<float>(Positions);

                for (int vertexIndex = 0; vertexIndex < Positions.ElementCount; vertexIndex++)
                {
                    Vector3 position = GetVertexPosition(vertexIndex, skinTransforms);
                    Positions.Set(vertexIndex, 0, 0, position.X);
                    Positions.Set(vertexIndex, 0, 1, position.Y);
                    Positions.Set(vertexIndex, 0, 2, position.Z);
                }
            }

            if (Normals is not null)
            {
                if (Normals.IsReadOnly)
                    Normals = DataBuffer.CloneToWritable<float>(Normals);

                for (int vertexIndex = 0; vertexIndex < Normals.ElementCount; vertexIndex++)
                {
                    Vector3 normal = GetVertexNormal(vertexIndex, skinTransforms);
                    Normals.Set(vertexIndex, 0, 0, normal.X);
                    Normals.Set(vertexIndex, 0, 1, normal.Y);
                    Normals.Set(vertexIndex, 0, 2, normal.Z);
                }
            }

            if (Tangents is not null)
            {
                if (Tangents.IsReadOnly)
                    Tangents = DataBuffer.CloneToWritable<float>(Tangents);

                for (int vertexIndex = 0; vertexIndex < Tangents.ElementCount; vertexIndex++)
                {
                    Vector4 tangent = GetVertexTangent(vertexIndex, skinTransforms);
                    Tangents.Set(vertexIndex, 0, 0, tangent.X);
                    Tangents.Set(vertexIndex, 0, 1, tangent.Y);
                    Tangents.Set(vertexIndex, 0, 2, tangent.Z);

                    if (Tangents.ComponentCount > 3)
                        Tangents.Set(vertexIndex, 0, 3, tangent.W);
                }
            }

            if (BiTangents is not null)
            {
                if (BiTangents.IsReadOnly)
                    BiTangents = DataBuffer.CloneToWritable<float>(BiTangents);

                for (int vertexIndex = 0; vertexIndex < BiTangents.ElementCount; vertexIndex++)
                {
                    Vector3 bitangent = GetVertexBiTangent(vertexIndex, skinTransforms);
                    BiTangents.Set(vertexIndex, 0, 0, bitangent.X);
                    BiTangents.Set(vertexIndex, 0, 1, bitangent.Y);
                    BiTangents.Set(vertexIndex, 0, 2, bitangent.Z);
                }
            }

            BoneIndices = null;
            BoneWeights = null;
            _skinnedBones = null;
            _inverseBindMatrices = null;
            _cachedSkinBounds = null;
            SkinBindingName = null;
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
        /// Gets the number of triangle faces described by the mesh topology.
        /// </summary>
        public int FaceCount => (FaceIndices?.ElementCount ?? 0) / 3;

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

        public MeshFaceOrder FaceOrder { get; private set; }

        /// <summary>
        /// Gets the vertex position for the specified vertex index, applying the current skin pose when available.
        /// </summary>
        /// <param name="vertexIndex">The zero-based vertex index.</param>
        /// <returns>The resolved vertex position.</returns>
        public Vector3 GetVertexPosition(int vertexIndex) => GetVertexPosition(vertexIndex, raw: false);

        /// <summary>
        /// Copies the active skin transforms aligned to <see cref="SkinnedBones"/> into the destination span.
        /// </summary>
        /// <param name="destination">The destination span to receive one transform per skinned bone.</param>
        /// <returns>The number of transforms written.</returns>
        public int CopySkinTransforms(Span<Matrix4x4> destination)
        {
            if (!HasSkinning || _skinnedBones is not { Length: > 0 } skinnedBones)
                return 0;

            EnsureInverseBindMatrices();

            if (_inverseBindMatrices is not { Length: var count } || count != skinnedBones.Length)
                return 0;

            if (destination.Length < skinnedBones.Length)
                throw new ArgumentException($"Destination span must be at least {skinnedBones.Length} elements.", nameof(destination));

            for (int i = 0; i < skinnedBones.Length; i++)
                destination[i] = _inverseBindMatrices[i] * skinnedBones[i].GetActiveWorldMatrix();

            return skinnedBones.Length;
        }

        /// <summary>
        /// Gets the vertex position for the specified vertex index.
        /// </summary>
        /// <param name="vertexIndex">The zero-based vertex index.</param>
        /// <param name="raw"><see langword="true"/> to return the stored buffer value without skinning; otherwise the active skin pose is applied.</param>
        /// <returns>The resolved vertex position.</returns>
        public Vector3 GetVertexPosition(int vertexIndex, bool raw)
        {
            Vector3 position = GetRequiredVector3(Positions, vertexIndex, nameof(Positions));
            return raw ? position : ApplySkinningDirect(position, vertexIndex, transformAsDirection: false);
        }

        /// <summary>
        /// Gets the vertex position for the specified vertex index using precomputed skin transforms.
        /// </summary>
        /// <param name="vertexIndex">The zero-based vertex index.</param>
        /// <param name="skinTransforms">The precomputed transforms aligned to <see cref="SkinnedBones"/>.</param>
        /// <returns>The resolved vertex position.</returns>
        public Vector3 GetVertexPosition(int vertexIndex, ReadOnlySpan<Matrix4x4> skinTransforms)
        {
            Vector3 position = GetRequiredVector3(Positions, vertexIndex, nameof(Positions));
            return ApplySkinning(position, vertexIndex, transformAsDirection: false, skinTransforms);
        }

        /// <summary>
        /// Gets the vertex normal for the specified vertex index, applying the current skin pose when available.
        /// </summary>
        /// <param name="vertexIndex">The zero-based vertex index.</param>
        /// <returns>The resolved vertex normal.</returns>
        public Vector3 GetVertexNormal(int vertexIndex) => GetVertexNormal(vertexIndex, raw: false);

        /// <summary>
        /// Gets the vertex normal for the specified vertex index.
        /// </summary>
        /// <param name="vertexIndex">The zero-based vertex index.</param>
        /// <param name="raw"><see langword="true"/> to return the stored buffer value without skinning; otherwise the active skin pose is applied.</param>
        /// <returns>The resolved vertex normal.</returns>
        public Vector3 GetVertexNormal(int vertexIndex, bool raw)
        {
            Vector3 normal = GetRequiredVector3(Normals, vertexIndex, nameof(Normals));
            return raw ? normal : ApplySkinningDirect(normal, vertexIndex, transformAsDirection: true);
        }

        /// <summary>
        /// Gets the vertex normal for the specified vertex index using precomputed skin transforms.
        /// </summary>
        /// <param name="vertexIndex">The zero-based vertex index.</param>
        /// <param name="skinTransforms">The precomputed transforms aligned to <see cref="SkinnedBones"/>.</param>
        /// <returns>The resolved vertex normal.</returns>
        public Vector3 GetVertexNormal(int vertexIndex, ReadOnlySpan<Matrix4x4> skinTransforms)
        {
            Vector3 normal = GetRequiredVector3(Normals, vertexIndex, nameof(Normals));
            return ApplySkinning(normal, vertexIndex, transformAsDirection: true, skinTransforms);
        }

        /// <summary>
        /// Gets the vertex tangent for the specified vertex index, applying the current skin pose when available.
        /// </summary>
        /// <param name="vertexIndex">The zero-based vertex index.</param>
        /// <returns>The resolved vertex tangent. When the source tangent has four components, the W component is preserved.</returns>
        public Vector4 GetVertexTangent(int vertexIndex) => GetVertexTangent(vertexIndex, raw: false);

        /// <summary>
        /// Gets the vertex tangent for the specified vertex index.
        /// </summary>
        /// <param name="vertexIndex">The zero-based vertex index.</param>
        /// <param name="raw"><see langword="true"/> to return the stored buffer value without skinning; otherwise the active skin pose is applied.</param>
        /// <returns>The resolved vertex tangent. When the source tangent has four components, the W component is preserved.</returns>
        public Vector4 GetVertexTangent(int vertexIndex, bool raw)
        {
            if (Tangents is null)
                throw new InvalidOperationException($"Mesh '{Name}' has no {nameof(Tangents)} buffer.");

            Vector3 tangent = GetRequiredVector3(Tangents, vertexIndex, nameof(Tangents));
            Vector3 resolvedTangent = raw ? tangent : ApplySkinningDirect(tangent, vertexIndex, transformAsDirection: true);
            float handedness = Tangents.ComponentCount > 3 ? Tangents.Get<float>(vertexIndex, 0, 3) : 0f;
            return new Vector4(resolvedTangent, handedness);
        }

        /// <summary>
        /// Gets the vertex tangent for the specified vertex index using precomputed skin transforms.
        /// </summary>
        /// <param name="vertexIndex">The zero-based vertex index.</param>
        /// <param name="skinTransforms">The precomputed transforms aligned to <see cref="SkinnedBones"/>.</param>
        /// <returns>The resolved vertex tangent. When the source tangent has four components, the W component is preserved.</returns>
        public Vector4 GetVertexTangent(int vertexIndex, ReadOnlySpan<Matrix4x4> skinTransforms)
        {
            if (Tangents is null)
                throw new InvalidOperationException($"Mesh '{Name}' has no {nameof(Tangents)} buffer.");

            Vector3 tangent = GetRequiredVector3(Tangents, vertexIndex, nameof(Tangents));
            Vector3 resolvedTangent = ApplySkinning(tangent, vertexIndex, transformAsDirection: true, skinTransforms);
            float handedness = Tangents.ComponentCount > 3 ? Tangents.Get<float>(vertexIndex, 0, 3) : 0f;
            return new Vector4(resolvedTangent, handedness);
        }

        /// <summary>
        /// Gets the vertex bitangent for the specified vertex index, applying the current skin pose when available.
        /// </summary>
        /// <param name="vertexIndex">The zero-based vertex index.</param>
        /// <returns>The resolved vertex bitangent.</returns>
        public Vector3 GetVertexBiTangent(int vertexIndex) => GetVertexBiTangent(vertexIndex, raw: false);

        /// <summary>
        /// Gets the vertex bitangent for the specified vertex index.
        /// </summary>
        /// <param name="vertexIndex">The zero-based vertex index.</param>
        /// <param name="raw"><see langword="true"/> to return the stored buffer value without skinning; otherwise the active skin pose is applied.</param>
        /// <returns>The resolved vertex bitangent.</returns>
        public Vector3 GetVertexBiTangent(int vertexIndex, bool raw)
        {
            Vector3 bitangent = GetRequiredVector3(BiTangents, vertexIndex, nameof(BiTangents));
            return raw ? bitangent : ApplySkinningDirect(bitangent, vertexIndex, transformAsDirection: true);
        }

        /// <summary>
        /// Gets the vertex bitangent for the specified vertex index using precomputed skin transforms.
        /// </summary>
        /// <param name="vertexIndex">The zero-based vertex index.</param>
        /// <param name="skinTransforms">The precomputed transforms aligned to <see cref="SkinnedBones"/>.</param>
        /// <returns>The resolved vertex bitangent.</returns>
        public Vector3 GetVertexBiTangent(int vertexIndex, ReadOnlySpan<Matrix4x4> skinTransforms)
        {
            Vector3 bitangent = GetRequiredVector3(BiTangents, vertexIndex, nameof(BiTangents));
            return ApplySkinning(bitangent, vertexIndex, transformAsDirection: true, skinTransforms);
        }

        /// <summary>
        /// Generates vertex normals for this mesh.
        /// </summary>
        public void GenerateNormals()
            => GenerateNormals(NormalGenerationMode.EqualWeight, preserveExisting: false);

        /// <summary>
        /// Generates vertex normals for this mesh using the specified normal generation mode.
        /// </summary>
        /// <param name="mode">The algorithm or strategy to use when generating normals for the mesh.</param>
        public void GenerateNormals(NormalGenerationMode mode)
            => GenerateNormals(mode, preserveExisting: false);


        /// <summary>
        /// Generates vertex normals for this mesh.
        /// </summary>
        /// <param name="preserveExisting">If <see langword="true"/>, existing normals are preserved and normals are only generated if none exist.</param>
        public void GenerateNormals(bool preserveExisting)
            => GenerateNormals(NormalGenerationMode.EqualWeight, preserveExisting);

        /// <summary>
        /// Generates vertex normals for this mesh using the specified normal generation mode.
        /// </summary>
        /// <param name="mode">The algorithm or strategy to use when generating normals for the mesh.</param>
        /// <param name="preserveExisting">If <see langword="true"/>, existing normals are preserved and normals are only generated if none exist.</param>
        public void GenerateNormals(NormalGenerationMode mode, bool preserveExisting)
        {
            if (Normals is not null && preserveExisting)
                return;

            MeshNormals.Generate(this, mode, FaceOrder);
        }

        private Vector3 ApplySkinningDirect(Vector3 value, int vertexIndex, bool transformAsDirection)
        {
            if (BoneIndices is null || BoneWeights is null || _skinnedBones is not { Length: > 0 } skinnedBones)
                return value;

            EnsureInverseBindMatrices();

            if (_inverseBindMatrices is not { Length: var matrixCount } || matrixCount != skinnedBones.Length)
                return value;

            DataBuffer boneIndices = BoneIndices;
            DataBuffer boneWeights = BoneWeights;
            int influenceCount = Math.Min(boneIndices.ValueCount, boneWeights.ValueCount);
            if (influenceCount == 0)
                return value;

            Vector3 result = Vector3.Zero;
            float totalWeight = 0f;

            for (int influenceIndex = 0; influenceIndex < influenceCount; influenceIndex++)
            {
                float weight = boneWeights.Get<float>(vertexIndex, influenceIndex, 0);
                if (weight == 0f)
                    continue;

                int skinIndex = boneIndices.Get<int>(vertexIndex, influenceIndex, 0);
                if ((uint)skinIndex >= (uint)skinnedBones.Length)
                    throw new InvalidDataException($"Mesh '{Name}' contains an invalid skin index {skinIndex} at vertex {vertexIndex}.");

                Matrix4x4 skinTransform = _inverseBindMatrices[skinIndex] * skinnedBones[skinIndex].GetActiveWorldMatrix();

                Vector3 transformed = transformAsDirection
                    ? TransformDirectionByNormalMatrix(value, skinTransform)
                    : Vector3.Transform(value, skinTransform);

                result += transformed * weight;
                totalWeight += weight;
            }

            return FinalizeSkinnedVector(value, result, totalWeight, transformAsDirection);
        }

        private Vector3 ApplySkinning(Vector3 value, int vertexIndex, bool transformAsDirection, ReadOnlySpan<Matrix4x4> skinTransforms)
        {
            if (skinTransforms.IsEmpty || BoneIndices is null || BoneWeights is null)
                return value;

            DataBuffer boneIndices = BoneIndices;
            DataBuffer boneWeights = BoneWeights;
            int influenceCount = Math.Min(boneIndices.ValueCount, boneWeights.ValueCount);
            if (influenceCount == 0)
                return value;

            Vector3 result = Vector3.Zero;
            float totalWeight = 0f;

            for (int influenceIndex = 0; influenceIndex < influenceCount; influenceIndex++)
            {
                float weight = boneWeights.Get<float>(vertexIndex, influenceIndex, 0);
                if (weight == 0f)
                    continue;

                int skinIndex = boneIndices.Get<int>(vertexIndex, influenceIndex, 0);
                if ((uint)skinIndex >= (uint)skinTransforms.Length)
                    throw new InvalidDataException($"Mesh '{Name}' contains an invalid skin index {skinIndex} at vertex {vertexIndex}.");

                Matrix4x4 skinTransform = skinTransforms[skinIndex];

                Vector3 transformed = transformAsDirection
                    ? TransformDirectionByNormalMatrix(value, skinTransform)
                    : Vector3.Transform(value, skinTransform);

                result += transformed * weight;
                totalWeight += weight;
            }

            return FinalizeSkinnedVector(value, result, totalWeight, transformAsDirection);
        }

        private static Vector3 FinalizeSkinnedVector(Vector3 sourceValue, Vector3 weightedResult, float totalWeight, bool transformAsDirection)
        {
            if (totalWeight <= 0f)
                return sourceValue;

            if (transformAsDirection)
            {
                float lengthSquared = weightedResult.LengthSquared();
                return lengthSquared > 1e-12f ? weightedResult / MathF.Sqrt(lengthSquared) : sourceValue;
            }

            return weightedResult;
        }

        private static Vector3 TransformDirectionByNormalMatrix(Vector3 value, Matrix4x4 transform)
        {
            if (!Matrix4x4.Invert(transform, out Matrix4x4 inverse))
                return Vector3.TransformNormal(value, transform);

            Matrix4x4 normalMatrix = Matrix4x4.Transpose(inverse);
            return Vector3.TransformNormal(value, normalMatrix);
        }

        private Vector3 GetRequiredVector3(DataBuffer? buffer, int vertexIndex, string bufferName)
        {
            if (buffer is null)
                throw new InvalidOperationException($"Mesh '{Name}' has no {bufferName} buffer.");

            if ((uint)vertexIndex >= (uint)buffer.ElementCount)
                throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Vertex index must be between 0 and {buffer.ElementCount - 1}.");

            return buffer.GetVector3(vertexIndex, 0);
        }

        private static T[] CopyToArray<T>(IReadOnlyList<T> source)
        {
            var result = new T[source.Count];
            for (int i = 0; i < source.Count; i++)
                result[i] = source[i];
            return result;
        }

        private SceneBounds[] BuildSkinBounds()
        {
            if (Positions is null
                || BoneIndices is null
                || BoneWeights is null
                || _skinnedBones is not { Length: > 0 } skinnedBones
                || _inverseBindMatrices is not { Length: var inverseBindCount }
                || inverseBindCount != skinnedBones.Length)
            {
                return [];
            }

            DataBuffer positions = Positions;
            DataBuffer boneIndices = BoneIndices;
            DataBuffer boneWeights = BoneWeights;
            int influenceCount = Math.Min(boneIndices.ValueCount, boneWeights.ValueCount);

            Vector3[] mins = new Vector3[skinnedBones.Length];
            Vector3[] maxs = new Vector3[skinnedBones.Length];
            bool[] valid = new bool[skinnedBones.Length];

            for (int vertexIndex = 0; vertexIndex < positions.ElementCount; vertexIndex++)
            {
                Vector3 localPosition = positions.GetVector3(vertexIndex, 0);

                for (int influenceIndex = 0; influenceIndex < influenceCount; influenceIndex++)
                {
                    float weight = boneWeights.Get<float>(vertexIndex, influenceIndex, 0);
                    if (weight <= 0f)
                    {
                        continue;
                    }

                    int skinIndex = boneIndices.Get<int>(vertexIndex, influenceIndex, 0);
                    if ((uint)skinIndex >= (uint)skinnedBones.Length)
                    {
                        throw new InvalidDataException($"Mesh '{Name}' contains an invalid skin index {skinIndex} at vertex {vertexIndex}.");
                    }

                    Vector3 bindSpacePosition = Vector3.Transform(localPosition, _inverseBindMatrices[skinIndex]);
                    if (!valid[skinIndex])
                    {
                        mins[skinIndex] = bindSpacePosition;
                        maxs[skinIndex] = bindSpacePosition;
                        valid[skinIndex] = true;
                        continue;
                    }

                    mins[skinIndex] = Vector3.Min(mins[skinIndex], bindSpacePosition);
                    maxs[skinIndex] = Vector3.Max(maxs[skinIndex], bindSpacePosition);
                }
            }

            SceneBounds[] result = new SceneBounds[skinnedBones.Length];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = valid[i] ? new SceneBounds(mins[i], maxs[i]) : SceneBounds.Invalid;
            }

            return result;
        }

        private static void Expand(Vector3 point, ref bool hasBounds, ref Vector3 min, ref Vector3 max)
        {
            if (!hasBounds)
            {
                min = point;
                max = point;
                hasBounds = true;
                return;
            }

            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }

        private static void ExpandBounds(SceneBounds bounds, Matrix4x4 transform, ref bool hasBounds, ref Vector3 min, ref Vector3 max)
        {
            if (!bounds.IsValid)
            {
                return;
            }

            Vector3 minCorner = bounds.Min;
            Vector3 maxCorner = bounds.Max;

            Expand(Vector3.Transform(new Vector3(minCorner.X, minCorner.Y, minCorner.Z), transform), ref hasBounds, ref min, ref max);
            Expand(Vector3.Transform(new Vector3(maxCorner.X, minCorner.Y, minCorner.Z), transform), ref hasBounds, ref min, ref max);
            Expand(Vector3.Transform(new Vector3(minCorner.X, maxCorner.Y, minCorner.Z), transform), ref hasBounds, ref min, ref max);
            Expand(Vector3.Transform(new Vector3(maxCorner.X, maxCorner.Y, minCorner.Z), transform), ref hasBounds, ref min, ref max);
            Expand(Vector3.Transform(new Vector3(minCorner.X, minCorner.Y, maxCorner.Z), transform), ref hasBounds, ref min, ref max);
            Expand(Vector3.Transform(new Vector3(maxCorner.X, minCorner.Y, maxCorner.Z), transform), ref hasBounds, ref min, ref max);
            Expand(Vector3.Transform(new Vector3(minCorner.X, maxCorner.Y, maxCorner.Z), transform), ref hasBounds, ref min, ref max);
            Expand(Vector3.Transform(new Vector3(maxCorner.X, maxCorner.Y, maxCorner.Z), transform), ref hasBounds, ref min, ref max);
        }

        private void ZeroAllSkinInfluences()
        {
            if (BoneIndices is not null)
            {
                if (BoneIndices.IsReadOnly)
                        BoneIndices = DataBuffer.CloneToWritable<int>(BoneIndices);

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
                        BoneWeights = DataBuffer.CloneToWritable<float>(BoneWeights);

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

        private void RemapSkinInfluencesAfterSkinRemoval(int removedIndex, int oldSkinnedBoneCount)
        {
            if (BoneIndices is null)
                return;
            if (BoneWeights is null)
                return;

            if (BoneIndices.IsReadOnly)
                BoneIndices = DataBuffer.CloneToWritable<int>(BoneIndices);
            if (BoneWeights.IsReadOnly)
                BoneWeights = DataBuffer.CloneToWritable<float>(BoneWeights);

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
                    else if (oldIndex > removedIndex && oldIndex < oldSkinnedBoneCount)
                    {
                        newIndex = oldIndex - 1;
                    }
                    else if (oldIndex < 0 || oldIndex >= oldSkinnedBoneCount)
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

        private Matrix4x4[] CopyAndFillInverseBindMatrices(IReadOnlyList<SkeletonBone> skinnedBones, IReadOnlyList<Matrix4x4> source)
        {
            var result = new Matrix4x4[skinnedBones.Count];
            int copyCount = Math.Min(source.Count, skinnedBones.Count);

            for (int i = 0; i < copyCount; i++)
                result[i] = source[i];

            if (copyCount < skinnedBones.Count)
            {
                for (int i = copyCount; i < skinnedBones.Count; i++)
                    result[i] = CreateInverseBindMatrix(skinnedBones[i], cache: null);
            }

            return result;
        }

        private Matrix4x4[] CreateInverseBindMatrices(IReadOnlyList<SkeletonBone> skinnedBones)
        {
            var result = new Matrix4x4[skinnedBones.Count];
            Dictionary<SceneNode, Matrix4x4> cache = [];

            for (int i = 0; i < skinnedBones.Count; i++)
                result[i] = CreateInverseBindMatrix(skinnedBones[i], cache);

            return result;
        }

        private Matrix4x4 CreateInverseBindMatrix(SkeletonBone bone, Dictionary<SceneNode, Matrix4x4>? cache)
        {
            Matrix4x4 boneBindWorld = cache is null
                ? bone.GetBindWorldMatrix()
                : ComputeCachedBindWorldMatrix(bone, cache);
            Matrix4x4 meshBindWorld = cache is null
                ? GetBindWorldMatrix()
                : ComputeCachedBindWorldMatrix(this, cache);

            return Matrix4x4.Invert(boneBindWorld, out Matrix4x4 inverseBoneBindWorld)
                ? meshBindWorld * inverseBoneBindWorld
                : Matrix4x4.Identity;
        }

        private static Matrix4x4 ComputeCachedBindWorldMatrix(SceneNode node, Dictionary<SceneNode, Matrix4x4> cache)
        {
            if (cache.TryGetValue(node, out var cached))
                return cached;

            Matrix4x4 world = node.Parent is not null
                ? node.GetBindLocalMatrix() * ComputeCachedBindWorldMatrix(node.Parent, cache)
                : node.GetBindLocalMatrix();

            cache[node] = world;
            return world;
        }

        public int[] GetBoneIndices(Dictionary<SkeletonBone, int> boneTable)
        {
            if (SkinnedBones is null)
                throw new NullReferenceException($"Mesh '{Name}' has no skinned bones.");

            var table = new int[SkinnedBones.Count];

            for (int i = 0; i < SkinnedBones.Count; i++)
            {
                if (!boneTable.TryGetValue(SkinnedBones[i], out int globalBoneIndex))
                    throw new KeyNotFoundException($"Mesh '{Name}' references a skinned bone that is not part of the bone table.");

                table[i] = globalBoneIndex;
            }

            return table;
        }
    }
}
