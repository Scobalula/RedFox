#version 330 core

in vec2 GridUv;
in vec2 CameraGridPosition;
in vec3 WorldPosition;

uniform vec3 CameraPosition;
uniform float GridSize;
uniform float GridCellSize;
uniform float GridMajorStep;
uniform float GridMinPixelsBetweenCells;
uniform float GridLineWidth;
uniform vec4 GridMinorColor;
uniform vec4 GridMajorColor;
uniform vec4 GridAxisXColor;
uniform vec4 GridAxisZColor;
uniform float FadeStartDistance;
uniform float FadeEndDistance;

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

void main()
{
    vec2 derivatives = max(vec2(
        length(vec2(dFdx(GridUv.x), dFdy(GridUv.x))),
        length(vec2(dFdx(GridUv.y), dFdy(GridUv.y)))),
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

    float lod0Alpha = CellAlpha(GridUv, lod0, lineWidth);
    float lod1Alpha = CellAlpha(GridUv, lod1, lineWidth);
    float lod2Alpha = CellAlpha(GridUv, lod2, lineWidth);
    float lineAlpha = lod2Alpha > 0.0
        ? lod2Alpha
        : (lod1Alpha > 0.0 ? lod1Alpha : lod0Alpha * (1.0 - lodFade));

    vec4 color = lod2Alpha > 0.0
        ? GridMajorColor
        : (lod1Alpha > 0.0 ? mix(GridMajorColor, GridMinorColor, lodFade) : GridMinorColor);

    float axisXAlpha = AxisAlpha(GridUv.y, lineWidth.y);
    float axisZAlpha = AxisAlpha(GridUv.x, lineWidth.x);
    float axisAlpha = max(axisXAlpha, axisZAlpha);
    if (axisAlpha >= lineAlpha && axisAlpha > 0.0)
    {
        color = axisXAlpha >= axisZAlpha ? GridAxisXColor : GridAxisZColor;
    }

    float alpha = max(lineAlpha, axisAlpha);
    float extentOpacity = 1.0 - Saturate(length(GridUv - CameraGridPosition) / max(GridSize, 1e-6));
    float fadeOpacity = 1.0;

    if (FadeEndDistance > FadeStartDistance)
    {
        float dist = distance(CameraPosition, WorldPosition);
        float fade = Saturate((dist - FadeStartDistance) / (FadeEndDistance - FadeStartDistance));
        fadeOpacity = 1.0 - fade;
    }

    color.a *= alpha * extentOpacity * fadeOpacity;

    if (color.a <= 0.01)
    {
        discard;
    }

    FragColor = color;
}