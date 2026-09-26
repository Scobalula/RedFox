using RedFox.Graphics3D.Buffers;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace RedFox.Graphics3D;

/// <summary>
/// Represents a renderable mesh node with optional vertex attributes, skin and morph deformers.
/// </summary>
public class Mesh : SceneNode
{
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
    /// Gets or sets the vertex indices that define the faces in the mesh as a triangle list.
    /// </summary>
    public DataBuffer? FaceIndices { get; set; }

    /// <summary>
    /// Gets or sets the collection of materials associated with this mesh.
    /// </summary>
    public List<Material>? Materials { get; set; }

    /// <summary>
    /// Gets or sets the skin that deforms this mesh.
    /// </summary>
    public Skin? Skin { get; set; }

    /// <summary>
    /// Gets or sets the morph that deforms this mesh.
    /// </summary>
    public Morph? Morph { get; set; }

    /// <summary>
    /// Gets the number of vertices described by the mesh buffers.
    /// </summary>
    public int VertexCount => Positions?.ElementCount ?? Normals?.ElementCount ?? Tangents?.ElementCount ?? BiTangents?.ElementCount ?? ColorLayers?.ElementCount ?? UVLayers?.ElementCount ?? Skin?.BoneIndices.ElementCount ?? Morph?.VertexCount ?? 0;

    /// <summary>
    /// Gets the number of vertex references stored in <see cref="FaceIndices"/>.
    /// </summary>
    public int IndexCount => FaceIndices?.ElementCount ?? 0;

    /// <summary>
    /// Gets the number of triangle faces described by the mesh topology.
    /// </summary>
    public int FaceCount => IndexCount / 3;

    /// <summary>
    /// Gets the number of UV layers stored on the mesh.
    /// </summary>
    public int UVLayerCount => UVLayers?.ValueCount ?? 0;

    /// <summary>
    /// Gets the number of color layers stored on the mesh.
    /// </summary>
    public int ColorLayerCount => ColorLayers?.ValueCount ?? 0;

    /// <summary>
    /// Gets a value indicating whether the mesh contains indexed topology.
    /// </summary>
    public bool IsIndexed => FaceIndices is not null;

    /// <summary>
    /// Gets a value indicating whether the mesh contains skinning data.
    /// </summary>
    public bool HasSkinning => Skin is not null;

    /// <summary>
    /// Gets a value indicating whether the mesh contains morph target data.
    /// </summary>
    public bool HasMorphTargets => Morph is not null;

    /// <summary>
    /// Copies the active skin transforms aligned to <see cref="Skin.Bones"/> into the destination span.
    /// </summary>
    /// <param name="destination">The destination span to receive one transform per skin bone.</param>
    /// <returns>The number of transforms written.</returns>
    public int CopySkinTransforms(Span<Matrix4x4> destination)
    {
        if (Skin is null)
            return 0;

        if (destination.Length < Skin.Bones.Count)
            throw new ArgumentException($"Destination span must be at least {Skin.Bones.Count} elements.", nameof(destination));

        Matrix4x4 meshBindWorld = GetBindWorldMatrix();

        for (int i = 0; i < Skin.Bones.Count; i++)
            destination[i] = Skin.GetSkinTransform(i, meshBindWorld);

        return Skin.Bones.Count;
    }

    /// <summary>
    /// Bakes the current morph weights and skinned deformation into the vertex buffers and clears the morph and skin.
    /// The current visual pose becomes the new raw mesh data.
    /// </summary>
    public void BakeCurrentPoseToVertices()
    {
        if (Skin is null && Morph is null)
            return;

        Matrix4x4[] skinTransforms = new Matrix4x4[Skin?.Bones.Count ?? 0];
        CopySkinTransforms(skinTransforms);

        Positions = BakeBuffer(Positions, vertexIndex => GetVertexPosition(vertexIndex, skinTransforms));
        Normals = BakeBuffer(Normals, vertexIndex => GetVertexNormal(vertexIndex, skinTransforms));
        Tangents = BakeBuffer(Tangents, vertexIndex => GetVertexTangent(vertexIndex, skinTransforms).AsVector3());
        BiTangents = BakeBuffer(BiTangents, vertexIndex => GetVertexBiTangent(vertexIndex, skinTransforms));

        Skin = null;
        Morph = null;
    }

