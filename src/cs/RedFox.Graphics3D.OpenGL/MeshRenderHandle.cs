using System.Numerics;
using RedFox.Graphics3D.Buffers;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL;

public sealed class MeshRenderHandle(Mesh mesh) : RenderHandle
{
    private readonly Mesh _mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
    private readonly List<uint> _vertexBufferObjects = [];

    private uint _srcPositionSsbo;
    private uint _srcNormalSsbo;
    private uint _morphPosDeltaSsbo;
    private uint _morphNrmDeltaSsbo;
    private uint _morphWeightSsbo;
    private uint _boneMatrixSsbo;
    private uint _influenceSsbo;
    private uint _influenceRangeSsbo;

    private bool _computeAvailable;
    private uint _vao;
    private uint _ebo;

    public Mesh Mesh => _mesh;
    public bool FrontFaceClockwise { get; private set; }
    public int VertexCount { get; private set; }
    public int IndexCount { get; private set; }
    public bool IsIndexed { get; private set; }
    public bool HasNormals { get; private set; }
    public bool HasUVs { get; private set; }
    public bool HasSkinning { get; private set; }
    public int BoneCount { get; private set; }
    public bool HasMorphTargets { get; private set; }
    public int MorphTargetCount { get; private set; }
    public bool UseComputeDeform { get; private set; }

    internal void SetComputeAvailable(bool available) => _computeAvailable = available;

    protected override void OnInitialize(GL gl)
    {
        Mesh mesh = _mesh;
        if (mesh.Positions is null || mesh.VertexCount == 0)
            return;

        if (mesh.Normals is null)
            MeshNormals.Generate(mesh);
        if (mesh.Tangents is null)
            MeshTangentFrame.Generate(mesh, true);

        bool hasSkinning = mesh.HasSkinning
                           && mesh.SkinnedBones is not null
                           && mesh.BoneIndices is not null
                           && mesh.BoneWeights is not null;
        bool hasMorphTargets = mesh.HasMorphTargets && mesh.MorphTargetCount > 0;
        bool useCompute = _computeAvailable && (hasSkinning || hasMorphTargets);

        _vao = gl.GenVertexArray();
        gl.BindVertexArray(_vao);

        if (useCompute)
        {
            float[] positionData = ExtractVertexFloatBuffer(mesh.Positions, 0, 3);
            float[] normalData = mesh.Normals is not null ? ExtractVertexFloatBuffer(mesh.Normals, 0, 3) : [];
            InitializeComputeBuffers(gl, mesh, positionData, normalData, hasSkinning, hasMorphTargets);
        }
        else
        {
            InitializeStaticBuffers(gl, mesh);
        }

        InitializeIndexBuffer(gl, mesh);

        VertexCount = mesh.VertexCount;
        HasNormals = mesh.Normals is not null;
        HasUVs = mesh.UVLayers is not null;
        HasSkinning = hasSkinning;
        HasMorphTargets = hasMorphTargets;
        MorphTargetCount = mesh.MorphTargetCount;
        UseComputeDeform = useCompute;

        gl.BindVertexArray(0);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
    }

    private void InitializeComputeBuffers(GL gl, Mesh mesh,
        float[] srcPositionData, float[] srcNormalData,
        bool hasSkinning, bool hasMorphTargets)
    {
        // Source (immutable) SSBOs
        _srcPositionSsbo = GlBufferOperations.CreateStorageBuffer(gl, srcPositionData, BufferUsageARB.StaticDraw);
        if (srcNormalData.Length > 0)
            _srcNormalSsbo = GlBufferOperations.CreateStorageBuffer(gl, srcNormalData, BufferUsageARB.StaticDraw);

        // Destination VBOs that double as compute write targets
        AddDynamicVbo(gl, 0, srcPositionData, 3);
        if (srcNormalData.Length > 0)
            AddDynamicVbo(gl, 1, srcNormalData, 3);
        if (mesh.UVLayers is not null)
            _vertexBufferObjects.Add(CreateVertexBufferObject(gl, 2, mesh.UVLayers, 0, 2, BufferUsageARB.StaticDraw));

        if (hasMorphTargets)
            InitializeMorphSsbos(gl, mesh);
        if (hasSkinning)
            InitializeSkinSsbos(gl, mesh);
    }

