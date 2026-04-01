in vec3 vWorldPos;
in vec3 vNormal;
in vec2 vTexCoord;
flat in int vHasNormals;

uniform vec3 uCameraPos;
uniform vec3 uLightDir;
uniform vec3 uLightColor;
uniform vec3 uSkyColor;
uniform vec3 uGroundColor;
uniform vec3 uSpecularColor;
uniform float uAmbientStrength;
uniform float uSpecularStrength;
uniform float uShininess;
uniform vec4 uDiffuseColor;
uniform bool uHasDiffuseTexture;
uniform sampler2D uDiffuseTexture;

out vec4 FragColor;

void main()
{
    vec4 texColor = uHasDiffuseTexture ? texture(uDiffuseTexture, vTexCoord) : vec4(1.0);
    vec4 baseColor = texColor * uDiffuseColor;

    vec3 normal = vHasNormals == 1
        ? normalize(vNormal)
        : normalize(cross(dFdx(vWorldPos), dFdy(vWorldPos)));
    vec3 sunDir = normalize(-uLightDir);
    float NdotL = max(dot(normal, sunDir), 0.0);
    vec3 direct = uLightColor * NdotL;

    float hemiFactor = clamp(normal.y * 0.5 + 0.5, 0.0, 1.0);
    vec3 indirect = mix(uGroundColor, uSkyColor, hemiFactor) * uAmbientStrength;

    vec3 viewDir = normalize(uCameraPos - vWorldPos);
    vec3 halfDir = normalize(sunDir + viewDir);
    float NdotH = max(dot(normal, halfDir), 0.0);
    vec3 specular = uSpecularColor * uLightColor * pow(NdotH, uShininess) * uSpecularStrength * NdotL;

    float fresnel = pow(1.0 - max(dot(normal, viewDir), 0.0), 5.0);
    vec3 skyRim = uSkyColor * fresnel * 0.08;

    vec3 finalColor = baseColor.rgb * (indirect + direct) + specular + skyRim;
    FragColor = vec4(finalColor, baseColor.a);
}
