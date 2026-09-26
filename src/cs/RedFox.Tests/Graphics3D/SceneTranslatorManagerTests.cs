using System.Text;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Formats;
using RedFox.Graphics3D.Groups;
using RedFox.Graphics3D.IO;

namespace RedFox.Tests.Graphics3D;

public sealed class SceneTranslatorManagerTests
{
    [Fact]
    public void RegisterAll_RegistersEveryBuiltInFormat()
    {
        SceneTranslatorManager manager = new();

        BuiltInSceneFormats.RegisterAll(manager);

        Assert.Equal(["ActorX PSA", "ActorX PSK", "BiovisionHierarchy", "Cast", "IdTech4MD5Anim", "IdTech4MD5Mesh", "KaydaraFBX", "MayaASCII", "SEAnim", "SEModel", "ValveSMD", "WavefrontOBJ", "XAsset XAnim", "XAsset XModel", "glTF"], manager.Translators.Select(translator => translator.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Register_ReplacesTranslatorWithSameName()
    {
        SceneTranslatorManager manager = CreateManager();
        StubSceneTranslator replacement = new("Stub", ".other", canRead: true, canWrite: true);

        manager.Register(replacement);

        Assert.Same(replacement, Assert.Single(manager.Translators, translator => translator.Name == "Stub"));
    }

    [Fact]
    public void Read_MatchesExtensionCaseInsensitively()
    {
        Scene scene = Read(CreateManager(), [.. StubSceneTranslator.Signature], "MODEL.STUB", new SceneTranslatorOptions());

        Assert.True(scene.TryFindChild("Stub", out _));
    }

    [Fact]
    public void Read_UnknownExtension_FallsBackToContentSignature()
    {
        Scene scene = Read(CreateManager(), [.. StubSceneTranslator.Signature], "model.bin", new SceneTranslatorOptions());

        Assert.True(scene.TryFindChild("Stub", out _));
    }

    [Fact]
    public void Read_ExtensionWithWrongSignature_ThrowsDescriptiveError()
    {
        IOException exception = Assert.Throws<IOException>(() => Read(CreateManager(), "nope"u8.ToArray(), "model.stub", new SceneTranslatorOptions()));

        Assert.Contains("model.stub", exception.Message);
        Assert.Contains("signature expected by Stub", exception.Message);
    }

    [Fact]
    public void Read_WriteOnlyFormat_ThrowsDescriptiveError()
    {
        IOException exception = Assert.Throws<IOException>(() => Read(CreateManager(), "text"u8.ToArray(), "scene.wo", new SceneTranslatorOptions()));

        Assert.Contains("handled by WriteOnly, which cannot read them", exception.Message);
    }

    [Fact]
    public void Write_UnknownExtension_ThrowsDescriptiveError()
    {
        IOException exception = Assert.Throws<IOException>(() => Write(CreateManager(), new Scene(), "scene.nope", new SceneTranslatorOptions()));

        Assert.Contains("no registered translator handles '.nope' files", exception.Message);
    }

    [Fact]
    public void TranslatorName_OverridesDetectionForReadAndWrite()
    {
        SceneTranslatorManager manager = CreateManager();
        SceneTranslatorOptions options = new() { TranslatorName = "stub" };

        byte[] data = Write(manager, new Scene(), "scene.dat", options);
        Scene scene = Read(manager, data, "scene.dat", options);

        Assert.Equal(StubSceneTranslator.Signature.ToArray(), data);
        Assert.True(scene.TryFindChild("Stub", out _));
    }

    [Fact]
    public void TranslatorName_WithoutCapability_ThrowsDescriptiveError()
    {
        IOException exception = Assert.Throws<IOException>(() => Read(CreateManager(), [.. StubSceneTranslator.Signature], "scene.dat", new SceneTranslatorOptions { TranslatorName = "WriteOnly" }));

        Assert.Contains("translator 'WriteOnly' does not support reading", exception.Message);
    }

    [Fact]
    public void Read_BuiltInFormatWithWrongExtension_IsDetectedByContent()
    {
        SceneTranslatorManager manager = new();
        BuiltInSceneFormats.RegisterAll(manager);

        Scene source = new("Source");
        MeshGroup model = source.RootNode.AddNode(new MeshGroup("Model"));
        Mesh mesh = model.AddNode(new Mesh { Name = "Mesh" });
        mesh.Positions = new DataBuffer<float>([0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f], 1, 3);
        mesh.Normals = new DataBuffer<float>([0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f, 1f], 1, 3);
        mesh.UVLayers = new DataBuffer<float>([0f, 0f, 1f, 0f, 0f, 1f], 1, 2);
        mesh.FaceIndices = new DataBuffer<int>([0, 1, 2], 1, 1);

        byte[] data = Write(manager, source, "model.semodel", new SceneTranslatorOptions());
        Scene loaded = Read(manager, data, "model.bin", new SceneTranslatorOptions());

        Mesh loadedMesh = Assert.Single(loaded.GetDescendants<Mesh>());
        Assert.Equal(3, loadedMesh.VertexCount);
        Assert.Equal(Encoding.ASCII.GetBytes("SEModel"), data[..7]);
    }

    private static SceneTranslatorManager CreateManager()
    {
        SceneTranslatorManager manager = new();
        manager.Register(new StubSceneTranslator("Stub", ".stub", canRead: true, canWrite: true));
        manager.Register(new StubSceneTranslator("WriteOnly", ".wo", canRead: false, canWrite: true));
        return manager;
    }

    private static Scene Read(SceneTranslatorManager manager, byte[] data, string filePath, SceneTranslatorOptions options)
    {
        using MemoryStream stream = new(data, writable: false);
        return manager.Read(stream, filePath, options);
    }

    private static byte[] Write(SceneTranslatorManager manager, Scene scene, string filePath, SceneTranslatorOptions options)
    {
        using MemoryStream stream = new();
        manager.Write(stream, filePath, scene, options);
        return stream.ToArray();
    }
}
