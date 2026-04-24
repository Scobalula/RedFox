cbuffer LightingConstants : register(b1)
{
    float3 AmbientColor;
    int LightCount;
    float4 LightDirectionsAndIntensity[4];
    float3 LightColors[4];
    float3 CameraPosition;
    int UseViewBasedLighting;
    float4 BaseColor;
    float MaterialSpecularStrength;
    float MaterialSpecularPower;
};

struct PSInput
{
    float4 Position : SV_Position;
    float3 WorldPosition : TEXCOORD0;
    float3 WorldNormal : TEXCOORD1;
};

float4 Main(PSInput input) : SV_Target
{
    return BaseColor;
}