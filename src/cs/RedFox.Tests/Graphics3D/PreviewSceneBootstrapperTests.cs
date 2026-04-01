using System.Text;
using System.Numerics;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Preview;

namespace RedFox.Tests.Graphics3D;

public sealed class PreviewSceneBootstrapperTests
{
    [Fact]
    public void LoadScene_WithMultipleObjFiles_CreatesContainersAndMeshes()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"RedFoxPreview_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string firstPath = Path.Combine(tempDirectory, "first.obj");
            string secondPath = Path.Combine(tempDirectory, "second.obj");

            File.WriteAllText(firstPath, CreateTriangleObj("FirstMesh"), Encoding.UTF8);
            File.WriteAllText(secondPath, CreateTriangleObj("SecondMesh"), Encoding.UTF8);

            Scene scene = SceneBootstrapper.LoadScene(
                [
                    firstPath,
                    secondPath,
                ],
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
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"RedFoxPreview_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string inputPath = Path.Combine(tempDirectory, "z_up.obj");
            File.WriteAllText(inputPath, """
                o ZUpMesh
                v 0 0 1
                v 1 0 1
                v 0 1 1
                f 1 2 3
                """, Encoding.UTF8);

            Scene scene = SceneBootstrapper.LoadScene(
                [
                    inputPath,
                ],
                TranslatorRegistry.CreateSceneTranslatorManager(),
                upAxis: SceneUpAxis.Z);

            Mesh mesh = scene.RootNode.EnumerateDescendants<Mesh>().Single();
            Vector3 worldPosition = Vector3.Transform(mesh.Positions!.GetVector3(0, 0), mesh.GetActiveWorldMatrix());
            Vector3 transformedPosition = Vector3.Transform(worldPosition, SceneBootstrapper.GetUpAxisTransform(SceneUpAxis.Z));

            Assert.Equal(0.0f, worldPosition.X, 4);
            Assert.Equal(0.0f, worldPosition.Y, 4);
            Assert.Equal(1.0f, worldPosition.Z, 4);

            Assert.Equal(0.0f, transformedPosition.X, 4);
            Assert.Equal(1.0f, transformedPosition.Y, 4);
            Assert.Equal(0.0f, transformedPosition.Z, 4);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
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
