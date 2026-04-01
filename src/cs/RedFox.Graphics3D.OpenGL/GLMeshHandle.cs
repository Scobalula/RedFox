using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL;

public sealed class GLMeshHandle
{
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

    public GLMeshHandle(
        uint vao, uint positionVbo, uint normalVbo, uint uvVbo,
        uint influenceRangeVbo, uint influenceTexture, uint boneMatrixTexture, uint ebo,
        int vertexCount, int indexCount, bool isIndexed,
        bool hasNormals, bool hasUVs, bool hasSkinning,
        int boneCount,
        int influenceTextureWidth, int influenceTextureHeight,
        int boneMatrixTextureWidth, int boneMatrixTextureHeight,
        bool hasMorphTargets, int morphTargetCount,
        float[]? basePositions, float[]? baseNormals,
        float[]? positionMorphDeltas, float[]? normalMorphDeltas)
    {
        VAO = vao;
        PositionVBO = positionVbo;
        NormalVBO = normalVbo;
        UVVBO = uvVbo;
        InfluenceRangeVBO = influenceRangeVbo;
        InfluenceTexture = influenceTexture;
        BoneMatrixTexture = boneMatrixTexture;
        EBO = ebo;
        VertexCount = vertexCount;
        IndexCount = indexCount;
        IsIndexed = isIndexed;
        HasNormals = hasNormals;
        HasUVs = hasUVs;
        HasSkinning = hasSkinning;
        BoneCount = boneCount;
        InfluenceTextureWidth = influenceTextureWidth;
        InfluenceTextureHeight = influenceTextureHeight;
        BoneMatrixTextureWidth = boneMatrixTextureWidth;
        BoneMatrixTextureHeight = boneMatrixTextureHeight;
        HasMorphTargets = hasMorphTargets;
        MorphTargetCount = morphTargetCount;
        BasePositions = basePositions;
        BaseNormals = baseNormals;
        PositionMorphDeltas = positionMorphDeltas;
        NormalMorphDeltas = normalMorphDeltas;
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
