layout (location = 0) in float aBoneIndex;
layout (location = 1) in vec3  aLocalOffset;
layout (location = 2) in vec4  aColor;

uniform mat4 uView;
uniform mat4 uProjection;
uniform mat4 uScene;
uniform float uAxisScale;
uniform sampler2D uBoneWorldMatrixTexture;
uniform vec2 uBoneWorldMatrixTextureSize;

out vec4 vColor;

ivec2 getTexelCoord(int index, vec2 sizeValue)
{
    int width = max(int(sizeValue.x), 1);
    return ivec2(index % width, index / width);
}

mat4 fetchBoneWorldMatrix(int boneIndex)
{
    int base = boneIndex * 4;
    vec4 r0 = texelFetch(uBoneWorldMatrixTexture, getTexelCoord(base + 0, uBoneWorldMatrixTextureSize), 0);
    vec4 r1 = texelFetch(uBoneWorldMatrixTexture, getTexelCoord(base + 1, uBoneWorldMatrixTextureSize), 0);
    vec4 r2 = texelFetch(uBoneWorldMatrixTexture, getTexelCoord(base + 2, uBoneWorldMatrixTextureSize), 0);
    vec4 r3 = texelFetch(uBoneWorldMatrixTexture, getTexelCoord(base + 3, uBoneWorldMatrixTextureSize), 0);
    // C# Matrix4x4 rows stored as GLSL columns — matches Vector3.Transform convention.
    return mat4(r0, r1, r2, r3);
}

void main()
{
    int   boneIndex   = int(aBoneIndex + 0.5);
    mat4  boneWorld   = fetchBoneWorldMatrix(boneIndex);
    vec3  scaledOffset = aLocalOffset * uAxisScale;
    vec4  worldPos    = boneWorld * vec4(scaledOffset, 1.0);
    vec4  scenePos    = uScene * worldPos;
    gl_Position       = uProjection * uView * scenePos;
    vColor            = aColor;
}
