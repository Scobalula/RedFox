namespace RedFox.Rendering.OpenGL.Shaders;

/// <summary>
/// GLSL source for the default mesh, skinning, and grid/line shaders used by the OpenGL renderer.
/// </summary>
internal static class BasicShaders
{
    public const string MeshVertexShaderSource = """
#version 330 core

layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec3 aNormal;

uniform mat4 uModel;
uniform mat4 uSceneAxis;
uniform mat4 uView;
uniform mat4 uProjection;

out vec3 vWorldPosition;
out vec3 vNormal;

void main()
{
    mat4 worldMatrix = uSceneAxis * uModel;
    vec4 worldPosition = worldMatrix * vec4(aPosition, 1.0);
    vWorldPosition = worldPosition.xyz;
    mat3 normalMatrix = mat3(transpose(inverse(worldMatrix)));
    vNormal = normalize(normalMatrix * aNormal);
    gl_Position = uProjection * uView * worldPosition;
}
""";

    public const string MeshFragmentShaderSource = """
#version 330 core

in vec3 vWorldPosition;
in vec3 vNormal;

#define MAX_LIGHTS 4

uniform vec3 uAmbientColor;
uniform int uLightCount;
uniform vec4 uLightDirectionsAndIntensity[MAX_LIGHTS];
uniform vec3 uLightColors[MAX_LIGHTS];
uniform vec3 uCameraPosition;
uniform int uUseViewBasedLighting;
uniform vec4 uBaseColor;
uniform float uMaterialSpecularStrength;
uniform float uMaterialSpecularPower;

out vec4 FragColor;

void main()
{
    vec3 normal = normalize(vNormal);
    vec3 viewDirection = normalize(uCameraPosition - vWorldPosition);
    vec3 ambient = uAmbientColor;

    if (uUseViewBasedLighting != 0)
    {
        float facing = max(dot(normal, viewDirection), 0.0);
        vec3 lit = (ambient + vec3(facing)) * uBaseColor.rgb;
        FragColor = vec4(lit, uBaseColor.a);
        return;
    }

    vec3 diffuse = vec3(0.0);
    vec3 specular = vec3(0.0);

    int count = clamp(uLightCount, 0, MAX_LIGHTS);
    for (int i = 0; i < count; i++)
    {
        vec3 lightDirection = normalize(-uLightDirectionsAndIntensity[i].xyz);
        float lightIntensity = uLightDirectionsAndIntensity[i].w;
        float nDotL = max(dot(normal, lightDirection), 0.0);
        diffuse += uLightColors[i] * (lightIntensity * nDotL);

        if (nDotL > 0.0)
        {
            vec3 reflected = reflect(-lightDirection, normal);
            float spec = pow(max(dot(viewDirection, reflected), 0.0), uMaterialSpecularPower);
            specular += uLightColors[i] * (lightIntensity * spec * uMaterialSpecularStrength);
        }
    }

    vec3 lit = ((ambient + diffuse) * uBaseColor.rgb) + specular;
    FragColor = vec4(lit, uBaseColor.a);
}
""";

