cbuffer FadeConstants : register(b1)
{
    float3 CameraPosition;
    float FadeStartDistance;
    float FadeEndDistance;
};

struct PSInput
{
    float4 Position : SV_Position;
    float3 WorldPosition : TEXCOORD3;
    float4 ColorValue : COLOR0;
};

float4 Main(PSInput input) : SV_Target
{
    return input.ColorValue;
}