using System.Numerics;

namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Imports FBX surface materials, their PBR extensions and connected file textures.
/// </summary>
public static class FbxMaterialImporter
{
    private static readonly string[] BaseColorProperties	= ["Maya|base_color", "Maya|baseColor", "3dsMax|Parameters|base_color", "3dsMax|main|basecolor"];
    private static readonly string[] MetallicProperties		= ["Maya|metallic", "Maya|metalness", "3dsMax|Parameters|metalness", "3dsMax|main|metalness"];
    private static readonly string[] RoughnessProperties	= ["Maya|roughness", "Maya|specularRoughness", "3dsMax|Parameters|roughness", "3dsMax|main|roughness"];

    /// <summary>
    /// Imports every material in the document into the scene root.
    /// </summary>
    /// <param name="importer">The active scene importer.</param>
    /// <returns>The imported materials keyed by FBX object identifier.</returns>
    public static Dictionary<long, Material> Import(FbxSceneImporter importer)
    {
        Dictionary<long, Material> materials = [];

        foreach (FbxObject fbxMaterial in importer.Graph.Objects)
        {
            if (fbxMaterial.ClassName != "Material")
                continue;

            Material material = new(importer.GetUniqueName(fbxMaterial.Name));

            ApplyProperties(material, fbxMaterial);
            material.SetParentUnsafe(importer.Scene);
            materials[fbxMaterial.Id] = material;

            foreach (FbxConnection connection in importer.Graph.GetChildConnections(fbxMaterial.Id))
            {
                if (importer.Graph.GetObject(connection.ChildId) is { ClassName: "Texture" } texture && GetSlot(connection.PropertyName) is string slot)
                    ConnectTexture(importer, material, slot, texture);
            }
        }

        ConnectLegacyModelTextures(importer, materials);
        return materials;
    }

    private static void ApplyProperties(Material material, FbxObject fbxMaterial)
    {
        Vector3	diffuse		= fbxMaterial.GetVector3("DiffuseColor", fbxMaterial.GetVector3("Diffuse", new Vector3(0.8f))) * fbxMaterial.GetSingle("DiffuseFactor", 1.0f);
        float	opacity		= GetOpacity(fbxMaterial);

        if (TryGetVector3(fbxMaterial, BaseColorProperties, out Vector3 baseColor))
            diffuse = baseColor;

        material.DiffuseColor	= new Vector4(diffuse, opacity);
        material.SpecularColor	= new Vector4(fbxMaterial.GetVector3("SpecularColor", fbxMaterial.GetVector3("Specular", new Vector3(0.2f))) * fbxMaterial.GetSingle("SpecularFactor", 1.0f), 1.0f);
        material.Shininess		= fbxMaterial.GetSingle("ShininessExponent", fbxMaterial.GetSingle("Shininess", 20.0f));

        Vector3 emissive = fbxMaterial.GetVector3("EmissiveColor", fbxMaterial.GetVector3("Emissive", Vector3.Zero)) * fbxMaterial.GetSingle("EmissiveFactor", 1.0f);

        if (emissive != Vector3.Zero)
            material.EmissiveColor = new Vector4(emissive, 1.0f);

        if (TryGetSingle(fbxMaterial, MetallicProperties, out float metallic))
            material.Metallic = metallic;

        if (TryGetSingle(fbxMaterial, RoughnessProperties, out float roughness))
            material.Roughness = roughness;
    }

    private static float GetOpacity(FbxObject fbxMaterial)
    {
        if (fbxMaterial.HasProperty("Opacity"))
            return Math.Clamp(fbxMaterial.GetSingle("Opacity", 1.0f), 0.0f, 1.0f);

        Vector3	transparent	= fbxMaterial.GetVector3("TransparentColor", Vector3.Zero);
        float	factor		= fbxMaterial.GetSingle("TransparencyFactor", 0.0f);

        return Math.Clamp(1.0f - factor * (transparent.X + transparent.Y + transparent.Z) / 3.0f, 0.0f, 1.0f);
    }

