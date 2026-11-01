#define PROBE_TEXELS 4

uniform int HasIrradianceProbes;
uniform highp sampler2D IrradianceProbes;
uniform vec3 ProbeGridOrigin;
uniform vec3 ProbeGridSpacing;
uniform vec3 ProbeGridCounts;
uniform float ProbeDistanceScale;
uniform int ProbeCapture;

const float ProbeNormalBias = 0.25;
const float ProbeOccludedWeight = 0.02;
const float ProbeMinimumWeight = 0.0001;

vec3 SampleProbeIrradiance(vec3 position, vec3 normal, vec3 fallback)
{
    vec3 counts = ProbeGridCounts;
    vec3 biased = position + normal * min(ProbeGridSpacing.x, min(ProbeGridSpacing.y, ProbeGridSpacing.z)) * ProbeNormalBias;
    vec3 grid = (biased - ProbeGridOrigin) / ProbeGridSpacing;
    vec3 base = clamp(floor(grid), vec3(0.0), counts - 2.0);
    vec3 fraction = clamp(grid - base, 0.0, 1.0);

    vec3 irradiance = vec3(0.0);
    float totalWeight = 0.0;
    for (int i = 0; i < 8; i++)
    {
        vec3 offset = vec3(float(i & 1), float((i >> 1) & 1), float((i >> 2) & 1));
        vec3 index = base + offset;
        vec3 probePosition = ProbeGridOrigin + index * ProbeGridSpacing;
        ivec2 texel = ivec2(int(index.x) * PROBE_TEXELS, int(index.y) + int(index.z) * int(counts.y));

        vec4 average = texelFetch(IrradianceProbes, texel, 0);
        vec4 x = texelFetch(IrradianceProbes, texel + ivec2(1, 0), 0);
        vec4 y = texelFetch(IrradianceProbes, texel + ivec2(2, 0), 0);
        vec4 z = texelFetch(IrradianceProbes, texel + ivec2(3, 0), 0);
        vec3 trilinear = mix(1.0 - fraction, fraction, offset);

        vec3 toSurface = biased - probePosition;
        float surfaceDistance = length(toSurface) / ProbeDistanceScale;
        vec3 direction = toSurface / max(length(toSurface), 1e-5);
        float visibleDistance = abs(average.a) + dot(vec3(x.a, y.a, z.a), direction);
        float validity = average.a > 0.0 ? 1.0 : 0.0;
        float facing = (dot(-direction, normal) + 1.0) * 0.5;

        float weight = trilinear.x * trilinear.y * trilinear.z * validity * (facing * facing + 0.05);
        if (surfaceDistance > visibleDistance * 1.1 + 0.1)
        {
            weight *= ProbeOccludedWeight;
        }

        irradiance += max(average.rgb + x.rgb * normal.x + y.rgb * normal.y + z.rgb * normal.z, 0.0) * weight;
        totalWeight += weight;
    }

    return totalWeight > ProbeMinimumWeight ? irradiance / totalWeight : fallback;
}
