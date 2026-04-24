cbuffer LineConstants : register(b0)
{
    float4x4 Model;
    float4x4 SceneAxis;
    float4x4 View;
    float4x4 Projection;
    float2 ViewportSize;
    float LineHalfWidthPx;
};

struct VSInput
{
    float3 LineStart : POSITION0;
    float3 LineEnd : POSITION1;
    float4 Color : COLOR0;
    float Along : TEXCOORD0;
    float Side : TEXCOORD1;
    float WidthScale : TEXCOORD2;
};

struct VSOutput
{
    float4 Position : SV_Position;
    float3 WorldPosition : TEXCOORD3;
    float4 ColorValue : COLOR0;
};

VSOutput Main(VSInput input)
{
    VSOutput output;
    output.WorldPosition = lerp(input.LineStart, input.LineEnd, input.Along);
    output.ColorValue = input.Color;
    output.Position = float4(output.WorldPosition, 1.0f);
    return output;
}