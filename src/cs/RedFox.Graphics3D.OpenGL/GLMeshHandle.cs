using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL;

public sealed class GLMeshHandle
{
    public uint VAO { get; }
    public uint PositionVBO { get; }
    public uint NormalVBO { get; }
    public uint UVVBO { get; }
    public uint BoneIndexVBO { get; }
    public uint BoneWeightVBO { get; }
    public uint EBO { get; }
    public int VertexCount { get; }
    public int IndexCount { get; }
    public bool IsIndexed { get; }
    public bool HasNormals { get; }
    public bool HasUVs { get; }
    public bool HasSkinning { get; }
    public int SkinInfluenceCount { get; }

    public GLMeshHandle(
        uint vao, uint positionVbo, uint normalVbo, uint uvVbo,
        uint boneIndexVbo, uint boneWeightVbo, uint ebo,
        int vertexCount, int indexCount, bool isIndexed,
        bool hasNormals, bool hasUVs, bool hasSkinning, int skinInfluenceCount)
    {
        VAO = vao;
        PositionVBO = positionVbo;
        NormalVBO = normalVbo;
        UVVBO = uvVbo;
        BoneIndexVBO = boneIndexVbo;
        BoneWeightVBO = boneWeightVbo;
        EBO = ebo;
        VertexCount = vertexCount;
        IndexCount = indexCount;
        IsIndexed = isIndexed;
        HasNormals = hasNormals;
        HasUVs = hasUVs;
        HasSkinning = hasSkinning;
        SkinInfluenceCount = skinInfluenceCount;
    }

    public void Delete(GL gl)
    {
        if (VAO != 0) gl.DeleteVertexArray(VAO);
        if (PositionVBO != 0) gl.DeleteBuffer(PositionVBO);
        if (NormalVBO != 0) gl.DeleteBuffer(NormalVBO);
        if (UVVBO != 0) gl.DeleteBuffer(UVVBO);
        if (BoneIndexVBO != 0) gl.DeleteBuffer(BoneIndexVBO);
        if (BoneWeightVBO != 0) gl.DeleteBuffer(BoneWeightVBO);
        if (EBO != 0) gl.DeleteBuffer(EBO);
    }
}
