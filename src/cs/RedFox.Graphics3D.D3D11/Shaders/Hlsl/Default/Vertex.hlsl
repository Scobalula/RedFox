cbuffer FrameConstants : register(b0)
{
    row_major float4x4 Model;
    row_major float4x4 SceneAxis;
    row_major float4x4 View;
    row_major float4x4 Projection;
};

struct VSInput
{
    float3 Positions : POSITION;
    float3 Normals : NORMAL;
};

struct VSOutput
{
    float4 Position : SV_Position;
    float3 WorldPosition : TEXCOORD0;
    float3 WorldNormal : TEXCOORD1;
};

VSOutput Main(VSInput input)
{
    VSOutput output;
    row_major float4x4 worldMatrix = mul(Model, SceneAxis);
    float4 worldPosition = mul(float4(input.Positions, 1.0f), worldMatrix);
    output.WorldPosition = worldPosition.xyz;
    output.WorldNormal = normalize(mul(float4(input.Normals, 0.0f), worldMatrix).xyz);
    output.Position = mul(worldPosition, mul(View, Projection));
    return output;
}