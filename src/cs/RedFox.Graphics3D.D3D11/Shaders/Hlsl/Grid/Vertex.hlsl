cbuffer LineConstants : register(b0)
{
    row_major float4x4 Model;
    row_major float4x4 SceneAxis;
    row_major float4x4 View;
    row_major float4x4 Projection;
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
    row_major float4x4 worldMatrix = mul(Model, SceneAxis);
    float4 localPosition = float4(lerp(input.LineStart, input.LineEnd, input.Along), 1.0f);
    float4 worldPosition = mul(localPosition, worldMatrix);

    float4 clipStart = mul(mul(float4(input.LineStart, 1.0f), worldMatrix), mul(View, Projection));
    float4 clipEnd = mul(mul(float4(input.LineEnd, 1.0f), worldMatrix), mul(View, Projection));
    float4 clipPosition = lerp(clipStart, clipEnd, input.Along);

    float safeStartW = max(abs(clipStart.w), 1e-5f);
    float safeEndW = max(abs(clipEnd.w), 1e-5f);
    float2 ndcStart = clipStart.xy / safeStartW;
    float2 ndcEnd = clipEnd.xy / safeEndW;
    float2 viewport = max(ViewportSize, float2(1.0f, 1.0f));
    float2 screenStart = (ndcStart * 0.5f + 0.5f) * viewport;
    float2 screenEnd = (ndcEnd * 0.5f + 0.5f) * viewport;

    float2 screenDirection = screenEnd - screenStart;
    float len = max(length(screenDirection), 1e-5f);
    float2 tangent = screenDirection / len;
    float2 normal = float2(-tangent.y, tangent.x);
    float halfWidth = LineHalfWidthPx * input.WidthScale;
    float capSign = (input.Along * 2.0f) - 1.0f;
    float2 offsetScreen = (normal * input.Side * halfWidth) + (tangent * capSign * halfWidth);
    float2 offsetNdc = (offsetScreen / viewport) * 2.0f;
    clipPosition.xy += offsetNdc * clipPosition.w;

    output.WorldPosition = worldPosition.xyz;
    output.ColorValue = input.Color;
    output.Position = clipPosition;
    return output;
}