    /// <summary>
    /// Gets the vertex position for the specified vertex index, applying the current morph weights and skin pose when available.
    /// </summary>
    /// <param name="vertexIndex">The zero-based vertex index.</param>
    /// <returns>The resolved vertex position.</returns>
    public Vector3 GetVertexPosition(int vertexIndex) => GetVertexPosition(vertexIndex, raw: false);

    /// <summary>
    /// Gets the vertex position for the specified vertex index.
    /// </summary>
    /// <param name="vertexIndex">The zero-based vertex index.</param>
    /// <param name="raw"><see langword="true"/> to return the stored buffer value without morphing or skinning; otherwise the current morph weights and skin pose are applied.</param>
    /// <returns>The resolved vertex position.</returns>
    public Vector3 GetVertexPosition(int vertexIndex, bool raw) => raw ? GetRequiredVector3(Positions, vertexIndex, nameof(Positions)) : GetVertexPosition(vertexIndex, []);

    /// <summary>
    /// Gets the vertex position for the specified vertex index using the current morph weights and precomputed skin transforms.
    /// </summary>
    /// <param name="vertexIndex">The zero-based vertex index.</param>
    /// <param name="skinTransforms">The transforms aligned to <see cref="Skin.Bones"/>, or an empty span to resolve the active skin pose.</param>
    /// <returns>The resolved vertex position.</returns>
    public Vector3 GetVertexPosition(int vertexIndex, ReadOnlySpan<Matrix4x4> skinTransforms)
    {
        Vector3 position = GetRequiredVector3(Positions, vertexIndex, nameof(Positions)) + (Morph?.GetPositionDelta(vertexIndex) ?? Vector3.Zero);
        return ApplySkinning(position, vertexIndex, Vector3.Transform, skinTransforms) ?? position;
    }

    /// <summary>
    /// Gets the vertex normal for the specified vertex index, applying the current morph weights and skin pose when available.
    /// </summary>
    /// <param name="vertexIndex">The zero-based vertex index.</param>
    /// <returns>The resolved vertex normal.</returns>
    public Vector3 GetVertexNormal(int vertexIndex) => GetVertexNormal(vertexIndex, raw: false);

    /// <summary>
    /// Gets the vertex normal for the specified vertex index.
    /// </summary>
    /// <param name="vertexIndex">The zero-based vertex index.</param>
    /// <param name="raw"><see langword="true"/> to return the stored buffer value without morphing or skinning; otherwise the current morph weights and skin pose are applied.</param>
    /// <returns>The resolved vertex normal.</returns>
    public Vector3 GetVertexNormal(int vertexIndex, bool raw) => raw ? GetRequiredVector3(Normals, vertexIndex, nameof(Normals)) : GetVertexNormal(vertexIndex, []);

    /// <summary>
    /// Gets the vertex normal for the specified vertex index using the current morph weights and precomputed skin transforms.
    /// </summary>
    /// <param name="vertexIndex">The zero-based vertex index.</param>
    /// <param name="skinTransforms">The transforms aligned to <see cref="Skin.Bones"/>, or an empty span to resolve the active skin pose.</param>
    /// <returns>The resolved vertex normal.</returns>
    public Vector3 GetVertexNormal(int vertexIndex, ReadOnlySpan<Matrix4x4> skinTransforms)
    {
        Vector3 normal = GetRequiredVector3(Normals, vertexIndex, nameof(Normals));
        Vector3 morphedNormal = normal + (Morph?.GetNormalDelta(vertexIndex) ?? Vector3.Zero);
        Vector3? skinnedNormal = ApplySkinning(morphedNormal, vertexIndex, static (value, transform) => Vector3.TransformNormal(value, Matrix4x4.Invert(transform, out Matrix4x4 inverse) ? Matrix4x4.Transpose(inverse) : transform), skinTransforms);
        return NormalizeOrDefault(skinnedNormal ?? morphedNormal, normal);
    }

