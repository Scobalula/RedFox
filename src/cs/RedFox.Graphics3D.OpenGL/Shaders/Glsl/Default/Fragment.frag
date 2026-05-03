#version 300 es
precision highp float;
precision highp int;

in vec3 WorldPosition;
in vec3 WorldNormal;

#define MAX_LIGHTS 4

uniform vec3 AmbientColor;
uniform int LightCount;
uniform vec4 LightDirectionsAndIntensity[MAX_LIGHTS];
uniform vec3 LightColors[MAX_LIGHTS];
uniform vec3 CameraPosition;
uniform int UseViewBasedLighting;
uniform vec4 BaseColor;
uniform float MaterialSpecularStrength;
uniform float MaterialSpecularPower;

out vec4 FragColor;

void main()
{
    vec3 normal = normalize(WorldNormal);
    vec3 viewDirection = normalize(CameraPosition - WorldPosition);
    vec3 ambient = AmbientColor;

    if (UseViewBasedLighting != 0)
    {
        float facing = max(dot(normal, viewDirection), 0.0);
        vec3 lit = (ambient + vec3(facing)) * BaseColor.rgb;
        FragColor = vec4(lit, BaseColor.a);
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

    vec3 lit = ((ambient + diffuse) * BaseColor.rgb) + specular;
    FragColor = vec4(lit, BaseColor.a);
}