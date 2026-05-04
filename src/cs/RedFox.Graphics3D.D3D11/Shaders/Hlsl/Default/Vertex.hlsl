cbuffer FrameConstants : register(b0)
{
    row_major float4x4 Model;
    row_major float4x4 SceneAxis;
    row_major float4x4 View;
    row_major float4x4 Projection;
};

cbuffer SkinningConstants : register(b1)
{
    int SkinInfluenceCount;
    int SkinningMode;
    int UVLayerCount;
    int UVLayerIndex;
};

Buffer<uint> BoneIndexBuffer : register(t12);
Buffer<float> BoneWeightBuffer : register(t13);
Buffer<float4> SkinTransformBuffer : register(t14);
Buffer<float2> UVLayerBuffer : register(t15);

static const int SkinningModeLinear = 0;
static const int SkinningModeDualQuaternion = 1;

struct VSInput
{
    float3 Positions : POSITION;
    float3 Normals : NORMAL;
};

struct VSOutput
{
    float4 Position : SV_Position;
    float3 WorldPosition : TEXCOORD0;
    float3 WorldNormal : TEXCOORD1;
    float2 TextureCoordinate : TEXCOORD2;
};

float4 QuaternionMultiply(float4 a, float4 b)
{
    return float4(
        (a.w * b.x) + (a.x * b.w) + (a.y * b.z) - (a.z * b.y),
        (a.w * b.y) - (a.x * b.z) + (a.y * b.w) + (a.z * b.x),
        (a.w * b.z) + (a.x * b.y) - (a.y * b.x) + (a.z * b.w),
        (a.w * b.w) - (a.x * b.x) - (a.y * b.y) - (a.z * b.z));
}

float4 QuaternionConjugate(float4 quaternion)
{
    return float4(-quaternion.xyz, quaternion.w);
}

float3 RotateVectorByQuaternion(float3 value, float4 quaternion)
{
    float4 valueQuaternion = float4(value, 0.0f);
    float4 rotatedQuaternion = QuaternionMultiply(QuaternionMultiply(quaternion, valueQuaternion), QuaternionConjugate(quaternion));
    return rotatedQuaternion.xyz;
}

float4 QuaternionFromMatrix(float3x3 basis)
{
    float3x3 m = transpose(basis);
    float trace = m[0][0] + m[1][1] + m[2][2];

    if (trace > 0.0f)
    {
        float scale = sqrt(trace + 1.0f) * 2.0f;
        return float4(
            (m[2][1] - m[1][2]) / scale,
            (m[0][2] - m[2][0]) / scale,
            (m[1][0] - m[0][1]) / scale,
            0.25f * scale);
    }

    if (m[0][0] > m[1][1] && m[0][0] > m[2][2])
    {
        float scale = sqrt(1.0f + m[0][0] - m[1][1] - m[2][2]) * 2.0f;
        return float4(
            0.25f * scale,
            (m[0][1] + m[1][0]) / scale,
            (m[0][2] + m[2][0]) / scale,
            (m[2][1] - m[1][2]) / scale);
    }

    if (m[1][1] > m[2][2])
    {
        float scale = sqrt(1.0f + m[1][1] - m[0][0] - m[2][2]) * 2.0f;
        return float4(
            (m[0][1] + m[1][0]) / scale,
            0.25f * scale,
            (m[1][2] + m[2][1]) / scale,
            (m[0][2] - m[2][0]) / scale);
    }

    float lastScale = sqrt(1.0f + m[2][2] - m[0][0] - m[1][1]) * 2.0f;
    return float4(
        (m[0][2] + m[2][0]) / lastScale,
        (m[1][2] + m[2][1]) / lastScale,
        0.25f * lastScale,
        (m[1][0] - m[0][1]) / lastScale);
}

void BuildDualQuaternion(float4x4 transform, out float4 rotationQuaternion, out float4 dualQuaternion)
{
    rotationQuaternion = normalize(QuaternionFromMatrix((float3x3)transform));
    float3 translation = transform[3].xyz;
    float4 translationQuaternion = float4(translation, 0.0f);
    dualQuaternion = 0.5f * QuaternionMultiply(translationQuaternion, rotationQuaternion);
}

float3 TransformPositionByDualQuaternion(float3 position, float4 rotationQuaternion, float4 dualQuaternion)
{
    float3 rotatedPosition = RotateVectorByQuaternion(position, rotationQuaternion);
    float4 translationQuaternion = QuaternionMultiply(dualQuaternion, QuaternionConjugate(rotationQuaternion));
    float3 translation = 2.0f * translationQuaternion.xyz;
    return rotatedPosition + translation;
}