    /// <summary>
    /// Gets the vertex tangent for the specified vertex index, applying the current morph weights and skin pose when available.
    /// </summary>
    /// <param name="vertexIndex">The zero-based vertex index.</param>
    /// <returns>The resolved vertex tangent. When the source tangent has four components, the W component is preserved.</returns>
    public Vector4 GetVertexTangent(int vertexIndex) => GetVertexTangent(vertexIndex, raw: false);

    /// <summary>
    /// Gets the vertex tangent for the specified vertex index.
    /// </summary>
    /// <param name="vertexIndex">The zero-based vertex index.</param>
    /// <param name="raw"><see langword="true"/> to return the stored buffer value without morphing or skinning; otherwise the current morph weights and skin pose are applied.</param>
    /// <returns>The resolved vertex tangent. When the source tangent has four components, the W component is preserved.</returns>
    public Vector4 GetVertexTangent(int vertexIndex, bool raw) => raw ? new Vector4(GetRequiredVector3(Tangents, vertexIndex, nameof(Tangents)), GetTangentHandedness(vertexIndex)) : GetVertexTangent(vertexIndex, []);

    /// <summary>
    /// Gets the vertex tangent for the specified vertex index using the current morph weights and precomputed skin transforms.
    /// </summary>
    /// <param name="vertexIndex">The zero-based vertex index.</param>
    /// <param name="skinTransforms">The transforms aligned to <see cref="Skin.Bones"/>, or an empty span to resolve the active skin pose.</param>
    /// <returns>The resolved vertex tangent. When the source tangent has four components, the W component is preserved.</returns>
    public Vector4 GetVertexTangent(int vertexIndex, ReadOnlySpan<Matrix4x4> skinTransforms)
    {
        Vector3 tangent = GetRequiredVector3(Tangents, vertexIndex, nameof(Tangents));
        Vector3 morphedTangent = tangent + (Morph?.GetTangentDelta(vertexIndex) ?? Vector3.Zero);
        Vector3 skinnedTangent = NormalizeOrDefault(ApplySkinning(morphedTangent, vertexIndex, Vector3.TransformNormal, skinTransforms) ?? morphedTangent, tangent);
        return new Vector4(skinnedTangent, GetTangentHandedness(vertexIndex));
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
    /// <param name="raw"><see langword="true"/> to return the stored buffer value without morphing or skinning; otherwise the current morph weights and skin pose are applied.</param>
    /// <returns>The resolved vertex bitangent.</returns>
    public Vector3 GetVertexBiTangent(int vertexIndex, bool raw) => raw ? GetRequiredVector3(BiTangents, vertexIndex, nameof(BiTangents)) : GetVertexBiTangent(vertexIndex, []);

    /// <summary>
    /// Gets the vertex bitangent for the specified vertex index using precomputed skin transforms.
    /// </summary>
    /// <param name="vertexIndex">The zero-based vertex index.</param>
    /// <param name="skinTransforms">The transforms aligned to <see cref="Skin.Bones"/>, or an empty span to resolve the active skin pose.</param>
    /// <returns>The resolved vertex bitangent.</returns>
    public Vector3 GetVertexBiTangent(int vertexIndex, ReadOnlySpan<Matrix4x4> skinTransforms)
    {
        Vector3 bitangent = GetRequiredVector3(BiTangents, vertexIndex, nameof(BiTangents));
        return NormalizeOrDefault(ApplySkinning(bitangent, vertexIndex, Vector3.TransformNormal, skinTransforms), bitangent);
    }

    /// <summary>
    /// Generates vertex normals for this mesh.
    /// </summary>
    public void GenerateNormals() => GenerateNormals(NormalGenerationMode.EqualWeight, preserveExisting: false);

    /// <summary>
    /// Generates vertex normals for this mesh using the specified normal generation mode.
    /// </summary>
    /// <param name="mode">The algorithm or strategy to use when generating normals for the mesh.</param>
    public void GenerateNormals(NormalGenerationMode mode) => GenerateNormals(mode, preserveExisting: false);

    /// <summary>
    /// Generates vertex normals for this mesh.
    /// </summary>
    /// <param name="preserveExisting">If <see langword="true"/>, existing normals are preserved and normals are only generated if none exist.</param>
    public void GenerateNormals(bool preserveExisting) => GenerateNormals(NormalGenerationMode.EqualWeight, preserveExisting);

    /// <summary>
    /// Generates vertex normals for this mesh using the specified normal generation mode.
    /// </summary>
    /// <param name="mode">The algorithm or strategy to use when generating normals for the mesh.</param>
    /// <param name="preserveExisting">If <see langword="true"/>, existing normals are preserved and normals are only generated if none exist.</param>
    public void GenerateNormals(NormalGenerationMode mode, bool preserveExisting)
    {
        if (Normals is not null && preserveExisting)
            return;

        MeshNormals.Generate(this, mode);
    }

    /// <summary>
    /// Generates vertex tangents for this mesh if none exist.
    /// </summary>
    public void GenerateTangents() => GenerateTangents(preserveExisting: true);

    /// <summary>
    /// Generates vertex tangents for this mesh.
    /// </summary>
    /// <param name="preserveExisting">If <see langword="true"/>, existing tangents are preserved and tangents are only generated if none exist.</param>
    public void GenerateTangents(bool preserveExisting)
    {
        if (Tangents is not null && preserveExisting)
            return;

        MeshTangentFrame.Generate(this);
    }

    /// <summary>
    /// Reverses the winding order of every triangle by swapping its second and third indices.
    /// Vertex data, including normals, is left unchanged.
    /// </summary>
    /// <exception cref="InvalidOperationException">The mesh has no <see cref="FaceIndices"/>.</exception>
    public void ReverseWinding()
    {
        if (FaceIndices is null)
            throw new InvalidOperationException($"Mesh '{Name}' has no {nameof(FaceIndices)} buffer.");

        DataBuffer faceIndices = FaceIndices.IsReadOnly ? DataBuffer.CloneToWritable<int>(FaceIndices) : FaceIndices;

        for (int index = 0; index + 2 < faceIndices.ElementCount; index += 3)
        {
            int second = faceIndices.Get<int>(index + 1, 0, 0);
            faceIndices.Set(index + 1, 0, 0, faceIndices.Get<int>(index + 2, 0, 0));
            faceIndices.Set(index + 2, 0, 0, second);
        }

        FaceIndices = faceIndices;
    }

    /// <inheritdoc/>
    public override bool TryGetSceneBounds(out SceneBounds bounds)
    {
        bounds = SceneBounds.Invalid;

        if (Positions is null)
            return false;

        float morphOffset = Morph?.GetMaxPositionOffset() ?? 0f;

        if (Skin is not null)
        {
            SceneBounds[] bindSpaceBounds = Skin.GetBindSpaceBounds(Positions, GetBindWorldMatrix());

            for (int i = 0; i < bindSpaceBounds.Length; i++)
                bounds = bounds.Include(bindSpaceBounds[i].Expand(morphOffset).Transform(Skin.Bones[i].GetActiveWorldMatrix()));

            if (bounds.IsValid)
                return true;
        }

        Matrix4x4 world = GetActiveWorldMatrix();

        if (morphOffset > 0f)
        {
            for (int i = 0; i < Positions.ElementCount; i++)
                bounds = bounds.Include(Positions.GetVector3(i, 0));

            bounds = bounds.Expand(morphOffset).Transform(world);
            return bounds.IsValid;
        }

        for (int i = 0; i < Positions.ElementCount; i++)
            bounds = bounds.Include(Vector3.Transform(Positions.GetVector3(i, 0), world));

        return bounds.IsValid;
    }

    /// <inheritdoc/>
    public override void Swap(SceneNode oldNode, SceneNode newNode)
    {
        base.Swap(oldNode, newNode);

        if (oldNode is SkeletonBone oldBone && newNode is SkeletonBone newBone)
            Skin?.RemapBones(new Dictionary<SkeletonBone, SkeletonBone> { [oldBone] = newBone });

        if (oldNode is Material oldMaterial && newNode is Material newMaterial && Materials is not null)
        {
            for (int i = 0; i < Materials.Count; i++)
            {
                if (ReferenceEquals(Materials[i], oldMaterial))
                    Materials[i] = newMaterial;
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnCloned()
    {
        Skin = Skin?.Clone();
        Morph = Morph?.Clone();
        Materials = Materials is null ? null : [.. Materials];
    }

    private Vector3? ApplySkinning(Vector3 value, int vertexIndex, Func<Vector3, Matrix4x4, Vector3> transform, ReadOnlySpan<Matrix4x4> skinTransforms)
    {
        if (Skin is null)
            return null;

        Matrix4x4 meshBindWorld = skinTransforms.IsEmpty ? GetBindWorldMatrix() : Matrix4x4.Identity;
        Vector3 result = Vector3.Zero;
        float totalWeight = 0f;

        for (int influenceIndex = 0; influenceIndex < Skin.InfluenceCount; influenceIndex++)
        {
            float weight = Skin.BoneWeights.Get<float>(vertexIndex, influenceIndex, 0);

            if (weight <= 0f)
                continue;

            int boneIndex = Skin.GetBoneIndex(vertexIndex, influenceIndex);
            Matrix4x4 skinTransform = skinTransforms.IsEmpty ? Skin.GetSkinTransform(boneIndex, meshBindWorld) : skinTransforms[boneIndex];

            result += transform(value, skinTransform) * weight;
            totalWeight += weight;
        }

        return totalWeight > 0f ? result : null;
    }

    private float GetTangentHandedness(int vertexIndex) => Tangents!.ComponentCount > 3 ? Tangents.Get<float>(vertexIndex, 0, 3) : 0f;

    private Vector3 GetRequiredVector3(DataBuffer? buffer, int vertexIndex, string bufferName)
    {
        if (buffer is null)
            throw new InvalidOperationException($"Mesh '{Name}' has no {bufferName} buffer.");

        if ((uint)vertexIndex >= (uint)buffer.ElementCount)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Vertex index must be between 0 and {buffer.ElementCount - 1}.");

        return buffer.GetVector3(vertexIndex, 0);
    }

    private static DataBuffer? BakeBuffer(DataBuffer? buffer, Func<int, Vector3> resolve)
    {
        if (buffer is null)
            return null;

        DataBuffer writable = buffer.IsReadOnly ? DataBuffer.CloneToWritable<float>(buffer) : buffer;

        for (int vertexIndex = 0; vertexIndex < writable.ElementCount; vertexIndex++)
        {
            Vector3 value = resolve(vertexIndex);
            writable.Set(vertexIndex, 0, 0, value.X);
            writable.Set(vertexIndex, 0, 1, value.Y);
            writable.Set(vertexIndex, 0, 2, value.Z);
        }

        return writable;
    }

    private static Vector3 NormalizeOrDefault(Vector3? value, Vector3 fallback) => value is { } vector && vector.LengthSquared() > 1e-12f ? Vector3.Normalize(vector) : fallback;
}
