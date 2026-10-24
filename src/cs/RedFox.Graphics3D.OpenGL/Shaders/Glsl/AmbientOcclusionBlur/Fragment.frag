#version 300 es
precision highp float;
precision highp int;
precision highp sampler2D;

uniform sampler2D OcclusionMap;
uniform sampler2D SceneDepthMap;
uniform mat4 InverseProjection;
uniform vec2 BlurDirection;

out vec4 FragColor;

const float Weights[5] = float[5](0.5, 1.0, 1.0, 1.0, 0.5);

const float DepthTolerance = 0.02;

float ViewDepth(ivec2 pixel)
{
    float depth = texelFetch(SceneDepthMap, pixel, 0).r;
    vec4 position = InverseProjection * vec4(0.0, 0.0, depth * 2.0 - 1.0, 1.0);
    return position.z / position.w;
}

void main()
{
    ivec2 size = textureSize(SceneDepthMap, 0);
    ivec2 pixel = ivec2(gl_FragCoord.xy);
    ivec2 direction = ivec2(BlurDirection);

    float center = ViewDepth(pixel);
    float before = ViewDepth(clamp(pixel - direction, ivec2(0), size - 1));
    float after = ViewDepth(clamp(pixel + direction, ivec2(0), size - 1));

    float slope = abs(after - center) < abs(center - before) ? after - center : center - before;
    float tolerance = abs(center) * DepthTolerance;

    float occlusion = 0.0;
    float total = 0.0;
    for (int i = -2; i <= 2; i++)
    {
        ivec2 tap = clamp(pixel + direction * i, ivec2(0), size - 1);
        float difference = (ViewDepth(tap) - center - slope * float(i)) / tolerance;
        float weight = Weights[i + 2] * exp2(-difference * difference);
        occlusion += texelFetch(OcclusionMap, tap, 0).r * weight;
        total += weight;
    }

    FragColor = vec4(occlusion / total, 0.0, 0.0, 1.0);
}
