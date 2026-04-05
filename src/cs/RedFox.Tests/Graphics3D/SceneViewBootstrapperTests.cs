using System.Numerics;
using System.Text;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Preview;
using FloatBuffer = RedFox.Graphics3D.Buffers.DataBuffer<float>;
using Viewing = RedFox.Graphics3D.OpenGL.Viewing;

namespace RedFox.Tests.Graphics3D;

public sealed class SceneViewBootstrapperTests
{
    [Fact]
    public void LoadScene_WithMultipleObjFiles_CreatesContainersAndMeshes()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"RedFoxSceneView_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string firstPath = Path.Combine(tempDirectory, "first.obj");
            string secondPath = Path.Combine(tempDirectory, "second.obj");

            File.WriteAllText(firstPath, CreateTriangleObj("FirstMesh"), Encoding.UTF8);
            File.WriteAllText(secondPath, CreateTriangleObj("SecondMesh"), Encoding.UTF8);

            Scene scene = Viewing.SceneViewBootstrapper.LoadScene(
                [firstPath, secondPath],
                TranslatorRegistry.CreateSceneTranslatorManager());

            Model[] containers = scene.RootNode.EnumerateChildren<Model>().ToArray();
            Mesh[] meshes = scene.RootNode.EnumerateDescendants<Mesh>().ToArray();

            Assert.Equal(2, containers.Length);
            Assert.Equal("first", containers[0].Name);
            Assert.Equal("second", containers[1].Name);
            Assert.Equal(2, meshes.Length);
            Assert.Contains(meshes, mesh => mesh.Name == "FirstMesh");
            Assert.Contains(meshes, mesh => mesh.Name == "SecondMesh");
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void GetUpAxisTransform_WithZUpAxis_RotatesZIntoY()
    {
        Vector3 transformed = Vector3.Transform(Vector3.UnitZ, Viewing.SceneViewBootstrapper.GetUpAxisTransform(Viewing.SceneUpAxis.Z));

        Assert.Equal(0.0f, transformed.X, 4);
        Assert.Equal(1.0f, transformed.Y, 4);
        Assert.Equal(0.0f, transformed.Z, 4);
    }

    [Fact]
    public void GetSceneTransform_WithNormalization_ScalesSceneToTargetRadius()
    {
        Scene scene = new("Normalize");
        Mesh mesh = scene.RootNode.AddNode(new Mesh { Name = "Triangle" });
        mesh.Positions = new FloatBuffer(
            [
                0.0f, 0.0f, 0.0f,
                4.0f, 0.0f, 0.0f,
                0.0f, 4.0f, 0.0f
            ],
            valueCount: 1,
            componentCount: 3);

        Matrix4x4 transform = Viewing.SceneViewBootstrapper.GetSceneTransform(scene, Viewing.SceneUpAxis.Y, normalizeScene: true, normalizeRadius: 2.0f);

        Assert.True(Viewing.SceneBounds.TryGetBounds(scene, transform, out Viewing.SceneBoundsInfo bounds));
        Assert.Equal(2.0f, bounds.Radius, 3);
    }

    private static string CreateTriangleObj(string name)
    {
        return $$"""
            o {{name}}
            v 0 0 0
            v 1 0 0
            v 0 1 0
            f 1 2 3
            """;
    }
}
