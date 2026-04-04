layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec3 aNormal;
layout (location = 2) in vec2 aTexCoord;
layout (location = 3) in ivec2 aInfluenceRange;

uniform mat4 uModel;
uniform mat4 uScene;
uniform mat4 uView;
uniform mat4 uProjection;
uniform vec3 uCameraPos;
uniform vec3 uCameraWorldPos;
uniform mat3 uNormalMatrix;
uniform mat3 uSceneNormalMatrix;
uniform bool uHasNormals;
uniform bool uHasSkinning;
uniform sampler2D uInfluenceTexture;
uniform vec2 uInfluenceTextureSize;
uniform sampler2D uBoneMatrixTexture;
uniform vec2 uBoneMatrixTextureSize;

out vec3 vWorldPos;
out vec3 vCameraRelativePos;
noperspective out vec3 vCameraRelativePosLinear;
out vec3 vNormal;
out vec2 vTexCoord;
flat out int vHasNormals;

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

mat3 computeNormalMatrix(mat4 matrixValue)
{
    return transpose(inverse(mat3(matrixValue)));
}

void main()
{
    vec4 localPos = vec4(aPosition, 1.0);
    mat4 worldMatrix = uModel;
    worldMatrix[3].xyz -= uCameraWorldPos;
    vec4 worldPos = worldMatrix * localPos;
    vec3 worldNormal = uHasNormals ? (uNormalMatrix * aNormal) : vec3(0.0, 1.0, 0.0);

    if (uHasSkinning)
    {
        vec4 skinnedWorldPos = vec4(0.0);
        vec3 skinnedWorldNormal = vec3(0.0);
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

            if (uHasNormals)
                skinnedWorldNormal += (computeNormalMatrix(boneMatrix) * aNormal) * weight;

            totalWeight += weight;
        }

        if (totalWeight > 0.0)
        {
            worldPos = skinnedWorldPos;
            if (uHasNormals)
                worldNormal = skinnedWorldNormal;
        }
    }

    vec3 sceneCameraRelativePos = (uScene * vec4(worldPos.xyz, 0.0)).xyz;
    vWorldPos = sceneCameraRelativePos + uCameraPos;
    vCameraRelativePos = sceneCameraRelativePos;
    vCameraRelativePosLinear = sceneCameraRelativePos;

    if (uHasNormals)
    {
        vec3 sceneNormal = uSceneNormalMatrix * worldNormal;
        float sceneNormalLength = length(sceneNormal);
        vNormal = sceneNormalLength > 0.0
            ? sceneNormal / sceneNormalLength
            : vec3(0.0, 1.0, 0.0);
    }
    else
    {
        vNormal = vec3(0.0, 1.0, 0.0);
    }

    vTexCoord = aTexCoord;
    vHasNormals = uHasNormals ? 1 : 0;
    mat4 viewRotation = mat4(mat3(uView));
    gl_Position = uProjection * viewRotation * vec4(sceneCameraRelativePos, 1.0);
}
