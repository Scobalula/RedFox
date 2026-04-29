cbuffer GridFrameConstants : register(b0)
{
    row_major float4x4 View;
    row_major float4x4 Projection;
    row_major float4x4 InverseView;
    row_major float4x4 InverseProjection;
};

struct VSOutput
{
    float4 Position : SV_Position;
    float3 NearPoint : TEXCOORD0;
    float3 FarPoint : TEXCOORD1;
};

static const float2 GridPositions[4] =
{
    float2(-1.0f, -1.0f),
    float2(1.0f, -1.0f),
    float2(1.0f, 1.0f),
    float2(-1.0f, 1.0f)
};

static const uint GridIndices[6] = { 0, 1, 2, 2, 3, 0 };

float3 UnprojectPoint(float2 position, float depth)
{
    float4 unprojected = mul(mul(float4(position, depth, 1.0f), InverseProjection), InverseView);
    return unprojected.xyz / unprojected.w;
}

VSOutput Main(uint vertexId : SV_VertexID)
{
    VSOutput output;
    uint index = GridIndices[vertexId];
    float2 position = GridPositions[index];

    output.NearPoint = UnprojectPoint(position, 0.0f);
    output.FarPoint = UnprojectPoint(position, 1.0f);
    output.Position = float4(position, 0.0f, 1.0f);
    return output;
}