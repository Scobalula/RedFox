using System.Numerics;
using RedFox.Graphics3D;
using RedFox.Graphics3D.OpenGL;

namespace RedFox.Tests.Graphics3D;

public sealed class PbrMaterialFactorsTests
{
    [Fact]
    public void Resolve_UsesScalarValuesWhenPresent()
    {
        Material material = new()
        {
            Metallic = 0.2f,
            Roughness = 0.9f,
            MetallicColor = new Vector4(0.8f, 0.8f, 0.8f, 1.0f),
            RoughnessColor = new Vector4(0.1f, 0.1f, 0.1f, 1.0f),
        };

        (float metallic, float roughness) = PbrMaterialFactors.Resolve(material);
        Assert.Equal(0.2f, metallic, 3);
        Assert.Equal(0.9f, roughness, 3);
    }

    [Fact]
    public void Resolve_UsesColorFactorWhenScalarUnset()
    {
        Material material = new()
        {
            Metallic = null,
            Roughness = null,
            MetallicColor = new Vector4(0.75f, 0.75f, 0.75f, 1.0f),
            RoughnessColor = new Vector4(0.4f, 0.4f, 0.4f, 1.0f),
        };

        (float metallic, float roughness) = PbrMaterialFactors.Resolve(material);
        Assert.Equal(0.75f, metallic, 3);
        Assert.Equal(0.4f, roughness, 3);
    }

    [Fact]
    public void Resolve_Defaults_WhenUnset()
    {
        (float metallic, float roughness) = PbrMaterialFactors.Resolve(material: null);
        Assert.Equal(0.0f, metallic, 3);
        Assert.Equal(0.5f, roughness, 3);
    }

    [Fact]
    public void Resolve_DefaultMaterial_UsesStableMaterialDefaults()
    {
        Material material = new();

        (float metallic, float roughness) = PbrMaterialFactors.Resolve(material);

        Assert.Equal(material.Metallic ?? 0.0f, metallic, 3);
        Assert.Equal(material.Roughness ?? 0.5f, roughness, 3);
    }

    [Fact]
    public void Resolve_UsesGlossColorWhenRoughnessIsUnset()
    {
        Material material = new()
        {
            Roughness = null,
            RoughnessColor = null,
            GlossColor = new Vector4(0.8f, 0.8f, 0.8f, 1.0f),
        };

        (float _, float roughness) = PbrMaterialFactors.Resolve(material);

        Assert.Equal(0.2f, roughness, 3);
    }
}
