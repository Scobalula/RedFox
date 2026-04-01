using RedFox.Graphics3D.OpenGL.Cameras;
using RedFox.Graphics3D.Preview;

namespace RedFox.Tests.Graphics3D;

public sealed class PreviewCliOptionsTests
{
    [Fact]
    public void Parse_WithMultipleFilesAndFlags_BindsExpectedOptions()
    {
        PreviewCliOptions options = PreviewCliOptions.Parse(
        [
            "--hidden",
            "--frames", "2",
            "--width", "1600",
            "--height", "900",
            "--camera", "fps",
            "--up-axis", "z",
            "--animation", "Walk",
            "--speed", "1.5",
            "--normalize-scene",
            "--normalize-radius", "8",
            "--no-grid",
            "--no-fit",
            "--wireframe",
            "--no-bones",
            "mesh_a.obj",
            "mesh_b.obj",
        ]);

        Assert.True(options.Hidden);
        Assert.True(options.Wireframe);
        Assert.False(options.ShowBones);
        Assert.False(options.ShowGrid);
        Assert.False(options.AutoFitOnLoad);
        Assert.True(options.NormalizeScene);
        Assert.Equal(2, options.MaxFrames);
        Assert.Equal(1600, options.Width);
        Assert.Equal(900, options.Height);
        Assert.Equal(CameraMode.Fps, options.CameraMode);
        Assert.Equal(SceneUpAxis.Z, options.UpAxis);
        Assert.Equal("Walk", options.AnimationName);
        Assert.Equal(1.5f, options.AnimationSpeed, 3);
        Assert.Equal(8.0f, options.NormalizeRadius, 3);
        Assert.Equal(2, options.InputFiles.Count);
        Assert.EndsWith("mesh_a.obj", options.InputFiles[0], StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("mesh_b.obj", options.InputFiles[1], StringComparison.OrdinalIgnoreCase);
    }
}
