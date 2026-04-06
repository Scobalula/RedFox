using System.Numerics;
using RedFox.Graphics3D.Buffers;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL;

public sealed class MeshRenderHandle(Mesh mesh, int maxTextureSize) : RenderHandle
{
    private readonly Mesh _mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
    private readonly int _maxTextureSize = maxTextureSize;
    private readonly List<uint> _vertexBufferObjects = [];

    public IReadOnlyList<uint> VertexBufferObjects => _vertexBufferObjects;

    private uint _vao;
    private uint _influenceRangeVbo;
    private uint _influenceTexture;
    private uint _boneMatrixTexture;
    private uint _ebo;

    private float[]? _positionMorphDeltas;
    private float[]? _normalMorphDeltas;

    public Mesh Mesh => _mesh;
    public bool FrontFaceClockwise { get; private set; } = false;
    public int VertexCount { get; private set; }
    public int IndexCount { get; private set; }
    public bool IsIndexed { get; private set; }
    public bool HasNormals { get; private set; }
    public bool HasUVs { get; private set; }
    public bool HasSkinning { get; private set; }
    public int BoneCount { get; private set; }
    public int InfluenceTextureWidth { get; private set; } = 1;
    public int InfluenceTextureHeight { get; private set; } = 1;
    public int BoneMatrixTextureWidth { get; private set; } = 1;
    public int BoneMatrixTextureHeight { get; private set; } = 1;
    public bool HasMorphTargets { get; private set; }
    public int MorphTargetCount { get; private set; }

    public static uint CreateVertexBufferObject(GL gl, uint index, DataBuffer buffer, int valueIndex, int componentCount, BufferUsageARB usage)
    {
        float[] data = ExtractVertexFloatBuffer(buffer, valueIndex, componentCount);
        var vbo = GlBufferOperations.UploadFloatAttributeBuffer(gl, data, usage);
        gl.EnableVertexAttribArray(index);
        gl.VertexAttribPointer(index, componentCount, VertexAttribPointerType.Float, false, 0, 0);
        return vbo;
    }

    protected override void OnInitialize(GL gl)
    {
        Mesh mesh = _mesh;

        if (mesh.Positions is null || mesh.VertexCount == 0)
            return;

        _vao = gl.GenVertexArray();
        gl.BindVertexArray(_vao);

        // We require these
        if (mesh.Normals is null)
            MeshNormals.Generate(mesh);
        if (mesh.Tangents is null)
            MeshTangentFrame.Generate(mesh, true);

        _vertexBufferObjects.Add(CreateVertexBufferObject(gl, 0, mesh.Positions, 0, 3, BufferUsageARB.DynamicDraw));


        if (mesh.Normals is not null)
            _vertexBufferObjects.Add(CreateVertexBufferObject(gl, 1, mesh.Normals, 0, 3, BufferUsageARB.DynamicDraw));
        if (mesh.UVLayers is not null)
            _vertexBufferObjects.Add(CreateVertexBufferObject(gl, 2, mesh.UVLayers, 0, 2, BufferUsageARB.DynamicDraw));

        bool hasSkinning = mesh.HasSkinning && mesh.SkinnedBones is not null
                           && mesh.BoneIndices is not null && mesh.BoneWeights is not null;

        if (hasSkinning)
        {
            int boneCount = mesh.SkinnedBones!.Count;
            (int[] influenceRanges, float[] influenceTextureData, int influenceEntryCount) =
                BuildSkinInfluenceTextureData(mesh.BoneIndices!, mesh.BoneWeights!, boneCount);

            unsafe
            {
                _influenceRangeVbo = gl.GenBuffer();
                gl.BindBuffer(BufferTargetARB.ArrayBuffer, _influenceRangeVbo);
                fixed (int* ptr = influenceRanges)
                {
                    gl.BufferData(BufferTargetARB.ArrayBuffer,
                        (nuint)(influenceRanges.Length * sizeof(int)), ptr, BufferUsageARB.StaticDraw);
                }
            }

            gl.EnableVertexAttribArray(3);
            gl.VertexAttribIPointer(3, 2, VertexAttribIType.Int, 0, 0);

            (InfluenceTextureWidth, InfluenceTextureHeight) =
                GlBufferOperations.ComputeTextureDimensions(_maxTextureSize, Math.Max(influenceEntryCount, 1));
            _influenceTexture = GlBufferOperations.CreateFloatTexture(
                gl, InfluenceTextureWidth, InfluenceTextureHeight,
                GlBufferOperations.PadTextureData(influenceTextureData, InfluenceTextureWidth * InfluenceTextureHeight * 4));

            (BoneMatrixTextureWidth, BoneMatrixTextureHeight) =
                GlBufferOperations.ComputeTextureDimensions(_maxTextureSize, Math.Max(boneCount * 4, 1));
            _boneMatrixTexture = GlBufferOperations.CreateFloatTexture(
                gl, BoneMatrixTextureWidth, BoneMatrixTextureHeight,
                new float[BoneMatrixTextureWidth * BoneMatrixTextureHeight * 4]);

            BoneCount = boneCount;
            HasSkinning = true;
        }

        uint[]? indices = null;
        if (mesh.IsIndexed && mesh.FaceIndices is not null)
        {
            indices = ExtractIndexBuffer(mesh.FaceIndices);
            IndexCount = indices.Length;
            IsIndexed = true;

            unsafe
            {
                _ebo = gl.GenBuffer();
                gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
                fixed (uint* ptr = indices)
                {
                    gl.BufferData(BufferTargetARB.ElementArrayBuffer,
                        (nuint)(indices.Length * sizeof(uint)), ptr, BufferUsageARB.StaticDraw);
                }
            }
        }

        //FrontFaceClockwise = DetermineFrontFaceClockwise(_basePositions, _baseNormals, indices, mesh.VertexCount);

        VertexCount = mesh.VertexCount;
        HasNormals = mesh.Normals is not null;
        HasUVs = mesh.UVLayers is not null;
        HasMorphTargets = mesh.HasMorphTargets;
        MorphTargetCount = mesh.MorphTargetCount;
        _positionMorphDeltas = mesh.DeltaPositions is null ? null : ExtractMorphFloatBuffer(mesh.DeltaPositions, 3);
        _normalMorphDeltas = mesh.DeltaNormals is null ? null : ExtractMorphFloatBuffer(mesh.DeltaNormals, 3);

        gl.BindVertexArray(0);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
    }

