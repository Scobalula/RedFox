cbuffer FrameConstants : register(b0)
{
    float4x4 Model;
    float4x4 SceneAxis;
    float4x4 View;
    float4x4 Projection;
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
    float4 worldPosition = mul(float4(input.Positions, 1.0f), mul(Model, SceneAxis));
    output.WorldPosition = worldPosition.xyz;
    output.WorldNormal = normalize(mul(float4(input.Normals, 0.0f), Model).xyz);
    output.Position = mul(worldPosition, mul(View, Projection));
    return output;
}