row_major float4x4 LoadSkinTransform(uint boneIndex)
{
    uint rowIndex = boneIndex * 4u;
    return float4x4(
        SkinTransformBuffer[rowIndex + 0u],
        SkinTransformBuffer[rowIndex + 1u],
        SkinTransformBuffer[rowIndex + 2u],
        SkinTransformBuffer[rowIndex + 3u]);
}

float2 ResolveTextureCoordinate(uint vertexIndex)
{
    if (UVLayerCount <= 0)
    {
        return float2(0.0f, 0.0f);
    }

    int layerIndex = clamp(UVLayerIndex, 0, UVLayerCount - 1);
    uint bufferIndex = (vertexIndex * (uint)UVLayerCount) + (uint)layerIndex;
    return UVLayerBuffer[bufferIndex];
}

void ResolveSkinnedVertex(uint vertexIndex, float3 sourcePosition, float3 sourceNormal, out float3 outputPosition, out float3 outputNormal)
{
    outputPosition = sourcePosition;
    outputNormal = sourceNormal;

    if (SkinInfluenceCount <= 0)
    {
        return;
    }

    outputPosition = float3(0.0f, 0.0f, 0.0f);
    outputNormal = float3(0.0f, 0.0f, 0.0f);
    float totalWeight = 0.0f;

    float4 blendedRotationQuaternion = float4(0.0f, 0.0f, 0.0f, 0.0f);
    float4 blendedDualQuaternion = float4(0.0f, 0.0f, 0.0f, 0.0f);
    bool hasQuaternionReference = false;
    float4 quaternionReference = float4(0.0f, 0.0f, 0.0f, 0.0f);

    uint baseIndex = vertexIndex * (uint)SkinInfluenceCount;
    for (int influenceIndex = 0; influenceIndex < SkinInfluenceCount; influenceIndex++)
    {
        float weight = BoneWeightBuffer[baseIndex + (uint)influenceIndex];
        if (weight <= 0.0f)
        {
            continue;
        }

        uint boneIndex = BoneIndexBuffer[baseIndex + (uint)influenceIndex];
        row_major float4x4 transform = LoadSkinTransform(boneIndex);

        if (SkinningMode == SkinningModeDualQuaternion)
        {
            float4 rotationQuaternion;
            float4 dualQuaternion;
            BuildDualQuaternion(transform, rotationQuaternion, dualQuaternion);

            if (!hasQuaternionReference)
            {
                quaternionReference = rotationQuaternion;
                hasQuaternionReference = true;
            }
            else if (dot(rotationQuaternion, quaternionReference) < 0.0f)
            {
                rotationQuaternion = -rotationQuaternion;
                dualQuaternion = -dualQuaternion;
            }

            blendedRotationQuaternion += rotationQuaternion * weight;
            blendedDualQuaternion += dualQuaternion * weight;
        }
        else
        {
            outputPosition += mul(float4(sourcePosition, 1.0f), transform).xyz * weight;
            outputNormal += mul(float4(sourceNormal, 0.0f), transform).xyz * weight;
        }

        totalWeight += weight;
    }

    if (totalWeight <= 0.0f)
    {
        outputPosition = sourcePosition;
        outputNormal = sourceNormal;
        return;
    }

    if (SkinningMode == SkinningModeDualQuaternion)
    {
        float quaternionLength = length(blendedRotationQuaternion);
        if (quaternionLength > 1e-8f)
        {
            float4 normalizedRotationQuaternion = blendedRotationQuaternion / quaternionLength;
            float4 normalizedDualQuaternion = blendedDualQuaternion / quaternionLength;
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

VSOutput Main(VSInput input, uint vertexId : SV_VertexID)
{
    VSOutput output;
    float3 resolvedPosition;
    float3 resolvedNormal;
    ResolveSkinnedVertex(vertexId, input.Positions, input.Normals, resolvedPosition, resolvedNormal);
    row_major float4x4 worldMatrix = mul(Model, SceneAxis);
    float4 worldPosition = mul(float4(resolvedPosition, 1.0f), worldMatrix);
    output.WorldPosition = worldPosition.xyz;
    output.WorldNormal = normalize(mul(float4(resolvedNormal, 0.0f), worldMatrix).xyz);
    output.TextureCoordinate = ResolveTextureCoordinate(vertexId);
    output.Position = mul(worldPosition, mul(View, Projection));
    return output;
}