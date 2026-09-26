using RedFox.Imaging;
using RedFox.Imaging.Primitives;
using RedFox.Imaging.IO;
using RedFox.Imaging.Formats.Png;
using RedFox.Graphics3D;

namespace RedFox.Tests.Graphics3D;

public sealed class TextureImageLoadingTests
{
    [Fact]
    public void TryLoad_UsesResolvedPathAndSceneImageTranslators()
    {
        string imagePath = Path.Combine(Path.GetTempPath(), $"redfox-texture-{Guid.NewGuid():N}.png");

        try
        {
            ImageTranslatorManager writerManager = new();
            writerManager.Register(new PngImageTranslator());
            Image source = new(1, 1, ImageFormat.R8G8B8A8Unorm, [255, 0, 0, 255]);
            writerManager.Write(imagePath, source);

            Scene scene = new();
            scene.ImageTranslators.Register(new PngImageTranslator());
            Texture texture = scene.RootNode.AddNode(new Texture("textures/red.png")
            {
                ResolvedFilePath = imagePath,
            });

            bool loaded = texture.TryLoad(scene.ImageTranslators);

            Assert.True(loaded);
            Assert.NotNull(texture.Data);
            Assert.Equal(1, texture.Data.Width);
            Assert.Equal(1, texture.Data.Height);
            Assert.Equal(ImageFormat.R8G8B8A8Unorm, texture.Data.Format);
        }
        finally
        {
            if (File.Exists(imagePath))
            {
                File.Delete(imagePath);
            }
        }
    }
}
