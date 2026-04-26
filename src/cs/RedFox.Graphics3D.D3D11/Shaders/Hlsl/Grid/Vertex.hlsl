cbuffer GridFrameConstants : register(b0)
{
    row_major float4x4 View;
    row_major float4x4 Projection;
    float3 CameraPosition;
    float GridSize;
};

struct VSOutput
{
    float4 Position : SV_Position;
    float2 GridUv : TEXCOORD0;
    float2 CameraGridPosition : TEXCOORD1;
    float3 WorldPosition : TEXCOORD2;
};

static const float3 GridPositions[4] =
{
    float3(-1.0f, 0.0f, -1.0f),
    float3(1.0f, 0.0f, -1.0f),
    float3(1.0f, 0.0f, 1.0f),
    float3(-1.0f, 0.0f, 1.0f)
};

static const uint GridIndices[6] = { 0, 1, 2, 2, 3, 0 };

VSOutput Main(uint vertexId : SV_VertexID)
{
    VSOutput output;
    uint index = GridIndices[vertexId];
    float3 position = GridPositions[index] * GridSize;
    position.x += CameraPosition.x;
    position.z += CameraPosition.z;

    output.WorldPosition = position;
    output.GridUv = position.xz;
    output.CameraGridPosition = CameraPosition.xz;
    output.Position = mul(mul(float4(position, 1.0f), View), Projection);
    return output;
}