    protected override void OnUpdate(GL gl, float deltaTime)
    {
        UpdateMorphTargets(gl);
        UpdateSkinning(gl);
    }

    public void Draw(GL gl)
    {
        gl.BindVertexArray(_vao);

        if (IsIndexed)
        {
            unsafe
            {
                gl.DrawElements(PrimitiveType.Triangles, (uint)IndexCount, DrawElementsType.UnsignedInt, (void*)0);
            }
        }
        else
        {
            gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)VertexCount);
        }

        gl.BindVertexArray(0);
    }

    public void BindSkinningTextures(GL gl)
    {
        gl.ActiveTexture(TextureUnit.Texture1);
        gl.BindTexture(TextureTarget.Texture2D, _influenceTexture);

        gl.ActiveTexture(TextureUnit.Texture2);
        gl.BindTexture(TextureTarget.Texture2D, _boneMatrixTexture);
    }

    public void UnbindSkinningTextures(GL gl)
    {
        gl.ActiveTexture(TextureUnit.Texture2);
        gl.BindTexture(TextureTarget.Texture2D, 0);
        gl.ActiveTexture(TextureUnit.Texture1);
        gl.BindTexture(TextureTarget.Texture2D, 0);
        gl.ActiveTexture(TextureUnit.Texture0);
    }

    private void UpdateMorphTargets(GL gl)
    {
        // TODO: This should be done in a shader imo
    }

    private void UpdateSkinning(GL gl)
    {
        if (!HasSkinning || _boneMatrixTexture == 0 || BoneCount <= 0)
            return;

        Matrix4x4[] matrices = new Matrix4x4[BoneCount];
        int count = _mesh.CopySkinTransforms(matrices);
        if (count == 0)
            return;

        float[] textureData = new float[BoneMatrixTextureWidth * BoneMatrixTextureHeight * 4];
        for (int i = 0; i < count; i++)
            GlBufferOperations.WriteMatrixTexels(matrices[i], textureData, i * 4);

        GlBufferOperations.UploadFloatTexture(gl, _boneMatrixTexture,
            BoneMatrixTextureWidth, BoneMatrixTextureHeight, textureData);
    }

    protected override void OnDispose(GL gl)
    {
        _mesh.GraphicsHandle = null;

        try
        {
            if (_vao != 0)
                gl.DeleteVertexArray(_vao);

            foreach (var vertexBufferObject in _vertexBufferObjects)
                if (vertexBufferObject != 0)
                    gl.DeleteBuffer(vertexBufferObject);

            if (_influenceRangeVbo != 0)
                gl.DeleteBuffer(_influenceRangeVbo);
            if (_influenceTexture != 0)
                gl.DeleteTexture(_influenceTexture);
            if (_boneMatrixTexture != 0)
                gl.DeleteTexture(_boneMatrixTexture);
            if (_ebo != 0)
                gl.DeleteBuffer(_ebo);
        }
        catch { }
    }

    private static bool DetermineFrontFaceClockwise(float[] positions, float[]? normals, uint[]? indices, int vertexCount)
    {
        const float epsilon = 1e-10f;
        if (normals is null || positions.Length < 9 || normals.Length < 9)
            return false;

        int triangleCount = indices is not null ? indices.Length / 3 : vertexCount / 3;
        if (triangleCount <= 0)
            return false;

        int positiveCount = 0;
        int negativeCount = 0;
        int sampleCount = Math.Min(triangleCount, 512);
        int triangleStep = Math.Max(triangleCount / sampleCount, 1);

        for (int t = 0; t < triangleCount; t += triangleStep)
        {
            int b = t * 3;
            int i0 = indices is not null ? (int)indices[b] : b;
            int i1 = indices is not null ? (int)indices[b + 1] : b + 1;
            int i2 = indices is not null ? (int)indices[b + 2] : b + 2;

            Vector3 p0 = ReadVec3(positions, i0);
            Vector3 p1 = ReadVec3(positions, i1);
            Vector3 p2 = ReadVec3(positions, i2);

            Vector3 faceNormal = Vector3.Cross(p1 - p0, p2 - p0);
            if (faceNormal.LengthSquared() <= epsilon)
                continue;

            Vector3 avgNormal = ReadVec3(normals, i0) + ReadVec3(normals, i1) + ReadVec3(normals, i2);
            if (avgNormal.LengthSquared() <= epsilon)
                continue;

            float orientation = Vector3.Dot(faceNormal, avgNormal);
            if (orientation > epsilon) positiveCount++;
            else if (orientation < -epsilon) negativeCount++;
        }

        return negativeCount > positiveCount;
    }

    private static Vector3 ReadVec3(float[] v, int i) => new(v[i * 3], v[i * 3 + 1], v[i * 3 + 2]);

    private static (int[] Ranges, float[] TexData, int EntryCount) BuildSkinInfluenceTextureData(
        DataBuffer boneIndices, DataBuffer boneWeights, int boneCount)
    {
        int vertexCount = boneIndices.ElementCount;
        int influenceCount = Math.Min(boneIndices.ValueCount, boneWeights.ValueCount);
        int[] ranges = new int[vertexCount * 2];
        List<float> texData = [];

        for (int vi = 0; vi < vertexCount; vi++)
        {
            int startIndex = texData.Count / 4;
            float totalWeight = 0.0f;
            List<(int BoneIndex, float Weight)> influences = [];

            for (int ii = 0; ii < influenceCount; ii++)
            {
                float weight = boneWeights.Get<float>(vi, ii, 0);
                if (weight <= 1e-6f) continue;

                int boneIndex = boneIndices.Get<int>(vi, ii, 0);
                if ((uint)boneIndex >= (uint)boneCount) continue;

                influences.Add((boneIndex, weight));
                totalWeight += weight;
            }

            ranges[vi * 2] = startIndex;
            ranges[vi * 2 + 1] = influences.Count;

            if (totalWeight <= 1e-6f) continue;

            float invTotal = 1.0f / totalWeight;
            foreach ((int boneIndex, float weight) in influences)
            {
                texData.Add(boneIndex);
                texData.Add(weight * invTotal);
                texData.Add(0.0f);
                texData.Add(0.0f);
            }
        }

        return (ranges, [.. texData], texData.Count / 4);
    }

    private static float[] ExtractVertexFloatBuffer(DataBuffer buffer, int valueIndex, int componentCount)
    {
        float[] result = new float[buffer.ElementCount * componentCount];
        for (int ei = 0; ei < buffer.ElementCount; ei++)
        {
            for (int ci = 0; ci < componentCount; ci++)
            {
                result[ei * componentCount + ci] = ci < buffer.ComponentCount
                    ? buffer.Get<float>(ei, Math.Min(valueIndex, buffer.ValueCount - 1), ci)
                    : ci == 3 ? 1.0f : 0.0f;
            }
        }

        return result;
    }

    private static float[] ExtractMorphFloatBuffer(DataBuffer buffer, int componentCount)
    {
        float[] result = new float[buffer.ElementCount * buffer.ValueCount * componentCount];
        for (int ei = 0; ei < buffer.ElementCount; ei++)
        {
            for (int vi = 0; vi < buffer.ValueCount; vi++)
            {
                int dst = ((ei * buffer.ValueCount) + vi) * componentCount;
                for (int ci = 0; ci < componentCount; ci++)
                {
                    result[dst + ci] = ci < buffer.ComponentCount
                        ? buffer.Get<float>(ei, vi, ci)
                        : 0.0f;
                }
            }
        }

        return result;
    }

    private static uint[] ExtractIndexBuffer(DataBuffer buffer)
    {
        uint[] result = new uint[buffer.ElementCount];
        for (int ei = 0; ei < buffer.ElementCount; ei++)
            result[ei] = uint.CreateChecked(buffer.Get<int>(ei, 0, 0));
        return result;
    }
}
