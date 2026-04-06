layout(local_size_x = 64) in;

// Source vertex data (bind = 0, 1)
layout(std430, binding = 0) readonly buffer SrcPositions { float srcPositions[]; };
layout(std430, binding = 1) readonly buffer SrcNormals   { float srcNormals[];   };

// Destination vertex data (bind = 2, 3) — same GL buffers as the VBOs
layout(std430, binding = 2) writeonly buffer DstPositions { float dstPositions[]; };
layout(std430, binding = 3) writeonly buffer DstNormals   { float dstNormals[];   };

// Morph target deltas: [vertex * targetCount + target] * 3  (bind = 4, 5)
layout(std430, binding = 4) readonly buffer MorphPosDelta { float morphPosDeltas[]; };
layout(std430, binding = 5) readonly buffer MorphNrmDelta { float morphNrmDeltas[]; };

// Morph weights: one float per target (bind = 6)
layout(std430, binding = 6) readonly buffer MorphWeights { float morphWeights[]; };

// Bone matrices stored row-major so that (mat * v) matches the C# row-vector convention.
// Each matrix occupies 16 consecutive floats:  row0 row1 row2 row3.
// Read four vec4 columns → mat4 gives the correct column-major interpretation.
layout(std430, binding = 7) readonly buffer BoneMatrices { mat4 boneMatrices[]; };

// Per-influence pair: (boneIndex, weight) packed as vec2 (bind = 8)
layout(std430, binding = 8) readonly buffer Influences { vec2 influences[]; };

// Per-vertex (startOffset, count) into the Influences buffer (bind = 9)
layout(std430, binding = 9) readonly buffer InfluenceRanges { ivec2 influenceRanges[]; };

uniform int  uVertexCount;
uniform int  uMorphTargetCount;
uniform bool uHasMorphTargets;
uniform bool uHasSkinning;

void main()
{
    uint vid = gl_GlobalInvocationID.x;
    if (vid >= uint(uVertexCount))
        return;

    int base3 = int(vid) * 3;

    // Load source position and normal
    vec3 pos = vec3(srcPositions[base3], srcPositions[base3 + 1], srcPositions[base3 + 2]);
    vec3 nrm = vec3(srcNormals[base3],  srcNormals[base3 + 1],  srcNormals[base3 + 2]);

    // --- Morph targets ---
    if (uHasMorphTargets)
    {
        for (int t = 0; t < uMorphTargetCount; t++)
        {
            float w = morphWeights[t];
            if (abs(w) < 1e-6)
                continue;

            int dBase = (int(vid) * uMorphTargetCount + t) * 3;
            pos += vec3(morphPosDeltas[dBase], morphPosDeltas[dBase + 1], morphPosDeltas[dBase + 2]) * w;
            nrm += vec3(morphNrmDeltas[dBase], morphNrmDeltas[dBase + 1], morphNrmDeltas[dBase + 2]) * w;
        }
    }

    // --- Skeletal skinning ---
    if (uHasSkinning)
    {
        ivec2 range = influenceRanges[vid];
        int start = range.x;
        int count = range.y;

        vec3 skinnedPos = vec3(0.0);
        vec3 skinnedNrm = vec3(0.0);
        float totalWeight = 0.0;

        for (int i = 0; i < count; i++)
        {
            vec2  inf       = influences[start + i];
            int   boneIndex = int(inf.x + 0.5);
            float weight    = inf.y;
            if (weight <= 0.0)
                continue;

            mat4 boneMat = boneMatrices[boneIndex];
            skinnedPos  += (boneMat * vec4(pos, 1.0)).xyz * weight;
            skinnedNrm  += (mat3(boneMat) * nrm)          * weight;
            totalWeight += weight;
        }

        if (totalWeight > 0.0)
        {
            pos = skinnedPos;
            nrm = skinnedNrm;
        }
    }

    // Normalise the deformed normal
    float nrmLen = length(nrm);
    if (nrmLen > 1e-6)
        nrm /= nrmLen;

    // Write output
    dstPositions[base3]     = pos.x;
    dstPositions[base3 + 1] = pos.y;
    dstPositions[base3 + 2] = pos.z;

    dstNormals[base3]     = nrm.x;
    dstNormals[base3 + 1] = nrm.y;
    dstNormals[base3 + 2] = nrm.z;
}
