StructuredBuffer<float> Positions : register(t0);
StructuredBuffer<float> Normals : register(t1);
StructuredBuffer<uint> BoneIndices : register(t2);
StructuredBuffer<float> BoneWeights : register(t3);
StructuredBuffer<float4x4> SkinTransforms : register(t4);
RWStructuredBuffer<float> SkinnedPositions : register(u0);
RWStructuredBuffer<float> SkinnedNormals : register(u1);

cbuffer SkinningConstants : register(b0)
{
    int VertexCount;
    int SkinInfluenceCount;
    int SkinningMode;
};

[numthreads(64, 1, 1)]
void Main(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    if (dispatchThreadId.x >= (uint)VertexCount)
    {
        return;
    }

    // Phase 6 mirror scaffold only. The D3D11 backend is introduced in Phase 8.
}