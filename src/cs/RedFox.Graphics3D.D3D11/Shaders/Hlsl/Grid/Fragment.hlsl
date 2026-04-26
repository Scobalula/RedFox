cbuffer GridStyleConstants : register(b1)
{
    float GridCellSize;
    float GridMajorStep;
    float GridMinPixelsBetweenCells;
    float GridLineWidth;
    float4 GridMinorColor;
    float4 GridMajorColor;
    float4 GridAxisXColor;
    float4 GridAxisZColor;
    float FadeStartDistance;
    float FadeEndDistance;
    float2 GridPadding;
};

cbuffer GridFrameConstants : register(b0)
{
    row_major float4x4 View;
    row_major float4x4 Projection;
    float3 CameraPosition;
    float GridSize;
};

struct PSInput
{
    float4 Position : SV_Position;
    float2 GridUv : TEXCOORD0;
    float2 CameraGridPosition : TEXCOORD1;
    float3 WorldPosition : TEXCOORD2;
};

float Max2(float2 value)
{
    return max(value.x, value.y);
}

float CellAlpha(float2 uv, float cellSize, float2 lineWidth)
{
    float2 cell = frac(uv / cellSize) * cellSize;
    float2 distanceToLine = min(cell, cellSize - cell);
    float2 alpha = 1.0f - saturate(distanceToLine / max(lineWidth, float2(1e-6f, 1e-6f)));
    return Max2(alpha);
}

float AxisAlpha(float coordinate, float lineWidth)
{
    return 1.0f - saturate(abs(coordinate) / max(lineWidth, 1e-6f));
}

float4 Main(PSInput input) : SV_Target
{
    float2 derivatives = max(float2(
        length(float2(ddx(input.GridUv.x), ddy(input.GridUv.x))),
        length(float2(ddx(input.GridUv.y), ddy(input.GridUv.y)))),
        float2(1e-6f, 1e-6f));

    float cellSize = max(GridCellSize, 1e-6f);
    float majorStep = max(GridMajorStep, 2.0f);
    float minPixels = max(GridMinPixelsBetweenCells, 1.0f);
    float lodLevel = max(0.0f, log(max((length(derivatives) * minPixels) / cellSize, 1e-6f)) / log(majorStep) + 1.0f);
    float lodFade = frac(lodLevel);
    float lod0 = cellSize * pow(majorStep, floor(lodLevel));
    float lod1 = lod0 * majorStep;
    float lod2 = lod1 * majorStep;
    float2 lineWidth = derivatives * max(GridLineWidth, 0.25f);

    float lod0Alpha = CellAlpha(input.GridUv, lod0, lineWidth);
    float lod1Alpha = CellAlpha(input.GridUv, lod1, lineWidth);
    float lod2Alpha = CellAlpha(input.GridUv, lod2, lineWidth);
    float lineAlpha = lod2Alpha > 0.0f
        ? lod2Alpha
        : (lod1Alpha > 0.0f ? lod1Alpha : lod0Alpha * (1.0f - lodFade));

    float4 color = lod2Alpha > 0.0f
        ? GridMajorColor
        : (lod1Alpha > 0.0f ? lerp(GridMajorColor, GridMinorColor, lodFade) : GridMinorColor);

    float axisXAlpha = AxisAlpha(input.GridUv.y, lineWidth.y);
    float axisZAlpha = AxisAlpha(input.GridUv.x, lineWidth.x);
    float axisAlpha = max(axisXAlpha, axisZAlpha);
    if (axisAlpha >= lineAlpha && axisAlpha > 0.0f)
    {
        color = axisXAlpha >= axisZAlpha ? GridAxisXColor : GridAxisZColor;
    }

    float alpha = max(lineAlpha, axisAlpha);
    float extentOpacity = 1.0f - saturate(length(input.GridUv - input.CameraGridPosition) / max(GridSize, 1e-6f));
    float fadeOpacity = 1.0f;

    if (FadeEndDistance > FadeStartDistance)
    {
        float dist = distance(CameraPosition, input.WorldPosition);
        float fade = saturate((dist - FadeStartDistance) / (FadeEndDistance - FadeStartDistance));
        fadeOpacity = 1.0f - fade;
    }

    color.a *= alpha * extentOpacity * fadeOpacity;

    if (color.a <= 0.01f)
    {
        discard;
    }

    return color;
}