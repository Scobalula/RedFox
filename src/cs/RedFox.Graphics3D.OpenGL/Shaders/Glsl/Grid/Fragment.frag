#version 330 core

in vec3 NearPoint;
in vec3 FarPoint;

uniform mat4 View;
uniform mat4 Projection;
uniform float GridCellSize;
uniform float GridMajorStep;
uniform float GridMinPixelsBetweenCells;
uniform float GridLineWidth;
uniform vec4 GridMinorColor;
uniform vec4 GridMajorColor;
uniform vec4 GridAxisXColor;
uniform vec4 GridAxisZColor;

out vec4 FragColor;

float Saturate(float value)
{
    return clamp(value, 0.0, 1.0);
}

vec2 Saturate(vec2 value)
{
    return clamp(value, vec2(0.0), vec2(1.0));
}

float Max2(vec2 value)
{
    return max(value.x, value.y);
}

float CellAlpha(vec2 uv, float cellSize, vec2 lineWidth)
{
    vec2 cell = mod(uv, vec2(cellSize));
    vec2 distanceToLine = min(cell, vec2(cellSize) - cell);
    vec2 alpha = vec2(1.0) - Saturate(distanceToLine / max(lineWidth, vec2(1e-6)));
    return Max2(alpha);
}

float AxisAlpha(float coordinate, float lineWidth)
{
    return 1.0 - Saturate(abs(coordinate) / max(lineWidth, 1e-6));
}

float ComputeDepth(vec3 worldPosition)
{
    vec4 clipPosition = Projection * View * vec4(worldPosition, 1.0);
    float normalizedDepth = clipPosition.z / clipPosition.w;
    return clamp((normalizedDepth * 0.5) + 0.5, 0.0, 0.999999);
}

void main()
{
    vec3 ray = FarPoint - NearPoint;
    if (abs(ray.y) <= 1e-6)
    {
        discard;
    }

    float intersection = -NearPoint.y / ray.y;
    if (intersection < 0.0)
    {
        discard;
    }

    vec3 worldPosition = NearPoint + ray * intersection;
    vec2 gridUv = worldPosition.xz;

    vec2 derivatives = max(vec2(
        length(vec2(dFdx(gridUv.x), dFdy(gridUv.x))),
        length(vec2(dFdx(gridUv.y), dFdy(gridUv.y)))),
        vec2(1e-6));

    float cellSize = max(GridCellSize, 1e-6);
    float majorStep = max(GridMajorStep, 2.0);
    float minPixels = max(GridMinPixelsBetweenCells, 1.0);
    float lodLevel = max(0.0, log(max((length(derivatives) * minPixels) / cellSize, 1e-6)) / log(majorStep) + 1.0);
    float lodFade = fract(lodLevel);
    float lod0 = cellSize * pow(majorStep, floor(lodLevel));
    float lod1 = lod0 * majorStep;
    float lod2 = lod1 * majorStep;
    vec2 lineWidth = derivatives * max(GridLineWidth, 0.25);
    vec2 axisLineWidth = min(lineWidth, vec2(max(cellSize * 0.08, 1e-6)));

    float lod0Alpha = CellAlpha(gridUv, lod0, lineWidth);
    float lod1Alpha = CellAlpha(gridUv, lod1, lineWidth);
    float lod2Alpha = CellAlpha(gridUv, lod2, lineWidth);
    float lineAlpha = lod2Alpha > 0.0
        ? lod2Alpha
        : (lod1Alpha > 0.0 ? lod1Alpha : lod0Alpha * (1.0 - lodFade));

    vec4 color = lod2Alpha > 0.0
        ? GridMajorColor
        : (lod1Alpha > 0.0 ? mix(GridMajorColor, GridMinorColor, lodFade) : GridMinorColor);

    float axisXAlpha = AxisAlpha(gridUv.y, axisLineWidth.y);
    float axisZAlpha = AxisAlpha(gridUv.x, axisLineWidth.x);
    float axisAlpha = max(axisXAlpha, axisZAlpha);
    if (axisAlpha >= lineAlpha && axisAlpha > 0.0)
    {
        color = axisXAlpha >= axisZAlpha ? GridAxisXColor : GridAxisZColor;
    }

    float alpha = max(lineAlpha, axisAlpha);
    color.a *= alpha;

    if (color.a <= 0.01)
    {
        discard;
    }

    FragColor = color;
    gl_FragDepth = ComputeDepth(worldPosition);
}