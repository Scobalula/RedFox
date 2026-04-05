in vec3 vWorldPos;

uniform samplerCube uEnvironmentMap;
uniform float uBaseResolution;
uniform float uTargetMipLevel;

out vec4 FragColor;

float kernelWeight(int offset)
{
    int index = abs(offset);
    if (index == 0)
        return 70.0;
    if (index == 1)
        return 56.0;
    if (index == 2)
        return 28.0;
    if (index == 3)
        return 8.0;
    return 1.0;
}

void buildBasis(vec3 dir, out vec3 tangent, out vec3 bitangent)
{
    vec3 up = abs(dir.y) < 0.999 ? vec3(0.0, 1.0, 0.0) : vec3(1.0, 0.0, 0.0);
    tangent = normalize(cross(up, dir));
    bitangent = cross(dir, tangent);
}

void main()
{
    vec3 dir = normalize(vWorldPos);
    vec3 tangent;
    vec3 bitangent;
    buildBasis(dir, tangent, bitangent);

    float stepTexels = max(exp2(max(uTargetMipLevel - 1.0, 0.0)), 1.0);
    float offsetScale = (6.0 * stepTexels) / max(uBaseResolution, 1.0);

    vec3 color = vec3(0.0);
    float totalWeight = 0.0;

    for (int y = -4; y <= 4; y++)
    {
        float wy = kernelWeight(y);
        for (int x = -4; x <= 4; x++)
        {
            float weight = wy * kernelWeight(x);
            vec2 offset = vec2(float(x), float(y)) * offsetScale;
            vec3 sampleDir = normalize(dir + tangent * offset.x + bitangent * offset.y);
            color += textureLod(uEnvironmentMap, sampleDir, uTargetMipLevel - 1.0).rgb * weight;
            totalWeight += weight;
        }
    }

    FragColor = vec4(color / max(totalWeight, 1e-6), 1.0);
}
