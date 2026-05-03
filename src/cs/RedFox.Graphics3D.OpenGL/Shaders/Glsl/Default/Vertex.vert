#version 300 es
precision highp float;
precision highp int;
precision highp sampler2D;
precision highp usampler2D;

layout (location = 0) in vec3 Positions;
layout (location = 1) in vec3 Normals;

uniform mat4 Model;
uniform mat4 SceneAxis;
uniform mat4 View;
uniform mat4 Projection;
uniform int SkinInfluenceCount;
uniform int SkinningMode;
uniform usampler2D BoneIndexBuffer;
uniform sampler2D BoneWeightBuffer;
uniform sampler2D SkinTransformBuffer;

out vec3 WorldPosition;
out vec3 WorldNormal;

const int SkinningModeLinear = 0;
const int SkinningModeDualQuaternion = 1;

ivec2 GetLinearTextureCoordinate(ivec2 textureDimensions, int index)
{
    int width = max(textureDimensions.x, 1);
    return ivec2(index % width, index / width);
}

uvec4 FetchUIntBufferTexel(usampler2D textureSampler, int index)
{
    return texelFetch(textureSampler, GetLinearTextureCoordinate(textureSize(textureSampler, 0), index), 0);
}

vec4 FetchFloatBufferTexel(sampler2D textureSampler, int index)
{
    return texelFetch(textureSampler, GetLinearTextureCoordinate(textureSize(textureSampler, 0), index), 0);
}

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

mat4 LoadSkinTransform(uint boneIndex)
{
    int rowIndex = int(boneIndex) * 4;
    return mat4(
        FetchFloatBufferTexel(SkinTransformBuffer, rowIndex + 0),
        FetchFloatBufferTexel(SkinTransformBuffer, rowIndex + 1),
        FetchFloatBufferTexel(SkinTransformBuffer, rowIndex + 2),
        FetchFloatBufferTexel(SkinTransformBuffer, rowIndex + 3));
}

void ResolveSkinnedVertex(uint vertexIndex, vec3 sourcePosition, vec3 sourceNormal, out vec3 outputPosition, out vec3 outputNormal)
{
    outputPosition = sourcePosition;
    outputNormal = sourceNormal;

    if (SkinInfluenceCount <= 0)
    {
        return;
    }

    outputPosition = vec3(0.0);
    outputNormal = vec3(0.0);
    float totalWeight = 0.0;

    vec4 blendedRotationQuaternion = vec4(0.0);
    vec4 blendedDualQuaternion = vec4(0.0);
    bool hasQuaternionReference = false;
    vec4 quaternionReference = vec4(0.0);

    int baseIndex = int(vertexIndex) * SkinInfluenceCount;
    for (int influenceIndex = 0; influenceIndex < SkinInfluenceCount; influenceIndex++)
    {
        float weight = FetchFloatBufferTexel(BoneWeightBuffer, baseIndex + influenceIndex).x;
        if (weight <= 0.0)
        {
            continue;
        }

        uint boneIndex = FetchUIntBufferTexel(BoneIndexBuffer, baseIndex + influenceIndex).x;
        mat4 transform = LoadSkinTransform(boneIndex);

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

    if (totalWeight <= 0.0)
    {
        outputPosition = sourcePosition;
        outputNormal = sourceNormal;
        return;
    }

    if (SkinningMode == SkinningModeDualQuaternion)
    {
        float quaternionLength = length(blendedRotationQuaternion);
        if (quaternionLength > 1e-8)
        {
            vec4 normalizedRotationQuaternion = blendedRotationQuaternion / quaternionLength;
            vec4 normalizedDualQuaternion = blendedDualQuaternion / quaternionLength;
            outputPosition = TransformPositionByDualQuaternion(sourcePosition, normalizedRotationQuaternion, normalizedDualQuaternion);
            outputNormal = RotateVectorByQuaternion(sourceNormal, normalizedRotationQuaternion);
            return;
        }

        outputPosition = sourcePosition;
        outputNormal = sourceNormal;
        return;
    }

    outputPosition /= totalWeight;
    outputNormal /= totalWeight;
}

void main()
{
    vec3 resolvedPosition;
    vec3 resolvedNormal;
    ResolveSkinnedVertex(uint(gl_VertexID), Positions, Normals, resolvedPosition, resolvedNormal);

    mat4 worldMatrix = SceneAxis * Model;
    vec4 worldPosition = worldMatrix * vec4(resolvedPosition, 1.0);
    WorldPosition = worldPosition.xyz;
    mat3 normalMatrix = mat3(transpose(inverse(worldMatrix)));
    WorldNormal = normalize(normalMatrix * resolvedNormal);
    gl_Position = Projection * View * worldPosition;
}