    private void InitializeStaticBuffers(GL gl, Mesh mesh)
    {
        _vertexBufferObjects.Add(CreateVertexBufferObject(gl, 0, mesh.Positions!, 0, 3, BufferUsageARB.StaticDraw));
        if (mesh.Normals is not null)
            _vertexBufferObjects.Add(CreateVertexBufferObject(gl, 1, mesh.Normals, 0, 3, BufferUsageARB.StaticDraw));
        if (mesh.UVLayers is not null)
            _vertexBufferObjects.Add(CreateVertexBufferObject(gl, 2, mesh.UVLayers, 0, 2, BufferUsageARB.StaticDraw));
    }

    private void InitializeMorphSsbos(GL gl, Mesh mesh)
    {
        int vertexCount = mesh.VertexCount;
        int targetCount = mesh.MorphTargetCount;
        int deltaSize = vertexCount * targetCount * 3;

        float[] morphPosDeltas = mesh.DeltaPositions is not null
            ? ExtractMorphFloatBuffer(mesh.DeltaPositions, 3) : new float[deltaSize];
        float[] morphNrmDeltas = mesh.DeltaNormals is not null
            ? ExtractMorphFloatBuffer(mesh.DeltaNormals, 3) : new float[deltaSize];

        _morphPosDeltaSsbo = GlBufferOperations.CreateStorageBuffer(gl, morphPosDeltas, BufferUsageARB.StaticDraw);
        _morphNrmDeltaSsbo = GlBufferOperations.CreateStorageBuffer(gl, morphNrmDeltas, BufferUsageARB.StaticDraw);
        _morphWeightSsbo = GlBufferOperations.CreateEmptyStorageBuffer(
            gl, Math.Max(targetCount, 1) * sizeof(float), BufferUsageARB.DynamicDraw);
    }

