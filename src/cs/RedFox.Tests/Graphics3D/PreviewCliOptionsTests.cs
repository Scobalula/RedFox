using RedFox.Graphics3D.OpenGL;
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
            "--msaa", "8",
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
        Assert.Equal(8, options.MsaaSamples);
        Assert.Equal(2, options.InputFiles.Count);
        Assert.EndsWith("mesh_a.obj", options.InputFiles[0], StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("mesh_b.obj", options.InputFiles[1], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_WithEnvironmentMapFlags_BindsExpectedOptions()
    {
        PreviewCliOptions options = PreviewCliOptions.Parse(
        [
            "--envmap", "test.exr",
            "--envmap-exposure", "1.25",
            "--envmap-intensity", "0.25",
            "--envmap-blur",
            "--envmap-blur-radius", "3.0",
            "--envmap-no-flip-y",
            "--no-ibl",
            "mesh.obj",
        ]);

        Assert.Equal("test.exr", options.EnvironmentMapPath);
        Assert.Equal(1.25f, options.EnvironmentMapExposure, 3);
        Assert.Equal(0.25f, options.EnvironmentMapReflectionIntensity, 3);
        Assert.True(options.EnvironmentMapBlur);
        Assert.Equal(3.0f, options.EnvironmentMapBlurRadius, 3);
        Assert.Equal(EnvironmentMapFlipMode.ForceNoFlipY, options.EnvironmentMapFlipMode);
        Assert.False(options.EnableIBL);
    }

    [Fact]
    public void Parse_WithSkyboxAndShadingFlags_BindsExpectedOptions()
    {
        PreviewCliOptions options = PreviewCliOptions.Parse(
        [
            "--no-skybox",
            "--shading", "fullbright",
            "mesh.obj",
        ]);

        Assert.False(options.ShowSkybox);
        Assert.Equal(RendererShadingMode.Fullbright, options.ShadingMode);
    }

    [Fact]
    public void Parse_WithEnvmapFlipY_SetsFlipMode()
    {
        PreviewCliOptions options = PreviewCliOptions.Parse(
        [
            "--envmap", "test.exr",
            "--envmap-flip-y",
            "mesh.obj",
        ]);

        Assert.Equal(EnvironmentMapFlipMode.ForceFlipY, options.EnvironmentMapFlipMode);
    }

    [Fact]
    public void Parse_WithConflictingEnvmapFlipFlags_Throws()
    {
        Assert.Throws<ArgumentException>(() => PreviewCliOptions.Parse(
        [
            "--envmap", "test.exr",
            "--envmap-flip-y",
            "--envmap-no-flip-y",
            "mesh.obj",
        ]));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(4, 4)]
    public void Parse_WithMsaa_BindsRequestedSampleCount(int requested, int expected)
    {
        PreviewCliOptions options = PreviewCliOptions.Parse(
        [
            "--msaa", requested.ToString(),
            "mesh.obj",
        ]);

        Assert.Equal(expected, options.MsaaSamples);
    }

    [Fact]
    public void Parse_WithNegativeMsaa_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PreviewCliOptions.Parse(
        [
            "--msaa", "-1",
            "mesh.obj",
        ]));
    }

    [Fact]
    public void Parse_WithoutBackendFlag_DefaultsToCliBackend()
    {
        PreviewCliOptions options = PreviewCliOptions.Parse(
        [
            "mesh.obj",
        ]);

        Assert.Equal(PreviewBackend.Cli, options.Backend);
    }

    [Theory]
    [InlineData("--backend", "cli", PreviewBackend.Cli)]
    [InlineData("--backend", "avalonia", PreviewBackend.Avalonia)]
    public void Parse_WithBackendFlag_BindsExpectedBackend(string option, string value, PreviewBackend expected)
    {
        PreviewCliOptions options = PreviewCliOptions.Parse(
        [
            option, value,
            "mesh.obj",
        ]);

        Assert.Equal(expected, options.Backend);
    }
}
