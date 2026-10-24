#version 300 es
precision highp float;
precision highp int;
precision highp sampler2D;

in vec3 WorldPosition;
in vec3 WorldNormal;
in vec2 TextureCoordinate;

#define MAX_LIGHTS 4
#define PI 3.14159265

uniform vec3 AmbientColor;
uniform int LightCount;
uniform vec4 LightDirectionsAndIntensity[MAX_LIGHTS];
uniform vec3 LightColors[MAX_LIGHTS];
uniform int EnvironmentLightCount;
uniform vec4 EnvironmentLightDirections[MAX_LIGHTS];
uniform vec3 EnvironmentLightPowers[MAX_LIGHTS];
uniform vec3 CameraPosition;
uniform int UseViewBasedLighting;
uniform int HasDiffuseMap;
uniform int UVLayerCount;
uniform vec4 BaseColor;
uniform float MaterialSpecularStrength;
uniform float MaterialSpecularPower;
uniform sampler2D DiffuseMap;
uniform int DepthPrepass;

out vec4 FragColor;

#include "Shadows.glsl"
#include "AmbientOcclusion.glsl"

void main()
{
    if (ShadowPass != 0 || DepthPrepass != 0)
    {
        FragColor = vec4(0.0);
        return;
    }

    vec3 normal = normalize(WorldNormal);

    if (!gl_FrontFacing)
    {
        normal = -normal;
    }

    vec3 viewDirection = normalize(CameraPosition - WorldPosition);
    vec3 ambient = AmbientColor * ScreenSpaceOcclusion();
    vec4 surfaceColor = BaseColor;

    if (HasDiffuseMap != 0 && UVLayerCount > 0)
    {
        surfaceColor *= texture(DiffuseMap, TextureCoordinate);
    }

    if (UseViewBasedLighting != 0)
    {
        float facing = max(dot(normal, viewDirection), 0.0);
        vec3 lit = (ambient + vec3(facing)) * surfaceColor.rgb;
        FragColor = vec4(lit, surfaceColor.a);
        return;
    }

    vec3 diffuse = vec3(0.0);
    vec3 specular = vec3(0.0);

    int count = clamp(LightCount, 0, MAX_LIGHTS);
    for (int i = 0; i < count; i++)
    {
        vec3 lightDirection = normalize(-LightDirectionsAndIntensity[i].xyz);
        float lightIntensity = LightDirectionsAndIntensity[i].w;
        float nDotL = max(dot(normal, lightDirection), 0.0);
        diffuse += LightColors[i] * (lightIntensity * nDotL);

        if (nDotL > 0.0)
        {
            vec3 reflected = reflect(-lightDirection, normal);
            float spec = pow(max(dot(viewDirection, reflected), 0.0), MaterialSpecularPower);
            specular += LightColors[i] * (lightIntensity * spec * MaterialSpecularStrength);
        }
    }

    int environmentCount = clamp(EnvironmentLightCount, 0, MAX_LIGHTS);
    for (int i = 0; i < environmentCount; i++)
    {
        vec3 lightDirection = EnvironmentLightDirections[i].xyz;
        float nDotL = max(dot(normal, lightDirection), 0.0);
        if (nDotL <= 0.0)
        {
            continue;
        }

        float visibility = i < ShadowCount ? ShadowVisibility(i, WorldPosition, normal) : 1.0;
        vec3 lightColor = EnvironmentLightPowers[i] / PI * visibility;
        vec3 reflected = reflect(-lightDirection, normal);
        float spec = pow(max(dot(viewDirection, reflected), 0.0), MaterialSpecularPower);
        diffuse += lightColor * nDotL;
        specular += lightColor * (spec * MaterialSpecularStrength);
    }

    vec3 lit = ((ambient + diffuse) * surfaceColor.rgb) + specular;
    FragColor = vec4(lit, surfaceColor.a);
}