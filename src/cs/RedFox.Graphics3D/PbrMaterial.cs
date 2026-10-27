namespace RedFox.Graphics3D;

/// <summary>
/// Provides the material type, texture input and parameter names of the shared PBR material types.
/// </summary>
public static class PbrMaterial
{
    /// <summary>
    /// The opaque PBR material type. Alpha tests against <see cref="Material.AlphaCutoff"/> when it is above zero.
    /// </summary>
    public const string TypeName = "Pbr";

    /// <summary>
    /// The alpha-blended PBR material type. Reflections stay visible where the surface is transparent.
    /// </summary>
    public const string TransparentTypeName = "PbrTransparent";

    /// <summary>
    /// The opaque hair material type, lit with dual Kajiya-Kay highlights along the bitangent.
    /// </summary>
    public const string HairTypeName = "PbrHair";

    /// <summary>
    /// The alpha-blended hair material type.
    /// </summary>
    public const string HairTransparentTypeName = "PbrHairTransparent";

    /// <summary>
    /// The eye material type: <see cref="NormalMap"/> shapes the cornea reflection and <see cref="IrisNormalMap"/> the iris.
    /// </summary>
    public const string EyeTypeName = "PbrEye";

    /// <summary>
    /// The opaque unlit material type, showing base color and emission without lighting.
    /// </summary>
    public const string UnlitTypeName = "PbrUnlit";

    /// <summary>
    /// The alpha-blended unlit material type.
    /// </summary>
    public const string UnlitTransparentTypeName = "PbrUnlitTransparent";

    /// <summary>
    /// The additive unlit material type, for glows and effects.
    /// </summary>
    public const string AdditiveTypeName = "PbrAdditive";

    /// <summary>
    /// The base color input, read from RGB. Defaults to the texture at <see cref="Material.DiffuseMapName"/>.
    /// </summary>
    public const string DiffuseMap = "DiffuseMap";

    /// <summary>
    /// The opacity input, read from the first component. Without it, opacity comes from the alpha of <see cref="DiffuseMap"/>.
    /// </summary>
    public const string OpacityMap = "OpacityMap";

    /// <summary>
    /// The tangent-space normal input, read from the first two components with green pointing along increasing V. Defaults to the texture at <see cref="Material.NormalMapName"/>.
    /// </summary>
    public const string NormalMap = "NormalMap";

    /// <summary>
    /// The detail normal input, read like <see cref="NormalMap"/> at the UVs scaled by <see cref="DetailNormalTiling"/>.
    /// </summary>
    public const string DetailNormalMap = "DetailNormalMap";

    /// <summary>
    /// The roughness input, read from the first component. Defaults to the texture at <see cref="Material.RoughnessMapName"/>; invert a gloss map with a scale of -1 and a bias of 1.
    /// </summary>
    public const string RoughnessMap = "RoughnessMap";

    /// <summary>
    /// The metalness input, read from the first component. Defaults to the texture at <see cref="Material.MetallicMapName"/>.
    /// </summary>
    public const string MetallicMap = "MetallicMap";

    /// <summary>
    /// The specular color input, read from RGB. When present the material uses the specular workflow and ignores metalness. Defaults to the texture at <see cref="Material.SpecularMapName"/>.
    /// </summary>
    public const string SpecularMap = "SpecularMap";

    /// <summary>
    /// The ambient occlusion input, read from the first component. Defaults to the texture at <see cref="Material.AmbientOcclusionMapName"/>.
    /// </summary>
    public const string OcclusionMap = "OcclusionMap";

    /// <summary>
    /// The emission input, read from RGB and multiplied by <see cref="Material.EmissiveColor"/>. Defaults to the texture at <see cref="Material.EmissiveMapName"/>.
    /// </summary>
    public const string EmissiveMap = "EmissiveMap";

    /// <summary>
    /// The subsurface scattering mask, read from the first component and multiplied by <see cref="SubsurfaceStrength"/>.
    /// </summary>
    public const string SubsurfaceMap = "SubsurfaceMap";

    /// <summary>
    /// The translucency input, read from the first component. 1 lets through all of <see cref="TranslucencyStrength"/>, lower values less, filtered by <see cref="SubsurfaceColor"/>.
    /// </summary>
    public const string TranslucencyMap = "TranslucencyMap";

    /// <summary>
    /// The hair highlight shift, read from the first component and added to <see cref="HairShift"/>.
    /// </summary>
    public const string HairShiftMap = "HairShiftMap";

    /// <summary>
    /// The hair strand direction input, read like <see cref="NormalMap"/>. Without it, strands run along V.
    /// </summary>
    public const string HairFlowMap = "HairFlowMap";

    /// <summary>
    /// The iris normal input of <see cref="EyeTypeName"/>, read like <see cref="NormalMap"/>.
    /// </summary>
    public const string IrisNormalMap = "IrisNormalMap";

    /// <summary>
    /// The float attribute that sets the dielectric reflectance, where 0.5 gives the common 4% at normal incidence.
    /// </summary>
    public const string Reflectance = "Reflectance";

    /// <summary>
    /// The float attribute that scales the <see cref="NormalMap"/> bumps.
    /// </summary>
    public const string NormalStrength = "NormalStrength";

    /// <summary>
    /// The <see cref="PbrNormalEncoding"/> attribute that sets how every normal input decodes.
    /// </summary>
    public const string NormalEncoding = "NormalEncoding";

    /// <summary>
    /// The float attribute that scales the <see cref="DetailNormalMap"/> bumps.
    /// </summary>
    public const string DetailNormalStrength = "DetailNormalStrength";

    /// <summary>
    /// The <see cref="System.Numerics.Vector2"/> attribute that scales the UVs of <see cref="DetailNormalMap"/>.
    /// </summary>
    public const string DetailNormalTiling = "DetailNormalTiling";

    /// <summary>
    /// The <see cref="System.Numerics.Vector2"/> attribute that scales the UVs of every other texture input.
    /// </summary>
    public const string TextureTiling = "TextureTiling";

    /// <summary>
    /// The float attribute that sets how far light scatters under the surface, from 0 for none to 1 for skin.
    /// </summary>
    public const string SubsurfaceStrength = "SubsurfaceStrength";

    /// <summary>
    /// The <see cref="System.Numerics.Vector3"/> attribute that sets how far each color channel scatters and transmits.
    /// </summary>
    public const string SubsurfaceColor = "SubsurfaceColor";

    /// <summary>
    /// The float attribute that sets how much light passes through from behind.
    /// </summary>
    public const string TranslucencyStrength = "TranslucencyStrength";

    /// <summary>
    /// The float attribute that slides hair highlights along the strand.
    /// </summary>
    public const string HairShift = "HairShift";

    /// <summary>
    /// The float attribute that scales hair highlights.
    /// </summary>
    public const string HairSpecularStrength = "HairSpecularStrength";
}
