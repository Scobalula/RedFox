using RedFox.Graphics3D.Buffers;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace RedFox.Graphics3D;

/// <summary>
/// Represents a skin deformer that binds mesh vertices to skeleton bones.
/// </summary>
/// <param name="bones">The bones referenced by <paramref name="boneIndices"/>.</param>
/// <param name="boneIndices">The per-vertex bone indices.</param>
/// <param name="boneWeights">The per-vertex bone weights.</param>
public class Skin(IReadOnlyList<SkeletonBone> bones, DataBuffer boneIndices, DataBuffer boneWeights)
{
    private SkeletonBone[] _bones = [.. bones];

    private Matrix4x4[]? _inverseBindMatrices;

    private DataBuffer? _boundsPositions;

    private SceneBounds[]? _bindSpaceBounds;

    /// <summary>
    /// Gets or sets the name of the skin.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the method used to blend bone transforms.
    /// </summary>
    public SkinningMode SkinningMode { get; set; }

    /// <summary>
    /// Gets the bones referenced by <see cref="BoneIndices"/>.
    /// </summary>
    public IReadOnlyList<SkeletonBone> Bones => _bones;

    /// <summary>
    /// Gets or sets the inverse bind matrices aligned with <see cref="Bones"/>, or <see langword="null"/> to derive them from the bind pose.
    /// </summary>
    /// <exception cref="ArgumentException">The matrix count does not match the bone count.</exception>
    public IReadOnlyList<Matrix4x4>? InverseBindMatrices
    {
        get => _inverseBindMatrices;
        set
        {
            if (value is not null && value.Count != _bones.Length)
                throw new ArgumentException($"Expected {_bones.Length} inverse bind matrices but received {value.Count}.", nameof(value));

            _inverseBindMatrices = value is null ? null : [.. value];
            _bindSpaceBounds = null;
        }
    }

    /// <summary>
    /// Gets or sets the per-vertex bone indices.
    /// </summary>
    public DataBuffer BoneIndices
    {
        get;
        set
        {
            field = value;
            _bindSpaceBounds = null;
        }
    } = boneIndices;

    /// <summary>
    /// Gets or sets the per-vertex bone weights.
    /// </summary>
    public DataBuffer BoneWeights
    {
        get;
        set
        {
            field = value;
            _bindSpaceBounds = null;
        }
    } = boneWeights;

    /// <summary>
    /// Gets the number of bone influences stored per vertex.
    /// </summary>
    public int InfluenceCount => Math.Min(BoneIndices.ValueCount, BoneWeights.ValueCount);

    /// <summary>
    /// Initializes a new instance of the <see cref="Skin"/> class with explicit inverse bind matrices.
    /// </summary>
    /// <param name="bones">The bones referenced by <paramref name="boneIndices"/>.</param>
    /// <param name="boneIndices">The per-vertex bone indices.</param>
    /// <param name="boneWeights">The per-vertex bone weights.</param>
    /// <param name="inverseBindMatrices">The inverse bind matrices aligned with <paramref name="bones"/>.</param>
    public Skin(IReadOnlyList<SkeletonBone> bones, DataBuffer boneIndices, DataBuffer boneWeights, IReadOnlyList<Matrix4x4> inverseBindMatrices) : this(bones, boneIndices, boneWeights)
    {
        InverseBindMatrices = inverseBindMatrices;
    }

    /// <summary>
    /// Gets the inverse bind matrix for the specified bone.
    /// </summary>
    /// <param name="boneIndex">The bone index.</param>
    /// <param name="meshBindWorld">The bind world matrix of the skinned mesh.</param>
    /// <returns>The inverse bind matrix.</returns>
    public Matrix4x4 GetInverseBindMatrix(int boneIndex, Matrix4x4 meshBindWorld)
    {
        return _inverseBindMatrices?[boneIndex] ?? DeriveInverseBindMatrix(_bones[boneIndex], meshBindWorld);
    }

    /// <summary>
    /// Gets the skin transform for the specified bone in its active pose.
    /// </summary>
    /// <param name="boneIndex">The bone index.</param>
    /// <param name="meshBindWorld">The bind world matrix of the skinned mesh.</param>
    /// <returns>The skin transform.</returns>
    public Matrix4x4 GetSkinTransform(int boneIndex, Matrix4x4 meshBindWorld) => GetInverseBindMatrix(boneIndex, meshBindWorld) * _bones[boneIndex].GetActiveWorldMatrix();