    private static bool TryGetVector3(FbxObject fbxMaterial, string[] names, out Vector3 value)
    {
        foreach (string name in names)
        {
            if (fbxMaterial.HasProperty(name))
            {
                value = fbxMaterial.GetVector3(name, Vector3.One);
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryGetSingle(FbxObject fbxMaterial, string[] names, out float value)
    {
        foreach (string name in names)
        {
            if (fbxMaterial.HasProperty(name))
            {
                value = fbxMaterial.GetSingle(name, 0.0f);
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? GetSlot(string propertyName)
    {
        string name = propertyName.ToLowerInvariant();

        if (name.Contains("transparent") || name.Contains("ambient") || name.Contains("reflection") || name.Contains("displacement"))
            return null;

        if (name.Contains("normal") || name.Contains("bump"))
            return "normal";

        if (name.Contains("rough"))
            return "roughness";

        if (name.Contains("metal"))
            return "metallic";

        if (name.Contains("emissi") || name.Contains("emit"))
            return "emissive";

        if (name.Contains("specular"))
            return "specular";

        if (name.Contains("shininess") || name.Contains("gloss"))
            return "gloss";

        if (name.Contains("ao_map") || name.Contains("occlusion"))
            return "ao";

        if (name.Contains("diffuse") || name.Contains("color") || name.Contains("base"))
            return "diffuse";

        return null;
    }

    private static void ConnectTexture(FbxSceneImporter importer, Material material, string slot, FbxObject fbxTexture)
    {
        if (material.TryGetTexture(slot, out _))
            return;

        FbxObject?	video		= importer.Graph.GetFirstChild(fbxTexture.Id, "Video");
        string		relative	= GetPath(fbxTexture, "RelativeFilename", video);
        string		absolute	= GetPath(fbxTexture, "FileName", video);
        Texture		texture		= new(string.IsNullOrEmpty(relative) ? absolute : relative)
        {
            Name = importer.GetUniqueName(fbxTexture.Name),
        };

        texture.ResolveFilePath(importer.SourceDirectory);

        if (!File.Exists(texture.ResolvedFilePath) && !string.IsNullOrEmpty(absolute) && File.Exists(absolute))
            texture.ResolvedFilePath = absolute;

        if (video?.Node.FirstChild("Content") is FbxNode content && GetContent(content) is { Length: > 0 } bytes)
            texture.ImageLoader = new FbxEmbeddedTextureLoader(bytes);

        switch (slot)
        {
            case "diffuse":
                material.DiffuseMapName = slot;
                break;
            case "normal":
                material.NormalMapName = slot;
                break;
            case "specular":
                material.SpecularMapName = slot;
                break;
            case "metallic":
                material.MetallicMapName = slot;
                break;
            case "roughness":
                material.RoughnessMapName = slot;
                break;
            case "gloss":
                material.GlossMapName = slot;
                break;
            case "emissive":
                material.EmissiveMapName = slot;
                break;
            case "ao":
                material.AmbientOcclusionMapName = slot;
                break;
        }

        texture.SetParentUnsafe(material);
        material.Connect(slot, texture);
    }

    private static string GetPath(FbxObject fbxTexture, string name, FbxObject? video)
    {
        if (fbxTexture.Node.TryGetChildProperty(name, out FbxProperty value) && value.Type == FbxPropertyType.String && value.AsString().Length > 0)
            return value.AsString();

        string videoName = name == "FileName" ? "Filename" : name;

        if (video is not null && video.Node.TryGetChildProperty(videoName, out value) && value.Type == FbxPropertyType.String)
            return value.AsString();

        return fbxTexture.GetString(name == "FileName" ? "Path" : name, string.Empty);
    }

    private static byte[]? GetContent(FbxNode content)
    {
        if (content.Properties.Count == 1 && content.Properties[0].Type == FbxPropertyType.Raw)
            return content.Properties[0].AsBytes();

        string encoded = string.Concat(content.Properties.Where(static property => property.Type == FbxPropertyType.String).Select(static property => property.AsString()));
        byte[] decoded = new byte[encoded.Length * 3 / 4 + 3];

        return Convert.TryFromBase64String(encoded, decoded, out int written) ? decoded[..written] : null;
    }

    private static void ConnectLegacyModelTextures(FbxSceneImporter importer, Dictionary<long, Material> materials)
    {
        foreach (FbxObject fbxTexture in importer.Graph.Objects)
        {
            if (fbxTexture.ClassName != "Texture")
                continue;

            foreach (FbxObject model in importer.Graph.GetParents(fbxTexture.Id, "Model"))
            {
                foreach (FbxObject fbxMaterial in importer.Graph.GetChildren(model.Id, "Material"))
                {
                    if (materials.TryGetValue(fbxMaterial.Id, out Material? material))
                        ConnectTexture(importer, material, "diffuse", fbxTexture);
                }
            }
        }
    }
}
