using System.Numerics;

namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Writes materials as FBX Phong surfaces with their file textures.
/// </summary>
public static class FbxMaterialExporter
{
    /// <summary>
    /// Writes every selected material and every material used by an exported mesh.
    /// </summary>
    /// <param name="exporter">The active scene exporter.</param>
    public static void Export(FbxSceneExporter exporter)
    {
        foreach (Material material in exporter.Selection.GetDescendants<Material>())
            ExportMaterial(exporter, material);

        foreach (SceneNode node in exporter.ModelIds.Keys)
        {
            if (node is not Mesh { Materials: not null } mesh)
                continue;

            foreach (Material material in mesh.Materials)
            {
                if (!exporter.Selection.Includes(material))
                    throw new InvalidDataException($"Mesh '{mesh.Name}' references material '{material.Name}' that is not part of the export selection.");

                ExportMaterial(exporter, material);
            }
        }
    }

    private static void ExportMaterial(FbxSceneExporter exporter, Material material)
    {
        if (exporter.MaterialIds.ContainsKey(material))
            return;

        long	materialId	= exporter.AddObject("Material", material.Name, string.Empty, out FbxNode node);
        FbxNode	properties	= new("Properties70");
        Vector4	diffuse		= material.DiffuseColor ?? new Vector4(0.8f, 0.8f, 0.8f, 1.0f);
        Vector4	specular	= material.SpecularColor ?? new Vector4(0.2f, 0.2f, 0.2f, 1.0f);
        Vector4	emissive	= material.EmissiveColor ?? Vector4.Zero;

        exporter.MaterialIds[material] = materialId;

        node.AddChild("Version", new FbxProperty(102));
        node.AddChild("ShadingModel", new FbxProperty("phong"));
        node.AddChild("MultiLayer", new FbxProperty(0));
        node.Children.Add(properties);

        FbxSceneExporter.AddProperty(properties, "ShadingModel", "KString", string.Empty, string.Empty, new FbxProperty("Phong"));
        FbxSceneExporter.AddVector3(properties, "DiffuseColor", "Color", "A", new Vector3(diffuse.X, diffuse.Y, diffuse.Z));
        FbxSceneExporter.AddNumber(properties, "DiffuseFactor", "A", 1.0);
        FbxSceneExporter.AddVector3(properties, "TransparentColor", "Color", "A", Vector3.One);
        FbxSceneExporter.AddNumber(properties, "TransparencyFactor", "A", 1.0 - diffuse.W);
        FbxSceneExporter.AddNumber(properties, "Opacity", "A", diffuse.W);
        FbxSceneExporter.AddVector3(properties, "SpecularColor", "Color", "A", new Vector3(specular.X, specular.Y, specular.Z));
        FbxSceneExporter.AddNumber(properties, "SpecularFactor", "A", material.SpecularStrength ?? 1.0f);
        FbxSceneExporter.AddNumber(properties, "ShininessExponent", "A", material.Shininess ?? 20.0f);
        FbxSceneExporter.AddVector3(properties, "EmissiveColor", "Color", "A", new Vector3(emissive.X, emissive.Y, emissive.Z));
        FbxSceneExporter.AddNumber(properties, "EmissiveFactor", "A", 1.0);

        if (material.Metallic is float metallic)
            FbxSceneExporter.AddNumber(properties, "Maya|metallic", "A+U", metallic);

        if (material.Roughness is float roughness)
            FbxSceneExporter.AddNumber(properties, "Maya|roughness", "A+U", roughness);

        ExportTexture(exporter, materialId, material.DiffuseMap, "DiffuseColor");
        ExportTexture(exporter, materialId, material.NormalMap, "NormalMap");
        ExportTexture(exporter, materialId, material.SpecularMap, "SpecularColor");
        ExportTexture(exporter, materialId, material.EmissiveMap, "EmissiveColor");
        ExportTexture(exporter, materialId, material.GlossMap, "ShininessExponent");
        ExportTexture(exporter, materialId, material.MetallicMap, "Maya|TEX_metallic_map");
        ExportTexture(exporter, materialId, material.RoughnessMap, "Maya|TEX_roughness_map");
        ExportTexture(exporter, materialId, material.AmbientOcclusionMap, "Maya|TEX_ao_map");
    }

    private static void ExportTexture(FbxSceneExporter exporter, long materialId, Texture? texture, string property)
    {
        if (texture is null)
            return;

        string	relative	= texture.GetPortableFilePath(exporter.TargetDirectory);
        string	absolute	= Path.IsPathRooted(texture.EffectiveFilePath) ? texture.EffectiveFilePath.Replace('\\', '/') : relative;
        string	name		= Path.GetFileNameWithoutExtension(texture.FilePath);
        long	textureId	= exporter.AddObject("Texture", name, string.Empty, out FbxNode textureNode);
        long	videoId		= exporter.AddObject("Video", name, "Clip", out FbxNode video);
        FbxNode	properties	= new("Properties70");

        textureNode.AddChild("Type", new FbxProperty("TextureVideoClip"));
        textureNode.AddChild("Version", new FbxProperty(202));
        textureNode.AddChild("TextureName", new FbxProperty($"{name}\0\u0001Texture"));
        textureNode.Children.Add(properties);
        textureNode.AddChild("Media", new FbxProperty($"{name}\0\u0001Video"));
        textureNode.AddChild("FileName", new FbxProperty(absolute));
        textureNode.AddChild("RelativeFilename", new FbxProperty(relative));
        textureNode.AddChild("ModelUVTranslation", new FbxProperty(0.0)).Properties.Add(new FbxProperty(0.0));
        textureNode.AddChild("ModelUVScaling", new FbxProperty(1.0)).Properties.Add(new FbxProperty(1.0));
        textureNode.AddChild("Texture_Alpha_Source", new FbxProperty("None"));

        FbxSceneExporter.AddProperty(properties, "UseMaterial", "bool", string.Empty, string.Empty, new FbxProperty(1));
        FbxSceneExporter.AddProperty(properties, "UVSet", "KString", string.Empty, string.Empty, new FbxProperty("map1"));

        FbxNode videoProperties = new("Properties70");

        video.AddChild("Type", new FbxProperty("Clip"));
        video.Children.Add(videoProperties);
        video.AddChild("UseMipMap", new FbxProperty(0));
        video.AddChild("Filename", new FbxProperty(absolute));
        video.AddChild("RelativeFilename", new FbxProperty(relative));

        if (texture.ImageLoader is FbxEmbeddedTextureLoader embedded)
            video.AddChild("Content", new FbxProperty(embedded.Content));

        FbxSceneExporter.AddProperty(videoProperties, "Path", "KString", "XRefUrl", string.Empty, new FbxProperty(absolute));

        exporter.Connect(videoId, textureId);
        exporter.Connect(textureId, materialId, property);
    }
}