    /// <summary>
    /// Gets the bone index for the specified vertex influence.
    /// </summary>
    /// <param name="vertexIndex">The vertex index.</param>
    /// <param name="influenceIndex">The influence index.</param>
    /// <returns>The bone index.</returns>
    /// <exception cref="InvalidDataException">The stored index is outside <see cref="Bones"/>.</exception>
    public int GetBoneIndex(int vertexIndex, int influenceIndex)
    {
        int boneIndex = BoneIndices.Get<int>(vertexIndex, influenceIndex, 0);

        if ((uint)boneIndex >= (uint)_bones.Length)
            throw new InvalidDataException($"Skin '{Name}' contains an invalid bone index {boneIndex} at vertex {vertexIndex}.");

        return boneIndex;
    }

    /// <summary>
    /// Sets <see cref="InverseBindMatrices"/> from the current bind pose.
    /// </summary>
    /// <param name="meshBindWorld">The bind world matrix of the skinned mesh.</param>
    public void CaptureInverseBindMatrices(Matrix4x4 meshBindWorld)
    {
        _inverseBindMatrices = Array.ConvertAll(_bones, bone => DeriveInverseBindMatrix(bone, meshBindWorld));
        _bindSpaceBounds = null;
    }

    /// <summary>
    /// Adds a bone to a skin that derives its inverse bind matrices.
    /// </summary>
    /// <param name="bone">The bone to add.</param>
    /// <returns>The bone index.</returns>
    /// <exception cref="InvalidOperationException">The skin has explicit inverse bind matrices.</exception>
    public int AddBone(SkeletonBone bone) => AddBone(bone, inverseBindMatrix: null);

    /// <summary>
    /// Adds a bone to the skin.
    /// </summary>
    /// <param name="bone">The bone to add.</param>
    /// <param name="inverseBindMatrix">The inverse bind matrix, or <see langword="null"/> when the skin derives them.</param>
    /// <returns>The bone index.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="inverseBindMatrix"/> does not match the inverse bind matrix mode.</exception>
    public int AddBone(SkeletonBone bone, Matrix4x4? inverseBindMatrix)
    {
        ArgumentNullException.ThrowIfNull(bone);

        int existingIndex = Array.IndexOf(_bones, bone);

        if (existingIndex >= 0)
            return existingIndex;

        if (inverseBindMatrix is null && _inverseBindMatrices is not null)
            throw new InvalidOperationException($"Skin '{Name}' has explicit inverse bind matrices; supply one for the new bone.");

        if (inverseBindMatrix is not null && _inverseBindMatrices is null)
            throw new InvalidOperationException($"Skin '{Name}' derives inverse bind matrices from the bind pose; call {nameof(CaptureInverseBindMatrices)} first.");

        _bones = [.. _bones, bone];
        _inverseBindMatrices = inverseBindMatrix is { } matrix ? [.. _inverseBindMatrices!, matrix] : null;
        _bindSpaceBounds = null;
        return _bones.Length - 1;
    }

    /// <summary>
    /// Removes a bone and renormalizes the affected weights.
    /// </summary>
    /// <param name="bone">The bone to remove.</param>
    /// <returns><see langword="true"/> when the bone was removed; otherwise <see langword="false"/>.</returns>
    public bool RemoveBone(SkeletonBone bone)
    {
        int boneIndex = Array.IndexOf(_bones, bone);

        if (boneIndex < 0)
            return false;

        RemoveBoneAt(boneIndex);
        return true;
    }

