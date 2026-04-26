using RedFox.Graphics3D;
using RedFox.Graphics3D.Rendering;

namespace RedFox.Samples.Examples;

internal sealed class MeshSampleOptions
{
    public List<string> ScenePaths { get; } = [];

    public bool ShowGrid { get; set; } = true;

    public SceneUpAxis UpAxis { get; set; } = SceneUpAxis.Y;

    public FaceWinding FaceWinding { get; set; } = FaceWinding.CounterClockwise;

    public bool UseViewBasedLighting { get; set; }

    public SkinningMode SkinningMode { get; set; } = SkinningMode.Linear;

    public float ExitAfterSeconds { get; set; }
}