    public const string SkinningComputeShaderSource = """
#version 430 core

layout(local_size_x = 64) in;

layout(std430, binding = 0) readonly buffer Positions
{
    float Position[];
};

layout(std430, binding = 1) readonly buffer Normals
{
    float Normal[];
};

layout(std430, binding = 2) readonly buffer BoneIndices
{
    uint BoneIndex[];
};

layout(std430, binding = 3) readonly buffer BoneWeights
{
    float BoneWeight[];
};

layout(std430, binding = 4) readonly buffer SkinTransforms
{
    mat4 SkinTransform[];
};

layout(std430, binding = 5) writeonly buffer SkinnedPositions
{
    float SkinnedPosition[];
};

layout(std430, binding = 6) writeonly buffer SkinnedNormals
{
    float SkinnedNormal[];
};

uniform int VertexCount;
uniform int SkinInfluenceCount;
uniform int SkinningMode;

const int SkinningModeLinear = 0;
const int SkinningModeDualQuaternion = 1;

vec4 QuaternionMultiply(vec4 a, vec4 b)
{
    return vec4(
        a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
        a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
        a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w,
        a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);
}

vec4 QuaternionConjugate(vec4 q)
{
    return vec4(-q.xyz, q.w);
}

vec3 RotateVectorByQuaternion(vec3 value, vec4 q)
{
    vec4 valueQuat = vec4(value, 0.0);
    vec4 rotatedQuat = QuaternionMultiply(QuaternionMultiply(q, valueQuat), QuaternionConjugate(q));
    return rotatedQuat.xyz;
}

vec4 QuaternionFromMatrix(mat3 matrix)
{
    // GLSL matrices are column-major (m[col][row]); transpose so indexing matches row-major formulas.
    mat3 m = transpose(matrix);

    float trace = m[0][0] + m[1][1] + m[2][2];
    vec4 result;

    if (trace > 0.0)
    {
        float s = sqrt(trace + 1.0) * 2.0;
        result.w = 0.25 * s;
        result.x = (m[2][1] - m[1][2]) / s;
        result.y = (m[0][2] - m[2][0]) / s;
        result.z = (m[1][0] - m[0][1]) / s;
        return result;
    }

    if (m[0][0] > m[1][1] && m[0][0] > m[2][2])
    {
        float s = sqrt(1.0 + m[0][0] - m[1][1] - m[2][2]) * 2.0;
        result.w = (m[2][1] - m[1][2]) / s;
        result.x = 0.25 * s;
        result.y = (m[0][1] + m[1][0]) / s;
        result.z = (m[0][2] + m[2][0]) / s;
        return result;
    }

    if (m[1][1] > m[2][2])
    {
        float s = sqrt(1.0 + m[1][1] - m[0][0] - m[2][2]) * 2.0;
        result.w = (m[0][2] - m[2][0]) / s;
        result.x = (m[0][1] + m[1][0]) / s;
        result.y = 0.25 * s;
        result.z = (m[1][2] + m[2][1]) / s;
        return result;
    }

    float sLast = sqrt(1.0 + m[2][2] - m[0][0] - m[1][1]) * 2.0;
    result.w = (m[1][0] - m[0][1]) / sLast;
    result.x = (m[0][2] + m[2][0]) / sLast;
    result.y = (m[1][2] + m[2][1]) / sLast;
    result.z = 0.25 * sLast;
    return result;
}

void BuildDualQuaternion(mat4 transform, out vec4 rotationQuaternion, out vec4 dualQuaternion)
{
    rotationQuaternion = normalize(QuaternionFromMatrix(mat3(transform)));
    vec3 translation = transform[3].xyz;
    vec4 translationQuaternion = vec4(translation, 0.0);
    dualQuaternion = 0.5 * QuaternionMultiply(translationQuaternion, rotationQuaternion);
}

vec3 TransformPositionByDualQuaternion(vec3 position, vec4 rotationQuaternion, vec4 dualQuaternion)
{
    vec3 rotatedPosition = RotateVectorByQuaternion(position, rotationQuaternion);
    vec4 translationQuaternion = QuaternionMultiply(dualQuaternion, QuaternionConjugate(rotationQuaternion));
    vec3 translation = 2.0 * translationQuaternion.xyz;
    return rotatedPosition + translation;
}

void main()
{
    uint vertexIndex = gl_GlobalInvocationID.x;
    if (vertexIndex >= uint(VertexCount))
    {
        return;
    }

    uint positionOffset = vertexIndex * 3u;
    vec3 sourcePosition = vec3(
        Position[positionOffset + 0u],
        Position[positionOffset + 1u],
        Position[positionOffset + 2u]);
    vec3 sourceNormal = vec3(
        Normal[positionOffset + 0u],
        Normal[positionOffset + 1u],
        Normal[positionOffset + 2u]);

    vec3 outputPosition = vec3(0.0);
    vec3 outputNormal = vec3(0.0);
    float totalWeight = 0.0;

    vec4 blendedRotationQuaternion = vec4(0.0);
    vec4 blendedDualQuaternion = vec4(0.0);
    bool hasQuaternionReference = false;
    vec4 quaternionReference = vec4(0.0);

    uint baseOffset = vertexIndex * uint(max(SkinInfluenceCount, 0));
    for (int influenceIndex = 0; influenceIndex < SkinInfluenceCount; influenceIndex++)
    {
        uint packedOffset = baseOffset + uint(influenceIndex);
        float weight = BoneWeight[packedOffset];
        if (weight <= 0.0)
        {
            continue;
        }

        uint boneIndex = BoneIndex[packedOffset];
        mat4 transform = SkinTransform[boneIndex];

        if (SkinningMode == SkinningModeDualQuaternion)
        {
            vec4 rotationQuaternion;
            vec4 dualQuaternion;
            BuildDualQuaternion(transform, rotationQuaternion, dualQuaternion);

            if (!hasQuaternionReference)
            {
                quaternionReference = rotationQuaternion;
                hasQuaternionReference = true;
            }
            else if (dot(rotationQuaternion, quaternionReference) < 0.0)
            {
                rotationQuaternion = -rotationQuaternion;
                dualQuaternion = -dualQuaternion;
            }

            blendedRotationQuaternion += rotationQuaternion * weight;
            blendedDualQuaternion += dualQuaternion * weight;
        }
        else
        {
            outputPosition += (transform * vec4(sourcePosition, 1.0)).xyz * weight;
            outputNormal += (mat3(transform) * sourceNormal) * weight;
        }

        totalWeight += weight;
    }

    if (totalWeight > 0.0)
    {
        if (SkinningMode == SkinningModeDualQuaternion)
        {
            float quaternionLength = length(blendedRotationQuaternion);
            if (quaternionLength > 1e-8)
            {
                vec4 normalizedRotationQuaternion = blendedRotationQuaternion / quaternionLength;
                vec4 normalizedDualQuaternion = blendedDualQuaternion / quaternionLength;
                outputPosition = TransformPositionByDualQuaternion(sourcePosition, normalizedRotationQuaternion, normalizedDualQuaternion);
                outputNormal = RotateVectorByQuaternion(sourceNormal, normalizedRotationQuaternion);
            }
            else
            {
                outputPosition = sourcePosition;
                outputNormal = sourceNormal;
            }
        }
        else
        {
            outputPosition /= totalWeight;
            outputNormal /= totalWeight;
        }
    }
    else
    {
        outputPosition = sourcePosition;
        outputNormal = sourceNormal;
    }

    float lengthSquared = dot(outputNormal, outputNormal);
    vec3 normalizedNormal = lengthSquared > 1e-12 ? normalize(outputNormal) : vec3(0.0, 1.0, 0.0);

    SkinnedPosition[positionOffset + 0u] = outputPosition.x;
    SkinnedPosition[positionOffset + 1u] = outputPosition.y;
    SkinnedPosition[positionOffset + 2u] = outputPosition.z;
    SkinnedNormal[positionOffset + 0u] = normalizedNormal.x;
    SkinnedNormal[positionOffset + 1u] = normalizedNormal.y;
    SkinnedNormal[positionOffset + 2u] = normalizedNormal.z;
}
""";

