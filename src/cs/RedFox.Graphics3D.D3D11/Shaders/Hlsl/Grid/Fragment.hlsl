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
};

cbuffer GridFrameConstants : register(b0)
{
    row_major float4x4 View;
    row_major float4x4 Projection;
    row_major float4x4 InverseView;
    row_major float4x4 InverseProjection;
};

struct PSInput
{
    float4 Position : SV_Position;
    float3 NearPoint : TEXCOORD0;
    float3 FarPoint : TEXCOORD1;
};

struct PSOutput
{
    float4 Color : SV_Target;
    float Depth : SV_Depth;
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

float ComputeDepth(float3 worldPosition)
{
    float4 clipPosition = mul(mul(float4(worldPosition, 1.0f), View), Projection);
    return min(max(clipPosition.z / clipPosition.w, 0.0f), 0.999999f);
}

PSOutput Main(PSInput input)
{
    float3 ray = input.FarPoint - input.NearPoint;
    if (abs(ray.y) <= 1e-6f)
    {
        discard;
    }

    float intersection = -input.NearPoint.y / ray.y;
    if (intersection < 0.0f)
    {
        discard;
    }

    float3 worldPosition = input.NearPoint + ray * intersection;
    float2 gridUv = worldPosition.xz;

    float2 derivatives = max(float2(
        length(float2(ddx(gridUv.x), ddy(gridUv.x))),
        length(float2(ddx(gridUv.y), ddy(gridUv.y)))),
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
    float2 axisLineWidth = min(lineWidth, float2(max(cellSize * 0.08f, 1e-6f), max(cellSize * 0.08f, 1e-6f)));

    float lod0Alpha = CellAlpha(gridUv, lod0, lineWidth);
    float lod1Alpha = CellAlpha(gridUv, lod1, lineWidth);
    float lod2Alpha = CellAlpha(gridUv, lod2, lineWidth);
    float lineAlpha = lod2Alpha > 0.0f
        ? lod2Alpha
        : (lod1Alpha > 0.0f ? lod1Alpha : lod0Alpha * (1.0f - lodFade));

    float4 color = lod2Alpha > 0.0f
        ? GridMajorColor
        : (lod1Alpha > 0.0f ? lerp(GridMajorColor, GridMinorColor, lodFade) : GridMinorColor);

    float axisXAlpha = AxisAlpha(gridUv.y, axisLineWidth.y);
    float axisZAlpha = AxisAlpha(gridUv.x, axisLineWidth.x);
    float axisAlpha = max(axisXAlpha, axisZAlpha);
    if (axisAlpha >= lineAlpha && axisAlpha > 0.0f)
    {
        color = axisXAlpha >= axisZAlpha ? GridAxisXColor : GridAxisZColor;
    }

    float alpha = max(lineAlpha, axisAlpha);
    color.a *= alpha;

    if (color.a <= 0.01f)
    {
        discard;
    }

    PSOutput output;
    output.Color = color;
    output.Depth = ComputeDepth(worldPosition);
    return output;
}