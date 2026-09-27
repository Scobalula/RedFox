using System.Numerics;

namespace RedFox.Graphics3D.Formats.WavefrontObj;

internal sealed class MeshMergeBucket(Material? material)
{
    public Material? Material { get; } = material;

    public List<Vector3> Positions { get; } = [];

    public List<Vector3> Normals { get; } = [];

    public List<Vector2> TexCoords { get; } = [];

    public List<int> FaceIndices { get; } = [];

    public bool HasCompleteNormals { get; set; } = true;

    public bool HasCompleteTexCoords { get; set; } = true;
}
