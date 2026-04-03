in vec3 vRayDirection;

uniform sampler2D uEnvironmentMap;
uniform float uExposure;
uniform bool uBlurEnabled;
uniform float uBlurRadius;
uniform vec2 uResolution;

out vec4 FragColor;

vec2 equirectDirectionToUv(vec3 dir)
{
    float u = 0.5 + atan(dir.z, dir.x) / 6.28318530;
    float v = acos(clamp(dir.y, -1.0, 1.0)) / 3.14159265;
    return vec2(u, v);
}

vec3 tonemapReinhard(vec3 color)
{
    return color / (color + vec3(1.0));
}

/// Computes Gaussian weight for a given distance from center.
float gaussianWeight(float distance, float sigma)
{
    float sigma2 = sigma * sigma;
    return exp(-(distance * distance) / (2.0 * sigma2));
}

/// Build an orthonormal basis (tangent, bitangent, normal) from a direction.
void makeBasis(vec3 normal, out vec3 tangent, out vec3 bitangent)
{
    vec3 a = abs(normal.z) < 0.999 ? vec3(0.0, 0.0, 1.0) : vec3(1.0, 0.0, 0.0);
    tangent = normalize(cross(a, normal));
    bitangent = cross(normal, tangent);
}

vec3 sampleEnvironmentMap(vec3 dir)
{
    vec2 uv = equirectDirectionToUv(dir);

    if (!uBlurEnabled || uBlurRadius <= 0.0)
        return texture(uEnvironmentMap, uv).rgb;

    // Convert blur radius from texels to radians
    float texelAngle = 6.28318530 / uResolution.x;
    float sigma = uBlurRadius * texelAngle;  // sigma in radians

    // Sample out to 4σ so edge weight is exp(-8) ≈ 0.03% — invisible even with HDR
    int maxR = int(ceil(uBlurRadius * 4.0));
    maxR = clamp(maxR, 1, 64);

    // Build tangent-space basis for this direction
    vec3 N = normalize(dir);
    vec3 T, B;
    makeBasis(N, T, B);

    vec3 result = vec3(0.0);
    float totalWeight = 0.0;

    // Jittered grid sampling — breaks up regular grid patterns
    float seed = fract(sin(dot(uv, vec2(12.9898, 78.233))) * 43758.5453);

    for (int vy = -64; vy <= 64; vy++)
    {
        if (abs(vy) > maxR) continue;

        for (int vx = -64; vx <= 64; vx++)
        {
            if (abs(vx) > maxR) continue;

            // Per-fragment jitter to decorrelate the grid
            float jitterX = fract(sin(float(vx) * 12.9898 + float(vy) * 78.233 + seed) * 43758.5453) * 2.0 - 1.0;
            float jitterY = fract(sin(float(vx) * 45.164 + float(vy) * 62.371 + seed * 2.0) * 43758.5453) * 2.0 - 1.0;

            vec2 jitteredOffset = vec2(float(vx) + jitterX * 0.5, float(vy) + jitterY * 0.5);
            float dist = length(jitteredOffset);

            // Circular cutoff at 4σ
            if (dist > float(maxR)) continue;

            // Convert to angular offset
            float angleX = jitteredOffset.x * texelAngle;
            float angleY = jitteredOffset.y * texelAngle;
            float angularDist = length(vec2(angleX, angleY));

            // Build 3D sample direction on the sphere
            vec3 sampleDir = N + T * tan(angleX) + B * tan(angleY);
            sampleDir = normalize(sampleDir);

            vec2 sampleUv = equirectDirectionToUv(sampleDir);
            vec3 sampleColor = texture(uEnvironmentMap, sampleUv).rgb;

            // Gaussian weight
            float weight = exp(-(angularDist * angularDist) / (2.0 * sigma * sigma));

            result += sampleColor * weight;
            totalWeight += weight;
        }
    }

    return result / totalWeight;
}

void main()
{
    vec3 dir = normalize(vRayDirection);
    vec3 envColor = sampleEnvironmentMap(dir);
    FragColor = vec4(tonemapReinhard(envColor * uExposure), 1.0);
}
