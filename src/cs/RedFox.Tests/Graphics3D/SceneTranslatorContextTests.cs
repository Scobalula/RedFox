using RedFox.Graphics3D.IO;

namespace RedFox.Tests.Graphics3D;

public sealed class SceneTranslatorContextTests
{
    [Fact]
    public void CreateReadContext_StoresPathsWithoutMutatingSharedOptions()
    {
        SceneTranslatorOptions options = new();
        string firstPath = Path.Combine(Path.GetTempPath(), "first", "model.obj");
        string secondPath = Path.Combine(Path.GetTempPath(), "second", "model.obj");

        SceneTranslationContext firstContext = SceneTranslator.CreateReadContext(firstPath, options);
        SceneTranslationContext secondContext = SceneTranslator.CreateReadContext(secondPath, options);

        Assert.Equal(Path.GetFullPath(firstPath), firstContext.SourceFilePath);
        Assert.Equal(Path.GetDirectoryName(Path.GetFullPath(firstPath)), firstContext.SourceDirectoryPath);
        Assert.Equal(Path.GetFullPath(secondPath), secondContext.SourceFilePath);
        Assert.Equal(Path.GetDirectoryName(Path.GetFullPath(secondPath)), secondContext.SourceDirectoryPath);
        Assert.Null(options.SourceFilePath);
        Assert.Null(options.SourceDirectoryPath);
    }
}