    /// <summary>
    /// Removes the bone at the specified index and renormalizes the affected weights.
    /// </summary>
    /// <param name="boneIndex">The bone index.</param>
    public void RemoveBoneAt(int boneIndex)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)boneIndex, (uint)_bones.Length, nameof(boneIndex));

        DataBuffer boneIndices = DataBuffer.CloneToWritable<int>(BoneIndices);
        DataBuffer boneWeights = DataBuffer.CloneToWritable<float>(BoneWeights);
        int influenceCount = InfluenceCount;

        for (int vertexIndex = 0; vertexIndex < boneIndices.ElementCount; vertexIndex++)
        {
            float totalWeight = 0f;

            for (int influenceIndex = 0; influenceIndex < influenceCount; influenceIndex++)
            {
                int oldIndex = boneIndices.Get<int>(vertexIndex, influenceIndex, 0);

                if (oldIndex == boneIndex || (uint)oldIndex >= (uint)_bones.Length)
                {
                    boneIndices.Set(vertexIndex, influenceIndex, 0, 0);
                    boneWeights.Set(vertexIndex, influenceIndex, 0, 0f);
                    continue;
                }

                if (oldIndex > boneIndex)
                    boneIndices.Set(vertexIndex, influenceIndex, 0, oldIndex - 1);

                totalWeight += MathF.Max(0f, boneWeights.Get<float>(vertexIndex, influenceIndex, 0));
            }

            if (totalWeight <= 0f)
                continue;

            for (int influenceIndex = 0; influenceIndex < influenceCount; influenceIndex++)
                boneWeights.Set(vertexIndex, influenceIndex, 0, MathF.Max(0f, boneWeights.Get<float>(vertexIndex, influenceIndex, 0)) / totalWeight);
        }

        _bones = [.. _bones[..boneIndex], .. _bones[(boneIndex + 1)..]];
        _inverseBindMatrices = _inverseBindMatrices is null ? null : [.. _inverseBindMatrices[..boneIndex], .. _inverseBindMatrices[(boneIndex + 1)..]];
        BoneIndices = boneIndices;
        BoneWeights = boneWeights;
    }

    /// <summary>
    /// Replaces bone references.
    /// </summary>
    /// <param name="remap">The mapping from old bones to new bones.</param>
    /// <returns>The number of bones that were replaced.</returns>
    public int RemapBones(IReadOnlyDictionary<SkeletonBone, SkeletonBone> remap)
    {
        ArgumentNullException.ThrowIfNull(remap);

        int remappedCount = 0;

        for (int i = 0; i < _bones.Length; i++)
        {
            if (!remap.TryGetValue(_bones[i], out SkeletonBone? replacement) || ReferenceEquals(replacement, _bones[i]))
                continue;

            _bones[i] = replacement;
            remappedCount++;
        }

        if (remappedCount > 0)
            _bindSpaceBounds = null;

        return remappedCount;
    }

    /// <summary>
    /// Maps each bone in <see cref="Bones"/> to its index in the specified bone table.
    /// </summary>
    /// <param name="boneTable">The bone table.</param>
    /// <returns>An array whose entries correspond to <see cref="Bones"/>.</returns>
    /// <exception cref="KeyNotFoundException">A bone is absent from <paramref name="boneTable"/>.</exception>
    public int[] GetBoneTableIndices(IReadOnlyDictionary<SkeletonBone, int> boneTable) => Array.ConvertAll(_bones, bone => boneTable.TryGetValue(bone, out int index) ? index : throw new KeyNotFoundException($"Skin '{Name}' references bone '{bone.Name}' that is not part of the bone table."));

    /// <summary>
    /// Creates a copy of this skin that shares its buffers.
    /// </summary>
    /// <returns>The copied skin.</returns>
    public Skin Clone() => new(_bones, BoneIndices, BoneWeights) { Name = Name, SkinningMode = SkinningMode, InverseBindMatrices = _inverseBindMatrices };

    internal SceneBounds[] GetBindSpaceBounds(DataBuffer positions, Matrix4x4 meshBindWorld)
    {
        if (_bindSpaceBounds is not null && ReferenceEquals(_boundsPositions, positions))
            return _bindSpaceBounds;

        Matrix4x4[] inverseBindMatrices = _inverseBindMatrices ?? Array.ConvertAll(_bones, bone => DeriveInverseBindMatrix(bone, meshBindWorld));
        SceneBounds[] bounds = new SceneBounds[_bones.Length];
        int influenceCount = InfluenceCount;

        for (int vertexIndex = 0; vertexIndex < positions.ElementCount; vertexIndex++)
        {
            Vector3 position = positions.GetVector3(vertexIndex, 0);

            for (int influenceIndex = 0; influenceIndex < influenceCount; influenceIndex++)
            {
                if (BoneWeights.Get<float>(vertexIndex, influenceIndex, 0) <= 0f)
                    continue;

                int boneIndex = GetBoneIndex(vertexIndex, influenceIndex);
                bounds[boneIndex] = bounds[boneIndex].Include(Vector3.Transform(position, inverseBindMatrices[boneIndex]));
            }
        }

        _boundsPositions = positions;
        _bindSpaceBounds = bounds;
        return bounds;
    }

    private static Matrix4x4 DeriveInverseBindMatrix(SkeletonBone bone, Matrix4x4 meshBindWorld) => Matrix4x4.Invert(bone.GetBindWorldMatrix(), out Matrix4x4 inverseBoneBindWorld) ? meshBindWorld * inverseBoneBindWorld : Matrix4x4.Identity;
}
