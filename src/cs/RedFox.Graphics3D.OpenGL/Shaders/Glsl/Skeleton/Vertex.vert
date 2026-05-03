#version 300 es
precision highp float;
precision highp int;

layout (location = 0) in vec3 LineStart;
layout (location = 1) in vec3 LineEnd;
layout (location = 2) in vec4 Color;
layout (location = 3) in float Along;
layout (location = 4) in float Side;
layout (location = 5) in float WidthScale;

uniform mat4 Model;
uniform mat4 SceneAxis;
uniform mat4 View;
uniform mat4 Projection;
uniform vec2 ViewportSize;
uniform float LineHalfWidthPx;

out vec3 WorldPosition;
out vec4 ColorValue;

void main()
{
    vec4 localPosition = vec4(mix(LineStart, LineEnd, Along), 1.0);
    vec4 worldPosition = SceneAxis * Model * localPosition;

    vec4 clipStart = Projection * View * (SceneAxis * Model * vec4(LineStart, 1.0));
    vec4 clipEnd = Projection * View * (SceneAxis * Model * vec4(LineEnd, 1.0));
    vec4 clipPosition = mix(clipStart, clipEnd, Along);

    float safeStartW = max(abs(clipStart.w), 1e-5);
    float safeEndW = max(abs(clipEnd.w), 1e-5);

    vec2 ndcStart = clipStart.xy / safeStartW;
    vec2 ndcEnd = clipEnd.xy / safeEndW;

    vec2 viewport = max(ViewportSize, vec2(1.0));
    vec2 screenStart = (ndcStart * 0.5 + 0.5) * viewport;
    vec2 screenEnd = (ndcEnd * 0.5 + 0.5) * viewport;

    vec2 screenDirection = screenEnd - screenStart;
    float len = max(length(screenDirection), 1e-5);
    vec2 tangent = screenDirection / len;
    vec2 normal = vec2(-tangent.y, tangent.x);
    float halfWidth = LineHalfWidthPx * WidthScale;
    float capSign = (Along * 2.0) - 1.0;

    vec2 offsetScreen = (normal * Side * halfWidth) + (tangent * capSign * halfWidth);
    vec2 offsetNdc = (offsetScreen / viewport) * 2.0;
    clipPosition.xy += offsetNdc * clipPosition.w;

    WorldPosition = worldPosition.xyz;
    ColorValue = Color;
    gl_Position = clipPosition;
}