    private void InitializeSkinSsbos(GL gl, Mesh mesh)
    {
        int boneCount = mesh.SkinnedBones!.Count;
        (int[] ranges, float[] influenceData, _) =
            BuildSkinInfluenceData(mesh.BoneIndices!, mesh.BoneWeights!, boneCount);

        _influenceSsbo = GlBufferOperations.CreateStorageBuffer(gl, influenceData, BufferUsageARB.StaticDraw);

        unsafe
        {
            _influenceRangeSsbo = gl.GenBuffer();
            gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _influenceRangeSsbo);
            fixed (int* ptr = ranges)
            {
                gl.BufferData(BufferTargetARB.ShaderStorageBuffer,
                    (nuint)(ranges.Length * sizeof(int)), ptr, BufferUsageARB.StaticDraw);
            }
            gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, 0);
        }

        _boneMatrixSsbo = GlBufferOperations.CreateEmptyStorageBuffer(
            gl, Math.Max(boneCount, 1) * 16 * sizeof(float), BufferUsageARB.DynamicDraw);

        BoneCount = boneCount;
    }

    private void InitializeIndexBuffer(GL gl, Mesh mesh)
    {
        if (!mesh.IsIndexed || mesh.FaceIndices is null)
            return;

        uint[] indices = ExtractIndexBuffer(mesh.FaceIndices);
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

    private void AddDynamicVbo(GL gl, uint index, float[] data, int componentCount)
    {
        uint vbo = GlBufferOperations.UploadFloatAttributeBuffer(gl, data, BufferUsageARB.DynamicDraw);
        gl.EnableVertexAttribArray(index);
        gl.VertexAttribPointer(index, componentCount, VertexAttribPointerType.Float, false, 0, 0);
        _vertexBufferObjects.Add(vbo);
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

    internal void UploadComputeData(GL gl)
    {
        if (HasMorphTargets && MorphTargetCount > 0 && _morphWeightSsbo != 0)
        {
            float[] weights = BlendShapeEvaluator.ResolveMorphWeights(_mesh, MorphTargetCount);
            GlBufferOperations.UploadStorageBuffer(gl, _morphWeightSsbo, weights);
        }

        if (HasSkinning && BoneCount > 0 && _boneMatrixSsbo != 0)
        {
            Matrix4x4[] matrices = new Matrix4x4[BoneCount];
            int count = _mesh.CopySkinTransforms(matrices);
            if (count > 0)
            {
                Matrix4x4 meshWorld = _mesh.GetActiveWorldMatrix();
                Matrix4x4.Invert(meshWorld, out Matrix4x4 inverseMeshWorld);

                float[] boneData = new float[BoneCount * 16];
                for (int i = 0; i < count; i++)
                    WriteBoneMatrix(matrices[i] * inverseMeshWorld, boneData, i * 16);

                GlBufferOperations.UploadStorageBuffer(gl, _boneMatrixSsbo, boneData);
            }
        }
    }

    /// <summary>Binds all SSBOs to their respective binding points for the compute dispatch.</summary>
    internal void BindComputeBuffers(GL gl)
    {
        gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 0, _srcPositionSsbo);
        gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 1, _srcNormalSsbo);
        gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 2, _vertexBufferObjects.Count > 0 ? _vertexBufferObjects[0] : 0);
        gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 3, _vertexBufferObjects.Count > 1 ? _vertexBufferObjects[1] : 0);
        gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 4, _morphPosDeltaSsbo);
        gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 5, _morphNrmDeltaSsbo);
        gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 6, _morphWeightSsbo);
        gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 7, _boneMatrixSsbo);
        gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 8, _influenceSsbo);
        gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 9, _influenceRangeSsbo);
    }

    /// <summary>Unbinds all SSBO binding points after compute dispatch.</summary>
    internal void UnbindComputeBuffers(GL gl)
    {
        for (uint i = 0; i <= 9; i++)
            gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, i, 0);
    }

    protected override void OnDispose(GL gl)
    {
        _mesh.GraphicsHandle = null;

        try
        {
            if (_vao != 0) gl.DeleteVertexArray(_vao);
            if (_ebo != 0) gl.DeleteBuffer(_ebo);

            foreach (uint vbo in _vertexBufferObjects)
                if (vbo != 0) gl.DeleteBuffer(vbo);

            DeleteBuffer(gl, _srcPositionSsbo);
            DeleteBuffer(gl, _srcNormalSsbo);
            DeleteBuffer(gl, _morphPosDeltaSsbo);
            DeleteBuffer(gl, _morphNrmDeltaSsbo);
            DeleteBuffer(gl, _morphWeightSsbo);
            DeleteBuffer(gl, _boneMatrixSsbo);
            DeleteBuffer(gl, _influenceSsbo);
            DeleteBuffer(gl, _influenceRangeSsbo);
        }
        catch { }
    }

    private static void DeleteBuffer(GL gl, uint id)
    {
        if (id != 0) gl.DeleteBuffer(id);
    }

    public static uint CreateVertexBufferObject(GL gl, uint index, DataBuffer buffer, int valueIndex, int componentCount, BufferUsageARB usage)
    {
        float[] data = ExtractVertexFloatBuffer(buffer, valueIndex, componentCount);
        uint vbo = GlBufferOperations.UploadFloatAttributeBuffer(gl, data, usage);
        gl.EnableVertexAttribArray(index);
        gl.VertexAttribPointer(index, componentCount, VertexAttribPointerType.Float, false, 0, 0);
        return vbo;
    }

    private static void WriteBoneMatrix(Matrix4x4 m, float[] dst, int offset)
    {
        dst[offset]      = m.M11; dst[offset + 1]  = m.M12; dst[offset + 2]  = m.M13; dst[offset + 3]  = m.M14;
        dst[offset + 4]  = m.M21; dst[offset + 5]  = m.M22; dst[offset + 6]  = m.M23; dst[offset + 7]  = m.M24;
        dst[offset + 8]  = m.M31; dst[offset + 9]  = m.M32; dst[offset + 10] = m.M33; dst[offset + 11] = m.M34;
        dst[offset + 12] = m.M41; dst[offset + 13] = m.M42; dst[offset + 14] = m.M43; dst[offset + 15] = m.M44;
    }

    private static (int[] Ranges, float[] InfluenceData, int EntryCount) BuildSkinInfluenceData(
        DataBuffer boneIndices, DataBuffer boneWeights, int boneCount)
    {
        int vertexCount = boneIndices.ElementCount;
        int influenceCount = Math.Min(boneIndices.ValueCount, boneWeights.ValueCount);
        int[] ranges = new int[vertexCount * 2];
        List<float> data = [];

        for (int vi = 0; vi < vertexCount; vi++)
        {
            int startIndex = data.Count / 2;
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
                data.Add(boneIndex);
                data.Add(weight * invTotal);
            }
        }

        return (ranges, [.. data], data.Count / 2);
    }

    internal static float[] ExtractVertexFloatBuffer(DataBuffer buffer, int valueIndex, int componentCount)
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

    internal static float[] ExtractMorphFloatBuffer(DataBuffer buffer, int componentCount)
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
