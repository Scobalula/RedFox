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
    float4 color = input.ColorValue;
    if (FadeEndDistance > FadeStartDistance)
    {
        float dist = distance(CameraPosition, input.WorldPosition);
        float fade = saturate((dist - FadeStartDistance) / (FadeEndDistance - FadeStartDistance));
        color.a *= 1.0f - fade;
    }

    if (color.a <= 0.01f)
    {
        discard;
    }

    return color;
}