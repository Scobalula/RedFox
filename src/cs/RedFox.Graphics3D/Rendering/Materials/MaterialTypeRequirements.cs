using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RedFox.Graphics3D.Rendering.Materials;

/// <summary>
/// Describes material inputs required or accepted by a material type.
/// </summary>
public sealed record class MaterialTypeRequirements
{
    private static readonly MaterialTextureRequirement[] EmptyTextures = [];
    private static readonly MaterialUniformRequirement[] EmptyUniforms = [];
    private readonly MaterialTextureRequirement[] _textures;
    private readonly MaterialUniformRequirement[] _uniforms;

    /// <summary>
    /// Gets an empty requirements value.
    /// </summary>
    public static MaterialTypeRequirements Empty { get; } = new(EmptyTextures, EmptyUniforms);

    /// <summary>
    /// Gets the texture inputs used by the material type.
    /// </summary>
    public IReadOnlyList<MaterialTextureRequirement> Textures => _textures;

    /// <summary>
    /// Gets the uniform inputs used by the material type.
    /// </summary>
    public IReadOnlyList<MaterialUniformRequirement> Uniforms => _uniforms;

    /// <summary>
    /// Initializes a new <see cref="MaterialTypeRequirements"/> value.
    /// </summary>
    /// <param name="textures">The texture inputs used by the material type.</param>
    /// <param name="uniforms">The uniform inputs used by the material type.</param>
    [JsonConstructor]
    public MaterialTypeRequirements(
        IReadOnlyList<MaterialTextureRequirement>? textures,
        IReadOnlyList<MaterialUniformRequirement>? uniforms)
    {
        _textures = CopyTextures(textures ?? EmptyTextures);
        _uniforms = CopyUniforms(uniforms ?? EmptyUniforms);
    }

    private static MaterialTextureRequirement[] CopyTextures(IReadOnlyList<MaterialTextureRequirement> textures)
    {
        MaterialTextureRequirement[] copy = new MaterialTextureRequirement[textures.Count];
        for (int index = 0; index < textures.Count; index++)
        {
            copy[index] = textures[index] ?? throw new ArgumentException("Texture requirement entries cannot be null.", nameof(textures));
        }

        return copy;
    }

    private static MaterialUniformRequirement[] CopyUniforms(IReadOnlyList<MaterialUniformRequirement> uniforms)
    {
        MaterialUniformRequirement[] copy = new MaterialUniformRequirement[uniforms.Count];
        for (int index = 0; index < uniforms.Count; index++)
        {
            copy[index] = uniforms[index] ?? throw new ArgumentException("Uniform requirement entries cannot be null.", nameof(uniforms));
        }

        return copy;
    }
}