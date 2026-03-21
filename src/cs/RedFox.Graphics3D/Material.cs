using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace RedFox.Graphics3D
{
    public class Material(string name) : SceneNode(name)
    {
        public Material() : this(string.Empty) { }

        public string? DiffuseMapName { get; set; }
        public string? SpecularMapName { get; set; }
        public string? MetallicMapName { get; set; }
        public string? GlossMapName { get; set; }
        public string? RoughnessMapName { get; set; }
        public string? NormalMapName { get; set; }
        public string? EmissiveMapName { get; set; }
        public string? EmissiveMaskMapName { get; set; }
        public string? AmbientOcclusionMapName { get; set; }
        public string? CavityMapName { get; set; }
        public string? AnisotropyMapName { get; set; }

        public Vector4? DiffuseColor { get; set; }
        public Vector4? SpecularColor { get; set; }
        public Vector4? MetallicColor { get; set; }
        public Vector4? GlossColor { get; set; }
        public Vector4? RoughnessColor { get; set; }
        public Vector4? EmissiveColor { get; set; }
        public Vector4? AmbientOcclusionColor { get; set; }
        public Vector4? CavityColor { get; set; }
        public Vector4? AnisotropyColor { get; set; }

        public float? Shininess { get; set; }
        public float? SpecularStrength { get; set; }

        /// <summary>
        /// When <see langword="true"/>, the alpha channel of the diffuse texture is used
        /// as a specular intensity mask (common in many game engines).
        /// </summary>
        public bool SpecularMaskInDiffuseAlpha { get; set; }

        /// <summary>
        /// When <see langword="true"/>, the specular map contains full-colour F0 values
        /// rather than a grayscale multiplier. The shader will replace (not multiply)
        /// the base specular colour with the map values.
        /// </summary>
        public bool UseSpecularColorMap { get; set; }

        /// <summary>
        /// Returns the texture map name for the given semantic slot, or <see langword="null"/>
        /// if the slot is not assigned. Used by schema-driven renderers to resolve
        /// material texture references dynamically.
        /// </summary>
        public string? GetMapName(string slotName) => slotName switch
        {
            "diffuse"    => DiffuseMapName,
            "normal"     => NormalMapName,
            "specular"   => SpecularMapName,
            "metallic"   => MetallicMapName,
            "emissive"   => EmissiveMapName,
            "roughness"  => RoughnessMapName,
            "gloss"      => GlossMapName,
            "ao"         => AmbientOcclusionMapName,
            "cavity"     => CavityMapName,
            "anisotropy" => AnisotropyMapName,
            _            => null,
        };
    }
}
