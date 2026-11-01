#version 300 es
precision highp float;
precision highp int;
precision highp sampler2D;

#include "Environment.glsl"

#define PROBE_TEXELS 4

uniform sampler2D FaceAtlas;
uniform int FaceSize;
uniform float MissDistance;

out vec4 FragColor;

const float SkyRoughness = 0.35;
const float SunMaskScale = 2.0;
const float MinimumSunMask = 0.05;
const float InvalidBackfaceFraction = 0.25;

const vec3 FaceForwards[6] = vec3[6](vec3(1.0, 0.0, 0.0), vec3(-1.0, 0.0, 0.0), vec3(0.0, 1.0, 0.0), vec3(0.0, -1.0, 0.0), vec3(0.0, 0.0, 1.0), vec3(0.0, 0.0, -1.0));
const vec3 FaceUps[6] = vec3[6](vec3(0.0, 1.0, 0.0), vec3(0.0, 1.0, 0.0), vec3(0.0, 0.0, -1.0), vec3(0.0, 0.0, 1.0), vec3(0.0, 1.0, 0.0), vec3(0.0, 1.0, 0.0));

bool IsDirectLight(vec3 direction)
{
    for (int i = 0; i < EnvironmentLightCount; i++)
    {
        float radius = max(EnvironmentLightDirections[i].w * SunMaskScale, MinimumSunMask);
        if (dot(direction, EnvironmentLightDirections[i].xyz) > cos(radius))
        {
            return true;
        }
    }

    return false;
}

void main()
{
    int coefficient = int(gl_FragCoord.x) % PROBE_TEXELS;
    float texelSize = 2.0 / float(FaceSize);

    vec3 average = vec3(0.0);
    vec3 axisX = vec3(0.0);
    vec3 axisY = vec3(0.0);
    vec3 axisZ = vec3(0.0);
    vec4 depth = vec4(0.0);
    float backfaceWeight = 0.0;
    float totalWeight = 0.0;

    for (int face = 0; face < 6; face++)
    {
        vec3 forward = FaceForwards[face];
        vec3 right = normalize(cross(forward, FaceUps[face]));
        vec3 up = cross(right, forward);

        for (int y = 0; y < FaceSize; y++)
        {
            for (int x = 0; x < FaceSize; x++)
            {
                vec2 ndc = (vec2(x, y) + 0.5) * texelSize - 1.0;
                vec3 ray = forward + right * ndc.x + up * ndc.y;
                float lengthSquared = dot(ray, ray);
                float solidAngle = texelSize * texelSize / (lengthSquared * sqrt(lengthSquared));
                vec3 direction = ray * inversesqrt(lengthSquared);

                vec4 sampleValue = texelFetch(FaceAtlas, ivec2(face * FaceSize + x, y), 0);
                vec3 radiance = sampleValue.rgb;
                float hitDistance = abs(sampleValue.a);

                if (sampleValue.a == 0.0)
                {
                    radiance = IsDirectLight(direction) ? vec3(0.0) : SampleEnvironment(direction, SkyRoughness);
                    hitDistance = MissDistance;
                }
                else if (sampleValue.a < 0.0)
                {
                    backfaceWeight += solidAngle;
                }

                average += radiance * solidAngle;
                axisX += radiance * direction.x * solidAngle;
                axisY += radiance * direction.y * solidAngle;
                axisZ += radiance * direction.z * solidAngle;
                depth += vec4(direction * hitDistance, hitDistance) * solidAngle;
                totalWeight += solidAngle;
            }
        }
    }

    float normalization = 4.0 * PI / totalWeight;
    float depthAverage = depth.w * normalization * 0.0795775;
    vec3 depthAxis = depth.xyz * normalization * 0.238732;
    float validity = backfaceWeight / totalWeight > InvalidBackfaceFraction ? -1.0 : 1.0;

    if (coefficient == 0)
    {
        FragColor = vec4(average * normalization * 0.0795775, depthAverage * validity);
    }
    else if (coefficient == 1)
    {
        FragColor = vec4(axisX * normalization * 0.159155, depthAxis.x);
    }
    else if (coefficient == 2)
    {
        FragColor = vec4(axisY * normalization * 0.159155, depthAxis.y);
    }
    else
    {
        FragColor = vec4(axisZ * normalization * 0.159155, depthAxis.z);
    }
}
