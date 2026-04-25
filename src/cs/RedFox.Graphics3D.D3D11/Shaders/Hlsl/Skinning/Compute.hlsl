 #pragma pack_matrix(row_major)

 ByteAddressBuffer Positions : register(t0);
 ByteAddressBuffer Normals : register(t1);
 ByteAddressBuffer BoneIndices : register(t2);
 ByteAddressBuffer BoneWeights : register(t3);
 ByteAddressBuffer SkinTransforms : register(t4);
 RWByteAddressBuffer SkinnedPositions : register(u0);
 RWByteAddressBuffer SkinnedNormals : register(u1);

 cbuffer SkinningConstants : register(b0)
 {
     int VertexCount;
     int SkinInfluenceCount;
     int SkinningMode;
     int Padding;
 };

 static const int SkinningModeLinear = 0;
 static const int SkinningModeDualQuaternion = 1;
 static const uint FloatSize = 4;
 static const uint Float3Size = 12;
 static const uint MatrixRowCount = 4;
 static const uint MatrixRowSize = 16;

 float LoadFloat(ByteAddressBuffer buffer, uint elementIndex)
 {
     return asfloat(buffer.Load(elementIndex * FloatSize));
 }

 uint LoadUInt(ByteAddressBuffer buffer, uint elementIndex)
 {
     return buffer.Load(elementIndex * FloatSize);
 }

 float3 LoadFloat3(ByteAddressBuffer buffer, uint elementIndex)
 {
     return asfloat(buffer.Load3(elementIndex * Float3Size));
 }

 float4 LoadFloat4(ByteAddressBuffer buffer, uint elementIndex)
 {
     return asfloat(buffer.Load4(elementIndex * MatrixRowSize));
 }

 float4x4 LoadTransform(uint boneIndex)
 {
     uint firstRow = boneIndex * MatrixRowCount;
     return float4x4(
         LoadFloat4(SkinTransforms, firstRow),
         LoadFloat4(SkinTransforms, firstRow + 1),
         LoadFloat4(SkinTransforms, firstRow + 2),
         LoadFloat4(SkinTransforms, firstRow + 3));
 }

 void StoreFloat3(RWByteAddressBuffer buffer, uint elementIndex, float3 value)
 {
     buffer.Store3(elementIndex * Float3Size, asuint(value));
 }

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

 [numthreads(64, 1, 1)]
 void Main(uint3 dispatchThreadId : SV_DispatchThreadID)
 {
     uint vertexIndex = dispatchThreadId.x;
     if (vertexIndex >= (uint)VertexCount)
     {
         return;
     }

    float3 sourcePosition = LoadFloat3(Positions, vertexIndex);
    float3 sourceNormal = LoadFloat3(Normals, vertexIndex);
     float3 outputPosition = float3(0.0f, 0.0f, 0.0f);
     float3 outputNormal = float3(0.0f, 0.0f, 0.0f);
     float totalWeight = 0.0f;

     float4 blendedRotationQuaternion = float4(0.0f, 0.0f, 0.0f, 0.0f);
     float4 blendedDualQuaternion = float4(0.0f, 0.0f, 0.0f, 0.0f);
     bool hasQuaternionReference = false;
     float4 quaternionReference = float4(0.0f, 0.0f, 0.0f, 0.0f);

     uint baseOffset = vertexIndex * (uint)max(SkinInfluenceCount, 0);
     for (int influenceIndex = 0; influenceIndex < SkinInfluenceCount; influenceIndex++)
     {
         uint packedOffset = baseOffset + (uint)influenceIndex;
         float weight = LoadFloat(BoneWeights, packedOffset);
         if (weight <= 0.0f)
         {
             continue;
         }

         uint boneIndex = LoadUInt(BoneIndices, packedOffset);
         float4x4 transform = LoadTransform(boneIndex);

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

     if (totalWeight > 0.0f)
     {
         if (SkinningMode == SkinningModeDualQuaternion)
         {
             float quaternionLength = length(blendedRotationQuaternion);
             if (quaternionLength > 1e-8f)
             {
                 float4 normalizedRotationQuaternion = blendedRotationQuaternion / quaternionLength;
                 float4 normalizedDualQuaternion = blendedDualQuaternion / quaternionLength;
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

     float normalLengthSquared = dot(outputNormal, outputNormal);
     float3 normalizedNormal = normalLengthSquared > 1e-12f ? normalize(outputNormal) : float3(0.0f, 1.0f, 0.0f);

     StoreFloat3(SkinnedPositions, vertexIndex, outputPosition);
     StoreFloat3(SkinnedNormals, vertexIndex, normalizedNormal);
 }