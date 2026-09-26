using CastNet.Nodes;
using System.Numerics;

namespace RedFox.Graphics3D.Formats.Cast;

internal static class CastMaterialTranslator
{
    private static readonly (string Slot, Func<Material, string?> GetMapName, Action<Material, string> SetMapName, Func<Material, Vector4?> GetColor, Action<Material, Vector4> SetColor)[] Slots =
    [
        ("diffuse", material => material.DiffuseMapName, (material, name) => material.DiffuseMapName = name, material => material.DiffuseColor, (material, color) => material.DiffuseColor = color),
        ("albedo", material => material.DiffuseMapName, (material, name) => material.DiffuseMapName ??= name, material => null, (material, color) => material.DiffuseColor ??= color),
        ("normal", material => material.NormalMapName, (material, name) => material.NormalMapName = name, material => null, (material, color) => { }),
        ("specular", material => material.SpecularMapName, (material, name) => material.SpecularMapName = name, material => material.SpecularColor, (material, color) => material.SpecularColor = color),
        ("metal", material => material.MetallicMapName, (material, name) => material.MetallicMapName = name, material => material.MetallicColor, (material, color) => material.MetallicColor = color),
        ("gloss", material => material.GlossMapName, (material, name) => material.GlossMapName = name, material => material.GlossColor, (material, color) => material.GlossColor = color),
        ("roughness", material => material.RoughnessMapName, (material, name) => material.RoughnessMapName = name, material => material.RoughnessColor, (material, color) => material.RoughnessColor = color),
        ("emissive", material => material.EmissiveMapName, (material, name) => material.EmissiveMapName = name, material => material.EmissiveColor, (material, color) => material.EmissiveColor = color),
        ("emask", material => material.EmissiveMaskMapName, (material, name) => material.EmissiveMaskMapName = name, material => null, (material, color) => { }),
        ("ao", material => material.AmbientOcclusionMapName, (material, name) => material.AmbientOcclusionMapName = name, material => material.AmbientOcclusionColor, (material, color) => material.AmbientOcclusionColor = color),
        ("cavity", material => material.CavityMapName, (material, name) => material.CavityMapName = name, material => material.CavityColor, (material, color) => material.CavityColor = color),
        ("aniso", material => material.AnisotropyMapName, (material, name) => material.AnisotropyMapName = name, material => material.AnisotropyColor, (material, color) => material.AnisotropyColor = color),
    ];

    public static Material Read(SceneNode model, MaterialNode materialNode, string? sourceDirectory)
    {
        var material = model.AddNode<Material>(materialNode.Name);

        material.Type = materialNode.Type;

        foreach (var (slot, node) in materialNode.EnumerateSlots())
        {
            var mapping = Array.Find(Slots, candidate => candidate.Slot == slot);

            if (node is FileNode { Path.Length: > 0 } file)
            {
                if (!material.TryFindChild(file.Path, out Texture? texture))
                {
                    texture = material.AddNode(new Texture(file.Path));
                    texture.ResolveFilePath(sourceDirectory);
                }

                material.Connect(slot, texture);
                mapping.SetMapName?.Invoke(material, slot);
            }
            else if (node is ColorNode color)
            {
                mapping.SetColor?.Invoke(material, color.Rgba);
            }
        }

        return material;
    }

    public static MaterialNode Write(ModelNode modelNode, Material material, string? targetDirectory)
    {
        var materialNode = modelNode.AddNode(new MaterialNode { Name = material.Name, Type = material.Type ?? "pbr" });
        var extraIndex = 0;

        foreach (var binding in material.Textures)
        {
            var slot = Array.Find(Slots, candidate => candidate.Slot == binding.SamplerUniform).Slot ?? Array.Find(Slots, candidate => candidate.GetMapName(material) == binding.SamplerUniform).Slot ?? $"extra{extraIndex++}";

            if (materialNode.GetSlot(slot) is null)
                materialNode.SetSlot(slot, new FileNode { Path = binding.Texture.GetPortableFilePath(targetDirectory) });
        }

        foreach (var (slot, _, _, getColor, _) in Slots)
        {
            if (getColor(material) is Vector4 color && materialNode.GetSlot(slot) is null)
                materialNode.SetSlot(slot, new ColorNode { Rgba = color });
        }

        return materialNode;
    }
}
