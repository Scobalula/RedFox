using RedFox.Graphics3D;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Formats.SEModel;
using RedFox.Graphics3D.Groups;
using RedFox.Graphics3D.IO;

namespace RedFox.Tests.Graphics3D;

public sealed class SemodelFormatFixTests
{
    [Fact]
    public void SemodelTranslator_RoundTripsMaterialWithoutUvsAndNormalizedColors()
    {
        Scene source = new("semodel");
        MeshGroup model = source.RootNode.AddNode(new MeshGroup
        {
            Name = "model",
        });
        Mesh mesh = model.AddNode(new Mesh
        {
            Name = "mesh",
        });
        mesh.Positions = new DataBuffer<float>([0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f], 1, 3);
        mesh.FaceIndices = new DataBuffer<int>([0, 1, 2], 1, 1);
        mesh.ColorLayers = new DataBuffer<float>([1f, 0.5f, 0f, 1f, 1f, 0.5f, 0f, 1f, 1f, 0.5f, 0f, 1f], 1, 4);
        Material material = model.AddNode(new Material("matériel"));
        mesh.Materials = [material];
        SceneTranslatorManager manager = new();
        manager.Register(new SemodelTranslator());

        using MemoryStream stream = new();
        manager.Write(stream, "scene.semodel", source, new SceneTranslatorOptions(), token: null);
        stream.Position = 0;
        Scene imported = manager.Read(stream, "scene.semodel", new SceneTranslatorOptions(), token: null);
        Mesh importedMesh = Assert.Single(imported.GetDescendants<Mesh>());
        Material importedMaterial = Assert.Single(imported.GetDescendants<Material>());

        Assert.Equal("matériel", importedMaterial.Name);
        Assert.Same(importedMaterial, Assert.Single(importedMesh.Materials!));
        Assert.Equal((byte)255, importedMesh.ColorLayers!.Get<byte>(0, 0, 0));
        Assert.Equal((byte)127, importedMesh.ColorLayers.Get<byte>(0, 0, 1));
        Assert.Equal((byte)0, importedMesh.ColorLayers.Get<byte>(0, 0, 2));
        Assert.Equal((byte)255, importedMesh.ColorLayers.Get<byte>(0, 0, 3));
    }
}
