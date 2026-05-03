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
     int PositionElementType;
     int PositionElementStrideBytes;
     int PositionValueStrideBytes;
     int NormalElementType;
     int NormalElementStrideBytes;
     int NormalValueStrideBytes;
     int BoneIndexElementType;
     int BoneIndexElementStrideBytes;
     int BoneIndexValueStrideBytes;
     int BoneWeightElementType;
     int BoneWeightElementStrideBytes;
     int BoneWeightValueStrideBytes;
 };

 static const uint BufferSelectorPosition = 0;
 static const uint BufferSelectorNormal = 1;
 static const uint BufferSelectorBoneIndex = 2;
 static const uint BufferSelectorBoneWeight = 3;
 static const int ElementTypeUnknown = 0;
 static const int ElementTypeFloat16 = 1;
 static const int ElementTypeFloat32 = 2;
 static const int ElementTypeFloat64 = 3;
 static const int ElementTypeInt8 = 4;
 static const int ElementTypeUInt8 = 5;
 static const int ElementTypeInt16 = 6;
 static const int ElementTypeUInt16 = 7;
 static const int ElementTypeInt32 = 8;
 static const int ElementTypeUInt32 = 9;
 static const int ElementTypeInt64 = 10;
 static const int ElementTypeUInt64 = 11;
 static const int SkinningModeLinear = 0;
 static const int SkinningModeDualQuaternion = 1;
 static const uint Float3Size = 12;
 static const uint MatrixRowCount = 4;
 static const uint MatrixRowSize = 16;
 static const double UInt32Scale = 4294967296.0;

 uint LoadBufferWord(uint bufferSelector, uint byteOffset)
 {
     switch (bufferSelector)
     {
         case BufferSelectorPosition:
             return Positions.Load(byteOffset);
         case BufferSelectorNormal:
             return Normals.Load(byteOffset);
         case BufferSelectorBoneIndex:
             return BoneIndices.Load(byteOffset);
         case BufferSelectorBoneWeight:
             return BoneWeights.Load(byteOffset);
         default:
             return 0;
     }
 }

 int GetElementTypeSizeBytes(int elementType)
 {
     switch (elementType)
     {
         case ElementTypeFloat16:
         case ElementTypeInt16:
         case ElementTypeUInt16:
             return 2;
         case ElementTypeFloat32:
         case ElementTypeInt32:
         case ElementTypeUInt32:
             return 4;
         case ElementTypeFloat64:
         case ElementTypeInt64:
         case ElementTypeUInt64:
             return 8;
         case ElementTypeInt8:
         case ElementTypeUInt8:
             return 1;
         default:
             return 0;
     }
 }

 float4 LoadFloat4(ByteAddressBuffer buffer, uint elementIndex)
 {
     return asfloat(buffer.Load4(elementIndex * MatrixRowSize));
 }

 float ConvertSignedUInt32ToFloat(uint raw)
 {
     return raw >= 0x80000000u
         ? (float)((double)raw - UInt32Scale)
         : (float)raw;
 }

 float ConvertSignedUInt64ToFloat(uint lowWord, uint highWord)
 {
     double highValue = highWord >= 0x80000000u
         ? (double)highWord - UInt32Scale
         : (double)highWord;
     return (float)((double)lowWord + (highValue * UInt32Scale));
 }

 float ConvertUnsignedUInt64ToFloat(uint lowWord, uint highWord)
 {
     return (float)((double)lowWord + ((double)highWord * UInt32Scale));
 }

 float LoadComponentAsFloat(uint bufferSelector, int elementType, uint byteOffset)
 {
     uint alignedByteOffset = byteOffset & ~3u;
     uint word = LoadBufferWord(bufferSelector, alignedByteOffset);
     uint shift = (byteOffset & 3u) * 8u;

     switch (elementType)
     {
         case ElementTypeFloat16:
         {
             uint raw = (byteOffset & 2u) == 0u ? (word & 0xFFFFu) : (word >> 16);
             return f16tof32(raw);
         }
         case ElementTypeFloat32:
             return asfloat(word);
         case ElementTypeFloat64:
         {
             uint highWord = LoadBufferWord(bufferSelector, alignedByteOffset + 4u);
             return (float)asdouble(word, highWord);
         }
         case ElementTypeInt8:
         {
             uint raw = (word >> shift) & 0xFFu;
             return raw >= 0x80u ? (float)((int)raw - 256) : (float)raw;
         }
         case ElementTypeUInt8:
             return (float)((word >> shift) & 0xFFu);
         case ElementTypeInt16:
         {
             uint raw = (word >> shift) & 0xFFFFu;
             return raw >= 0x8000u ? (float)((int)raw - 65536) : (float)raw;
         }
         case ElementTypeUInt16:
             return (float)((word >> shift) & 0xFFFFu);
         case ElementTypeInt32:
             return ConvertSignedUInt32ToFloat(word);
         case ElementTypeUInt32:
             return (float)word;
         case ElementTypeInt64:
         {
             uint highWord = LoadBufferWord(bufferSelector, alignedByteOffset + 4u);
             return ConvertSignedUInt64ToFloat(word, highWord);
         }
         case ElementTypeUInt64:
         {
             uint highWord = LoadBufferWord(bufferSelector, alignedByteOffset + 4u);
             return ConvertUnsignedUInt64ToFloat(word, highWord);
         }
         default:
             return 0.0f;
     }
 }

 uint LoadComponentAsUInt(uint bufferSelector, int elementType, uint byteOffset)
 {
     uint alignedByteOffset = byteOffset & ~3u;
     uint word = LoadBufferWord(bufferSelector, alignedByteOffset);
     uint shift = (byteOffset & 3u) * 8u;

     switch (elementType)
     {
         case ElementTypeUInt8:
             return (word >> shift) & 0xFFu;
         case ElementTypeUInt16:
             return (word >> shift) & 0xFFFFu;
         case ElementTypeUInt32:
             return word;
         case ElementTypeInt8:
         {
             uint raw = (word >> shift) & 0xFFu;
             return raw >= 0x80u ? 0u : raw;
         }
         case ElementTypeInt16:
         {
             uint raw = (word >> shift) & 0xFFFFu;
             return raw >= 0x8000u ? 0u : raw;
         }
         case ElementTypeInt32:
             return word >= 0x80000000u ? 0u : word;
         case ElementTypeUInt64:
         {
             uint highWord = LoadBufferWord(bufferSelector, alignedByteOffset + 4u);
             return highWord == 0u ? word : 0xFFFFFFFFu;
         }
         case ElementTypeInt64:
         {
             uint highWord = LoadBufferWord(bufferSelector, alignedByteOffset + 4u);
             return highWord >= 0x80000000u ? 0u : (highWord == 0u ? word : 0xFFFFFFFFu);
         }
         default:
             return (uint)max(LoadComponentAsFloat(bufferSelector, elementType, byteOffset), 0.0f);
     }
 }

 float LoadValueComponentAsFloat(uint bufferSelector, int elementType, int elementStrideBytes, int valueStrideBytes, uint elementIndex, uint valueIndex, uint componentIndex)
 {
     int componentSizeBytes = GetElementTypeSizeBytes(elementType);
     uint byteOffset = (elementIndex * (uint)elementStrideBytes)
         + (valueIndex * (uint)valueStrideBytes)
         + (componentIndex * (uint)componentSizeBytes);
     return LoadComponentAsFloat(bufferSelector, elementType, byteOffset);
 }

 uint LoadValueComponentAsUInt(uint bufferSelector, int elementType, int elementStrideBytes, int valueStrideBytes, uint elementIndex, uint valueIndex, uint componentIndex)
 {
     int componentSizeBytes = GetElementTypeSizeBytes(elementType);
     uint byteOffset = (elementIndex * (uint)elementStrideBytes)
         + (valueIndex * (uint)valueStrideBytes)
         + (componentIndex * (uint)componentSizeBytes);
     return LoadComponentAsUInt(bufferSelector, elementType, byteOffset);
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

    float3 sourcePosition = float3(
        LoadValueComponentAsFloat(BufferSelectorPosition, PositionElementType, PositionElementStrideBytes, PositionValueStrideBytes, vertexIndex, 0u, 0u),
        LoadValueComponentAsFloat(BufferSelectorPosition, PositionElementType, PositionElementStrideBytes, PositionValueStrideBytes, vertexIndex, 0u, 1u),
        LoadValueComponentAsFloat(BufferSelectorPosition, PositionElementType, PositionElementStrideBytes, PositionValueStrideBytes, vertexIndex, 0u, 2u));
    float3 sourceNormal = float3(
        LoadValueComponentAsFloat(BufferSelectorNormal, NormalElementType, NormalElementStrideBytes, NormalValueStrideBytes, vertexIndex, 0u, 0u),
        LoadValueComponentAsFloat(BufferSelectorNormal, NormalElementType, NormalElementStrideBytes, NormalValueStrideBytes, vertexIndex, 0u, 1u),
        LoadValueComponentAsFloat(BufferSelectorNormal, NormalElementType, NormalElementStrideBytes, NormalValueStrideBytes, vertexIndex, 0u, 2u));
     float3 outputPosition = float3(0.0f, 0.0f, 0.0f);
     float3 outputNormal = float3(0.0f, 0.0f, 0.0f);
     float totalWeight = 0.0f;

     float4 blendedRotationQuaternion = float4(0.0f, 0.0f, 0.0f, 0.0f);
     float4 blendedDualQuaternion = float4(0.0f, 0.0f, 0.0f, 0.0f);
     bool hasQuaternionReference = false;
     float4 quaternionReference = float4(0.0f, 0.0f, 0.0f, 0.0f);

     for (int influenceIndex = 0; influenceIndex < SkinInfluenceCount; influenceIndex++)
     {
         float weight = LoadValueComponentAsFloat(BufferSelectorBoneWeight, BoneWeightElementType, BoneWeightElementStrideBytes, BoneWeightValueStrideBytes, vertexIndex, (uint)influenceIndex, 0u);
         if (weight <= 0.0f)
         {
             continue;
         }

         uint boneIndex = LoadValueComponentAsUInt(BufferSelectorBoneIndex, BoneIndexElementType, BoneIndexElementStrideBytes, BoneIndexValueStrideBytes, vertexIndex, (uint)influenceIndex, 0u);
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