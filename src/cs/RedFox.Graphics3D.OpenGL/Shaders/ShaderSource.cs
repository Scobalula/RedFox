namespace RedFox.Graphics3D.OpenGL.Shaders;

public static class ShaderSource
{
    public const string MeshVertex = """
        #version 330 core

        layout (location = 0) in vec3 aPosition;
        layout (location = 1) in vec3 aNormal;
        layout (location = 2) in vec2 aTexCoord;
        layout (location = 3) in ivec4 aBoneIndices;
        layout (location = 4) in vec4 aBoneWeights;

        uniform mat4 uModel;
        uniform mat4 uView;
        uniform mat4 uProjection;
        uniform mat3 uNormalMatrix;
        uniform bool uHasSkinning;
        uniform mat4 uBoneMatrices[128];

        out vec3 vWorldPos;
        out vec3 vNormal;
        out vec2 vTexCoord;

        void main()
        {
            vec4 pos = vec4(aPosition, 1.0);
            vec4 norm = vec4(aNormal, 0.0);

            if (uHasSkinning)
            {
                mat4 skinMatrix =
                    aBoneWeights.x * uBoneMatrices[aBoneIndices.x] +
                    aBoneWeights.y * uBoneMatrices[aBoneIndices.y] +
                    aBoneWeights.z * uBoneMatrices[aBoneIndices.z] +
                    aBoneWeights.w * uBoneMatrices[aBoneIndices.w];

                pos = skinMatrix * pos;
                norm = skinMatrix * norm;
            }

            vec4 worldPos = uModel * pos;
            vWorldPos = worldPos.xyz;
            vNormal = normalize(uNormalMatrix * norm.xyz);
            vTexCoord = aTexCoord;

            gl_Position = uProjection * uView * worldPos;
        }
        """;

    public const string MeshFragment = """
        #version 330 core

        in vec3 vWorldPos;
        in vec3 vNormal;
        in vec2 vTexCoord;

        uniform vec3 uLightDir;
        uniform vec3 uLightColor;
        uniform vec3 uAmbientColor;
        uniform vec4 uDiffuseColor;
        uniform bool uHasDiffuseTexture;
        uniform sampler2D uDiffuseTexture;

        out vec4 FragColor;

        void main()
        {
            vec4 texColor = uHasDiffuseTexture ? texture(uDiffuseTexture, vTexCoord) : vec4(1.0);
            vec4 baseColor = texColor * uDiffuseColor;

            vec3 normal = normalize(vNormal);
            float NdotL = max(dot(normal, normalize(-uLightDir)), 0.0);
            vec3 diffuse = uLightColor * NdotL;

            vec3 viewDir = normalize(-vWorldPos);
            vec3 halfDir = normalize(normalize(-uLightDir) + viewDir);
            float NdotH = max(dot(normal, halfDir), 0.0);
            vec3 specular = uLightColor * pow(NdotH, 32.0) * 0.3;

            vec3 finalColor = baseColor.rgb * (uAmbientColor + diffuse) + specular;
            FragColor = vec4(finalColor, baseColor.a);
        }
        """;

    public const string LineVertex = """
        #version 330 core

        layout (location = 0) in vec3 aPosition;

        uniform mat4 uViewProjection;
        uniform mat4 uModel;

        void main()
        {
            gl_Position = uViewProjection * uModel * vec4(aPosition, 1.0);
        }
        """;

    public const string LineFragment = """
        #version 330 core

        uniform vec4 uLineColor;

        out vec4 FragColor;

        void main()
        {
            FragColor = uLineColor;
        }
        """;
}
