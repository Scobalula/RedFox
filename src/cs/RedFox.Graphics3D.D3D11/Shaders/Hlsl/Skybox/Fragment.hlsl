TextureCube SkyboxTexture : register(t0);
SamplerState SkyboxSampler : register(s0);

cbuffer SkyboxConstants : register(b0)
{
    row_major float4x4 InverseView;
    row_major float4x4 InverseProjection;
    float4 SkyboxTint;
    float SkyboxIntensity;
};

struct PSInput
{
    float4 Position : SV_Position;
    float2 ClipPosition : TEXCOORD0;
};

float4 Main(PSInput input) : SV_Target
{
    float4 viewPosition = mul(float4(input.ClipPosition, 1.0f, 1.0f), InverseProjection);
    float3 viewDirection = normalize(viewPosition.xyz / max(abs(viewPosition.w), 0.00001f));
    float3 worldDirection = normalize(mul(float4(viewDirection, 0.0f), InverseView).xyz);
    float4 color = SkyboxTexture.Sample(SkyboxSampler, worldDirection);
    return float4(color.rgb * SkyboxTint.rgb * SkyboxIntensity, color.a * SkyboxTint.a);
}