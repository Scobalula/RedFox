layout (location = 0) in vec3 aPosition;
layout (location = 3) in ivec2 aInfluenceRange;

uniform mat4 uModel;
uniform mat4 uScene;
uniform mat4 uView;
uniform mat4 uProjection;
uniform vec3 uCameraWorldPos;
uniform bool uHasSkinning;
uniform sampler2D uInfluenceTexture;
uniform vec2 uInfluenceTextureSize;
uniform sampler2D uBoneMatrixTexture;
uniform vec2 uBoneMatrixTextureSize;

ivec2 getTexelCoord(int index, vec2 sizeValue)
{
    int width = max(int(sizeValue.x), 1);
    return ivec2(index % width, index / width);
}

vec4 fetchData(sampler2D textureHandle, vec2 sizeValue, int index)
{
    return texelFetch(textureHandle, getTexelCoord(index, sizeValue), 0);
}

mat4 fetchBoneMatrix(int boneIndex)
{
    int texelBase = boneIndex * 4;
    vec4 row0 = fetchData(uBoneMatrixTexture, uBoneMatrixTextureSize, texelBase + 0);
    vec4 row1 = fetchData(uBoneMatrixTexture, uBoneMatrixTextureSize, texelBase + 1);
    vec4 row2 = fetchData(uBoneMatrixTexture, uBoneMatrixTextureSize, texelBase + 2);
    vec4 row3 = fetchData(uBoneMatrixTexture, uBoneMatrixTextureSize, texelBase + 3);
    return mat4(row0, row1, row2, row3);
}

void main()
{
    vec4 localPos = vec4(aPosition, 1.0);
    mat4 worldMatrix = uModel;
    worldMatrix[3].xyz -= uCameraWorldPos;
    vec4 worldPos = worldMatrix * localPos;

    if (uHasSkinning)
    {
        vec4 skinnedWorldPos = vec4(0.0);
        float totalWeight = 0.0;

        for (int influenceOffset = 0; influenceOffset < aInfluenceRange.y; influenceOffset++)
        {
            vec4 influence = fetchData(uInfluenceTexture, uInfluenceTextureSize, aInfluenceRange.x + influenceOffset);
            float weight = influence.y;
            if (weight <= 0.0)
                continue;

            int boneIndex = int(influence.x + 0.5);
            mat4 boneMatrix = fetchBoneMatrix(boneIndex);
            boneMatrix[3].xyz -= uCameraWorldPos;
            skinnedWorldPos += (boneMatrix * localPos) * weight;
            totalWeight += weight;
        }

        if (totalWeight > 0.0)
            worldPos = skinnedWorldPos;
    }

    vec3 sceneCameraRelativePos = (uScene * vec4(worldPos.xyz, 0.0)).xyz;
    mat4 viewRotation = mat4(mat3(uView));
    gl_Position = uProjection * viewRotation * vec4(sceneCameraRelativePos, 1.0);
}
