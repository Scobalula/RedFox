using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL;

public sealed class GLMeshHandle
{
    public sealed class Descriptor
    {
        public bool FrontFaceClockwise { get; init; }
        public bool HasConsistentWinding { get; init; }
        public uint VAO { get; init; }
        public uint PositionVBO { get; init; }
        public uint NormalVBO { get; init; }
        public uint UVVBO { get; init; }
        public uint InfluenceRangeVBO { get; init; }
        public uint InfluenceTexture { get; init; }
        public uint BoneMatrixTexture { get; init; }
        public uint EBO { get; init; }
        public int VertexCount { get; init; }
        public int IndexCount { get; init; }
        public bool IsIndexed { get; init; }
        public bool HasNormals { get; init; }
        public bool HasUVs { get; init; }
        public bool HasSkinning { get; init; }
        public int BoneCount { get; init; }
        public int InfluenceTextureWidth { get; init; }
        public int InfluenceTextureHeight { get; init; }
        public int BoneMatrixTextureWidth { get; init; }
        public int BoneMatrixTextureHeight { get; init; }
        public bool HasMorphTargets { get; init; }
        public int MorphTargetCount { get; init; }
        public float[]? BasePositions { get; init; }
        public float[]? BaseNormals { get; init; }
        public float[]? PositionMorphDeltas { get; init; }
        public float[]? NormalMorphDeltas { get; init; }
    }

    public bool FrontFaceClockwise { get; }
    public bool HasConsistentWinding { get; }
    public uint VAO { get; }
    public uint PositionVBO { get; }
    public uint NormalVBO { get; }
    public uint UVVBO { get; }
    public uint InfluenceRangeVBO { get; }
    public uint InfluenceTexture { get; }
    public uint BoneMatrixTexture { get; }
    public uint EBO { get; }
    public int VertexCount { get; }
    public int IndexCount { get; }
    public bool IsIndexed { get; }
    public bool HasNormals { get; }
    public bool HasUVs { get; }
    public bool HasSkinning { get; }
    public int BoneCount { get; }
    public int InfluenceTextureWidth { get; }
    public int InfluenceTextureHeight { get; }
    public int BoneMatrixTextureWidth { get; }
    public int BoneMatrixTextureHeight { get; }
    public bool HasMorphTargets { get; }
    public int MorphTargetCount { get; }
    public float[]? BasePositions { get; }
    public float[]? BaseNormals { get; }
    public float[]? PositionMorphDeltas { get; }
    public float[]? NormalMorphDeltas { get; }

    public GLMeshHandle(Descriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        FrontFaceClockwise = descriptor.FrontFaceClockwise;
        HasConsistentWinding = descriptor.HasConsistentWinding;
        VAO = descriptor.VAO;
        PositionVBO = descriptor.PositionVBO;
        NormalVBO = descriptor.NormalVBO;
        UVVBO = descriptor.UVVBO;
        InfluenceRangeVBO = descriptor.InfluenceRangeVBO;
        InfluenceTexture = descriptor.InfluenceTexture;
        BoneMatrixTexture = descriptor.BoneMatrixTexture;
        EBO = descriptor.EBO;
        VertexCount = descriptor.VertexCount;
        IndexCount = descriptor.IndexCount;
        IsIndexed = descriptor.IsIndexed;
        HasNormals = descriptor.HasNormals;
        HasUVs = descriptor.HasUVs;
        HasSkinning = descriptor.HasSkinning;
        BoneCount = descriptor.BoneCount;
        InfluenceTextureWidth = descriptor.InfluenceTextureWidth;
        InfluenceTextureHeight = descriptor.InfluenceTextureHeight;
        BoneMatrixTextureWidth = descriptor.BoneMatrixTextureWidth;
        BoneMatrixTextureHeight = descriptor.BoneMatrixTextureHeight;
        HasMorphTargets = descriptor.HasMorphTargets;
        MorphTargetCount = descriptor.MorphTargetCount;
        BasePositions = descriptor.BasePositions;
        BaseNormals = descriptor.BaseNormals;
        PositionMorphDeltas = descriptor.PositionMorphDeltas;
        NormalMorphDeltas = descriptor.NormalMorphDeltas;
    }

    public void Delete(GL gl)
    {
        try
        {
            if (VAO != 0) gl.DeleteVertexArray(VAO);
            if (PositionVBO != 0) gl.DeleteBuffer(PositionVBO);
            if (NormalVBO != 0) gl.DeleteBuffer(NormalVBO);
            if (UVVBO != 0) gl.DeleteBuffer(UVVBO);
            if (InfluenceRangeVBO != 0) gl.DeleteBuffer(InfluenceRangeVBO);
            if (InfluenceTexture != 0) gl.DeleteTexture(InfluenceTexture);
            if (BoneMatrixTexture != 0) gl.DeleteTexture(BoneMatrixTexture);
            if (EBO != 0) gl.DeleteBuffer(EBO);
        }
        catch
        {
        }
    }
}
