// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using RedFox.Graphics3D;

namespace RedFox.Tests.Graphics3D;

/// <summary>
/// Unit tests for the Material connection API introduced in the SceneGraph-Connections plan.
/// </summary>
public sealed class MaterialConnectionTests
{
    [Fact]
    public void Connect_BumpsVersion()
    {
        Material material = new("mat");
        uint versionBefore = material.Version;

        material.DiffuseMapName = "diffuse";
        material.Connect("diffuse", new Texture("diffuse.tga"));

        Assert.NotEqual(versionBefore, material.Version);
    }

    [Fact]
    public void Disconnect_BumpsVersion()
    {
        Material material = new("mat");
        material.DiffuseMapName = "diffuse";
        material.Connect("diffuse", new Texture("diffuse.tga"));
        uint versionAfterConnect = material.Version;

        material.Disconnect("diffuse");

        Assert.NotEqual(versionAfterConnect, material.Version);
    }

    [Fact]
    public void Textures_ReturnsSameReferenceUntilMutation()
    {
        Material material = new("mat");
        IReadOnlyList<MaterialTextureBinding> first = material.Textures;
        IReadOnlyList<MaterialTextureBinding> second = material.Textures;

        Assert.Same(first, second);

        material.DiffuseMapName = "diffuse";
        material.Connect("diffuse", new Texture("diffuse.tga"));

        // After mutation the reference is still the same live list
        Assert.Same(first, material.Textures);
    }

    [Fact]
    public void Connect_Replace_PreservesNumericSlotAndUpdatesTexture()
    {
        Material material = new("mat");
        Texture tex1 = new("diffuse1.tga");
        Texture tex2 = new("diffuse2.tga");

        material.DiffuseMapName = "diffuse";
        material.Connect("diffuse", tex1);
        int slotAfterFirst = material.Textures[0].Slot;

        material.Connect("diffuse", tex2);

        Assert.Single(material.Textures);
        Assert.Same(tex2, material.Textures[0].Texture);
        Assert.Equal(slotAfterFirst, material.Textures[0].Slot);
    }

    [Fact]
    public void Disconnect_RemovesBinding()
    {
        Material material = new("mat");
        material.DiffuseMapName = "diffuse";
        material.Connect("diffuse", new Texture("diffuse.tga"));

        material.Disconnect("diffuse");

        Assert.Empty(material.Textures);
    }

    [Fact]
    public void Disconnect_UnknownKey_NoEffect()
    {
        Material material = new("mat");
        uint versionBefore = material.Version;

        material.Disconnect("nonexistent");

        Assert.Equal(versionBefore, material.Version);
    }

    [Fact]
    public void TryGetTexture_ReturnsConnectedTexture()
    {
        Material material = new("mat");
        Texture expected = new("diffuse.tga");
        material.DiffuseMapName = "diffuse";
        material.Connect("diffuse", expected);

        bool found = material.TryGetTexture("diffuse", out Texture? actual);

        Assert.True(found);
        Assert.Same(expected, actual);
    }

    [Fact]
    public void TryGetTexture_MissingKey_ReturnsFalse()
    {
        Material material = new("mat");

        bool found = material.TryGetTexture("diffuse", out Texture? texture);

        Assert.False(found);
        Assert.Null(texture);
    }

    [Fact]
    public void SharedTexture_ConnectedToTwoMaterialsUnderDifferentSlotKeys_ResolvesCorrectly()
    {
        Texture shared = new("albedo.tga");

        Material matA = new("matA");
        matA.DiffuseMapName = "diffuse";
        matA.Connect("diffuse", shared);

        Material matB = new("matB");
        matB.DiffuseMapName = "BaseMetalMap";
        matB.Connect("BaseMetalMap", shared);

        Assert.True(matA.TryGetTexture("diffuse", out Texture? fromA));
        Assert.True(matB.TryGetTexture("BaseMetalMap", out Texture? fromB));
        Assert.Same(shared, fromA);
        Assert.Same(shared, fromB);
        Assert.Single(matA.Textures);
        Assert.Single(matB.Textures);
    }

    [Fact]
    public void DiffuseMap_Getter_ReturnsConnectedTexture()
    {
        Material material = new("mat");
        Texture expected = new("diffuse.tga");
        material.DiffuseMapName = "diffuse";
        material.Connect("diffuse", expected);

        Assert.Same(expected, material.DiffuseMap);
    }

    [Fact]
    public void DiffuseMap_Setter_ConnectsViaSlotKey()
    {
        Material material = new("mat");
        Texture tex = new("diffuse.tga");
        material.DiffuseMapName = "diffuse";

        material.DiffuseMap = tex;

        Assert.Same(tex, material.DiffuseMap);
        Assert.Single(material.Textures);
    }

    [Fact]
    public void DiffuseMap_Setter_NullDisconnects()
    {
        Material material = new("mat");
        material.DiffuseMapName = "diffuse";
        material.Connect("diffuse", new Texture("diffuse.tga"));

        material.DiffuseMap = null;

        Assert.Empty(material.Textures);
    }

    [Fact]
    public void DiffuseMap_Setter_WithoutMapName_Throws()
    {
        Material material = new("mat");

        Assert.Throws<InvalidOperationException>(() => material.DiffuseMap = new Texture("diffuse.tga"));
    }

    [Fact]
    public void TryGetDiffuseMap_RoutesViaSlotKey()
    {
        Material material = new("mat");
        Texture expected = new("diffuse.tga");
        material.DiffuseMapName = "diffuse";
        material.Connect("diffuse", expected);

        bool found = material.TryGetDiffuseMap(out Texture? actual);

        Assert.True(found);
        Assert.Same(expected, actual);
    }

    [Fact]
    public void TryGetDiffuseMap_WhenMapNameNull_ReturnsFalse()
    {
        Material material = new("mat");

        bool found = material.TryGetDiffuseMap(out Texture? texture);

        Assert.False(found);
        Assert.Null(texture);
    }

    [Fact]
    public void GetDiffuseMap_WhenMapNameNull_Throws()
    {
        Material material = new("mat");

        Assert.Throws<InvalidOperationException>(() => material.GetDiffuseMap());
    }

    [Fact]
    public void GetDiffuseMap_WhenNotConnected_Throws()
    {
        Material material = new("mat") { DiffuseMapName = "diffuse" };

        Assert.Throws<KeyNotFoundException>(() => material.GetDiffuseMap());
    }

    [Fact]
    public void NumericSlotKey_AssignsExplicitNumericIndex()
    {
        Material material = new("mat");
        Texture tex = new("tex.tga");

        material.Connect("3", tex);

        Assert.Single(material.Textures);
        Assert.Equal(3, material.Textures[0].Slot);
        Assert.Equal("Texture3", material.Textures[0].SamplerUniform);
    }

    [Fact]
    public void NamedSlotKey_AssignsNextFreeNumericIndex()
    {
        Material material = new("mat");
        Texture diffuse = new("diffuse.tga");
        Texture normal = new("normal.tga");

        material.Connect("diffuse", diffuse);
        material.Connect("normal", normal);

        Assert.Equal(0, material.Textures[0].Slot);
        Assert.Equal(1, material.Textures[1].Slot);
        Assert.Equal("diffuse", material.Textures[0].SamplerUniform);
        Assert.Equal("normal", material.Textures[1].SamplerUniform);
    }
}
