layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec3 aNormal;
layout (location = 2) in vec2 aTexCoord;

uniform mat4 uModel;
uniform mat4 uScene;
uniform mat4 uView;
uniform mat4 uProjection;
uniform vec3 uCameraPos;
uniform vec3 uCameraWorldPos;
uniform mat3 uNormalMatrix;
uniform mat3 uSceneNormalMatrix;
uniform bool uHasNormals;

uniform mat4 uLightSpaceMatrix;
uniform bool uEnableShadows;

out vec3 vWorldPos;
out vec3 vCameraRelativePos;
noperspective out vec3 vCameraRelativePosLinear;
out vec3 vViewPos;
out vec3 vNormal;
out vec3 vViewNormal;
out vec2 vTexCoord;
flat out int vHasNormals;
out float vClipW;
out vec4 vLightSpacePos;

void main()
{
    vec4 localPos = vec4(aPosition, 1.0);
    mat4 worldMatrix = uModel;
    worldMatrix[3].xyz -= uCameraWorldPos;
    vec4 worldPos = worldMatrix * localPos;
    vec3 worldNormal = uHasNormals ? (uNormalMatrix * aNormal) : vec3(0.0, 1.0, 0.0);

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
    vViewPos = (mat3(uView) * sceneCameraRelativePos);
    vViewNormal = mat3(uView) * vNormal;
    gl_Position = uProjection * vec4(vViewPos, 1.0);
    vClipW = gl_Position.w;

    if (uEnableShadows)
        vLightSpacePos = uLightSpaceMatrix * vec4(vWorldPos, 1.0);
    else
        vLightSpacePos = vec4(0.0);
}