    public const string LineVertexShaderSource = """
#version 330 core

layout (location = 0) in vec3 aLineStart;
layout (location = 1) in vec3 aLineEnd;
layout (location = 2) in vec4 aColor;
layout (location = 3) in float aAlong;
layout (location = 4) in float aSide;
layout (location = 5) in float aWidthScale;

uniform mat4 uModel;
uniform mat4 uSceneAxis;
uniform mat4 uView;
uniform mat4 uProjection;
uniform vec2 uViewportSize;
uniform float uLineHalfWidthPx;

out vec3 vWorldPosition;
out vec4 vColor;

void main()
{
    vec4 localPosition = vec4(mix(aLineStart, aLineEnd, aAlong), 1.0);
    vec4 worldPosition = uSceneAxis * uModel * localPosition;

    vec4 clipStart = uProjection * uView * (uSceneAxis * uModel * vec4(aLineStart, 1.0));
    vec4 clipEnd = uProjection * uView * (uSceneAxis * uModel * vec4(aLineEnd, 1.0));
    vec4 clipPosition = mix(clipStart, clipEnd, aAlong);

    float safeStartW = max(abs(clipStart.w), 1e-5);
    float safeEndW = max(abs(clipEnd.w), 1e-5);

    vec2 ndcStart = clipStart.xy / safeStartW;
    vec2 ndcEnd = clipEnd.xy / safeEndW;

    vec2 viewport = max(uViewportSize, vec2(1.0));
    vec2 screenStart = (ndcStart * 0.5 + 0.5) * viewport;
    vec2 screenEnd = (ndcEnd * 0.5 + 0.5) * viewport;

    vec2 screenDirection = screenEnd - screenStart;
    float len = max(length(screenDirection), 1e-5);
    vec2 tangent = screenDirection / len;
    vec2 normal = vec2(-tangent.y, tangent.x);
    float halfWidth = uLineHalfWidthPx * aWidthScale;
    float capSign = (aAlong * 2.0) - 1.0;

    vec2 offsetScreen = (normal * aSide * halfWidth) + (tangent * capSign * halfWidth);
    vec2 offsetNdc = (offsetScreen / viewport) * 2.0;
    clipPosition.xy += offsetNdc * clipPosition.w;

    vWorldPosition = worldPosition.xyz;
    vColor = aColor;
    gl_Position = clipPosition;
}
""";

    public const string LineFragmentShaderSource = """
#version 330 core

in vec3 vWorldPosition;
in vec4 vColor;

uniform vec3 uCameraPosition;
uniform float uFadeStartDistance;
uniform float uFadeEndDistance;

out vec4 FragColor;

void main()
{
    vec4 color = vColor;

    if (uFadeEndDistance > uFadeStartDistance)
    {
        float dist = distance(uCameraPosition, vWorldPosition);
        float t = clamp((dist - uFadeStartDistance) / (uFadeEndDistance - uFadeStartDistance), 0.0, 1.0);
        color.a *= 1.0 - t;
    }

    if (color.a <= 0.01)
    {
        discard;
    }

    FragColor = color;
}
""";
}
