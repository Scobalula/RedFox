Texture2D SourceTexture : register(t0);
SamplerState SourceSampler : register(s0);

cbuffer SkyboxRotationConstants : register(b0)
{
    float2 TargetSize;
    float SourceLod;
    float RotationOffset;
};

struct PSInput
{
    float4 Position : SV_Position;
    float2 ClipPosition : TEXCOORD0;
};

float4 Main(PSInput input) : SV_Target
{
    float2 uv = input.Position.xy / TargetSize;
    return SourceTexture.SampleLevel(SourceSampler, float2(uv.x + RotationOffset, uv.y), SourceLod);
}
