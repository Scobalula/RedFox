layout (location = 0) in vec3 aPosition;

uniform mat4 uModel;
uniform mat4 uScene;
uniform mat4 uLightViewProjection;

void main()
{
    vec4 worldPos = uModel * vec4(aPosition, 1.0);
    vec3 scenePos = (uScene * worldPos).xyz;
    gl_Position = uLightViewProjection * vec4(scenePos, 1.0);
}
