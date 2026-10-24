#version 300 es
precision highp float;
precision highp int;
precision highp sampler2D;

#define PI 3.14159265
#define HALF_PI 1.57079633

uniform sampler2D SceneDepthMap;
uniform mat4 Projection;
uniform mat4 InverseProjection;
uniform float Radius;
uniform float Intensity;
uniform int SliceCount;
uniform int StepCount;

out vec4 FragColor;

const float FalloffRange = 0.615;
const float SampleDistributionPower = 2.0;

const float MinimumSampleDistance = 1.3;

const float MaximumScreenRadius = 0.25;

const int NoiseIndices[16] = int[16](0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5);

vec3 ViewPosition(ivec2 pixel, ivec2 size)
{
    ivec2 clamped = clamp(pixel, ivec2(0), size - 1);
    float depth = texelFetch(SceneDepthMap, clamped, 0).r;
    vec2 uv = (vec2(clamped) + 0.5) / vec2(size);
    vec4 position = InverseProjection * vec4(vec3(uv, depth) * 2.0 - 1.0, 1.0);
    return position.xyz / position.w;
}

void main()
{
    ivec2 size = textureSize(SceneDepthMap, 0);
    ivec2 pixel = ivec2(gl_FragCoord.xy);

    if (texelFetch(SceneDepthMap, pixel, 0).r >= 1.0)
    {
        FragColor = vec4(1.0);
        return;
    }

    vec3 center = ViewPosition(pixel, size);
    vec3 left = ViewPosition(pixel - ivec2(1, 0), size);
    vec3 right = ViewPosition(pixel + ivec2(1, 0), size);
    vec3 down = ViewPosition(pixel - ivec2(0, 1), size);
    vec3 up = ViewPosition(pixel + ivec2(0, 1), size);

    vec3 tangentX = abs(center.z - left.z) < abs(right.z - center.z) ? center - left : right - center;
    vec3 tangentY = abs(center.z - down.z) < abs(up.z - center.z) ? center - down : up - center;
    vec3 normal = normalize(cross(tangentX, tangentY));

    vec3 viewDirection = Projection[2][3] != 0.0 ? normalize(-center) : vec3(0.0, 0.0, 1.0);
    float clipW = (Projection * vec4(center, 1.0)).w;
    float screenRadius = min(Radius * Projection[0][0] * 0.5 * float(size.x) / clipW, MaximumScreenRadius * float(size.y));

    if (screenRadius < MinimumSampleDistance)
    {
        FragColor = vec4(1.0);
        return;
    }

    float falloffRange = FalloffRange * Radius;
    float falloffScale = -1.0 / falloffRange;
    float falloffOffset = Radius / falloffRange;
    float minimumReach = MinimumSampleDistance / screenRadius;

    int noiseIndex = NoiseIndices[(pixel.y & 3) * 4 + (pixel.x & 3)];
    vec2 noise = fract(0.5 + float(noiseIndex) * vec2(0.75487766, 0.56984029));

    float visibility = 0.0;
    for (int slice = 0; slice < SliceCount; slice++)
    {
        float angle = (float(slice) + noise.x) / float(SliceCount) * PI;
        vec2 direction = vec2(cos(angle), sin(angle));

        vec3 sliceDirection = vec3(direction, 0.0);
        vec3 orthogonalDirection = sliceDirection - dot(sliceDirection, viewDirection) * viewDirection;
        vec3 axis = normalize(cross(orthogonalDirection, viewDirection));
        vec3 projectedNormal = normal - axis * dot(normal, axis);
        float projectedLength = max(length(projectedNormal), 1e-6);
        float cosNormal = clamp(dot(projectedNormal, viewDirection) / projectedLength, 0.0, 1.0);
        float n = sign(dot(orthogonalDirection, projectedNormal)) * acos(cosNormal);

        float lowHorizon0 = cos(n + HALF_PI);
        float lowHorizon1 = cos(n - HALF_PI);
        float horizon0 = lowHorizon0;
        float horizon1 = lowHorizon1;

        for (int i = 0; i < StepCount; i++)
        {
            float jitter = fract(noise.y + float(slice + i * StepCount) * 0.61803399);
            float reach = pow((float(i) + jitter) / float(StepCount), SampleDistributionPower) + minimumReach;
            ivec2 offset = ivec2(round(direction * reach * screenRadius));

            vec3 delta0 = ViewPosition(pixel + offset, size) - center;
            vec3 delta1 = ViewPosition(pixel - offset, size) - center;
            float length0 = max(length(delta0), 1e-6);
            float length1 = max(length(delta1), 1e-6);
            float weight0 = clamp(length0 * falloffScale + falloffOffset, 0.0, 1.0);
            float weight1 = clamp(length1 * falloffScale + falloffOffset, 0.0, 1.0);

            horizon0 = max(horizon0, mix(lowHorizon0, dot(delta0, viewDirection) / length0, weight0));
            horizon1 = max(horizon1, mix(lowHorizon1, dot(delta1, viewDirection) / length1, weight1));
        }

        float h0 = n + clamp(-acos(horizon1) - n, -HALF_PI, HALF_PI);
        float h1 = n + clamp(acos(horizon0) - n, -HALF_PI, HALF_PI);
        float arc0 = (cosNormal + 2.0 * h0 * sin(n) - cos(2.0 * h0 - n)) * 0.25;
        float arc1 = (cosNormal + 2.0 * h1 * sin(n) - cos(2.0 * h1 - n)) * 0.25;
        visibility += projectedLength * (arc0 + arc1);
    }

    visibility = clamp(visibility / float(SliceCount), 0.0, 1.0);
    FragColor = vec4(pow(visibility, Intensity), 0.0, 0.0, 1.0);
}
