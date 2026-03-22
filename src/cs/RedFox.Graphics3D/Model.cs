using System.Diagnostics.CodeAnalysis;

namespace RedFox.Graphics3D;

/// <summary>
/// Represents a 3D model node that provides access to descendant meshes and materials within the scene hierarchy.
/// </summary>
public class Model : SceneNode
{
    /// <summary>
    /// Returns an enumerable collection of all descendant meshes in the hierarchy.
    /// </summary>
    /// <returns>An enumerable collection of <see cref="Mesh"/> objects.</returns>
    public IEnumerable<Mesh> EnumerateMeshes() => EnumerateDescendants().OfType<Mesh>();

    /// <summary>
    /// Returns an array containing all descendant meshes in the hierarchy.
    /// </summary>
    /// <returns>An array of <see cref="Mesh"/> objects.</returns>
    public Mesh[] GetMeshes() => [.. EnumerateMeshes()];

    /// <summary>
    /// Attempts to find a mesh with the specified name.
    /// </summary>
    /// <param name="name">The name of the mesh to locate.</param>
    /// <param name="mesh">The mesh if found, otherwise null.</param>
    /// <returns>true if a mesh with the specified name is found; otherwise, false.</returns>
    public bool TryFindMesh(string name, [NotNullWhen(true)] out Mesh? mesh)
        => TryFindMesh(name, StringComparison.CurrentCulture, out mesh);

    /// <summary>
    /// Attempts to find a mesh with the specified name.
    /// </summary>
    /// <param name="name">The name of the mesh to locate.</param>
    /// <param name="comparisonType">A value that specifies the rules for the string comparison used to match the mesh name.</param>
    /// <param name="mesh">The mesh if found, otherwise null.</param>
    /// <returns>true if a mesh with the specified name is found; otherwise, false.</returns>
    public bool TryFindMesh(string name, StringComparison comparisonType, [NotNullWhen(true)] out Mesh? mesh)
        => TryFindDescendant(name, comparisonType, out mesh);

    /// <summary>
    /// Attempts to find a mesh with the specified name.
    /// </summary>
    /// <param name="name">The name of the mesh to locate.</param>
    /// <returns>A mesh object that matches the specified name.</returns>
    public Mesh FindMesh(string name) => FindMesh(name, StringComparison.CurrentCulture);

    /// <summary>
    /// Attempts to find a mesh with the specified name.
    /// </summary>
    /// <param name="name">The name of the mesh to locate.</param>
    /// <param name="comparisonType">A value that specifies the rules for the string comparison used to match the mesh name.</param>
    /// <returns>A mesh object that matches the specified name.</returns>
    private Mesh FindMesh(string name, StringComparison comparisonType)
    {
        if (TryFindDescendant(name, comparisonType, out Mesh? node))
            return node;

        throw new KeyNotFoundException($"A mesh with the name: {name} was not found in: {Name}");
    }

    /// <summary>
    /// Returns an enumerable collection of all descendant materials in the hierarchy.
    /// </summary>
    /// <returns>An enumerable collection of <see cref="Material"/> objects.</returns>
    public IEnumerable<Material> EnumerateMaterials() => EnumerateDescendants().OfType<Material>();

    /// <summary>
    /// Returns an array containing all descendant materials in the hierarchy.
    /// </summary>
    /// <returns>An array of <see cref="Material"/> objects.</returns>
    public Material[] GetMaterials() => [.. EnumerateMaterials()];

    /// <summary>
    /// Attempts to find a material with the specified name.
    /// </summary>
    /// <param name="name">The name of the material to locate.</param>
    /// <param name="material">The material if found, otherwise null.</param>
    /// <returns>true if a material with the specified name is found; otherwise, false.</returns>
    public bool TryFindMaterial(string name, [NotNullWhen(true)] out Material? material)
        => TryFindMaterial(name, StringComparison.CurrentCulture, out material);

    /// <summary>
    /// Attempts to find a material with the specified name.
    /// </summary>
    /// <param name="name">The name of the material to locate.</param>
    /// <param name="comparisonType">A value that specifies the rules for the string comparison used to match the material name.</param>
    /// <param name="material">The material if found, otherwise null.</param>
    /// <returns>true if a material with the specified name is found; otherwise, false.</returns>
    public bool TryFindMaterial(string name, StringComparison comparisonType, [NotNullWhen(true)] out Material? material)
        => TryFindDescendant(name, comparisonType, out material);

    /// <summary>
    /// Attempts to find a material with the specified name.
    /// </summary>
    /// <param name="name">The name of the material to locate.</param>
    /// <returns>A material object that matches the specified name.</returns>
    public Material FindMaterial(string name) => FindMaterial(name, StringComparison.CurrentCulture);

    /// <summary>
    /// Attempts to find a material with the specified name.
    /// </summary>
    /// <param name="name">The name of the material to locate.</param>
    /// <param name="comparisonType">A value that specifies the rules for the string comparison used to match the material name.</param>
    /// <returns>A material object that matches the specified name.</returns>
    private Material FindMaterial(string name, StringComparison comparisonType)
    {
        if (TryFindDescendant(name, comparisonType, out Material? node))
            return node;

        throw new KeyNotFoundException($"A material with the name: {name} was not found in: {Name}");
    }
}
