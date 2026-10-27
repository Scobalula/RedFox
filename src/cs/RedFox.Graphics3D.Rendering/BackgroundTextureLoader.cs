using System.Collections.Concurrent;
using RedFox.Imaging;
using RedFox.Imaging.IO;

namespace RedFox.Graphics3D.Rendering;

internal sealed class BackgroundTextureLoader : IDisposable
{
    private readonly ConcurrentQueue<CompletedTextureLoad> _completedLoads = new();
    private int _inFlightLoads;
    private volatile bool _disposed;

    public int Concurrency { get; set; } = Math.Clamp(Environment.ProcessorCount / 2, 2, 4);

    public bool HasPendingLoads => Volatile.Read(ref _inFlightLoads) > 0 || !_completedLoads.IsEmpty;

    public void QueueLoads(IReadOnlyList<SceneNode> nodes, Scene scene)
    {
        for (int i = 0; i < nodes.Count && Volatile.Read(ref _inFlightLoads) < Concurrency; i++)
        {
            if (nodes[i] is Texture texture)
            {
                QueueLoad(texture, scene);
            }
        }

        if (scene.Skybox.Texture is { } skyboxTexture && Volatile.Read(ref _inFlightLoads) < Concurrency)
        {
            QueueLoad(skyboxTexture, scene);
        }
    }

    public void ApplyCompleted()
    {
        while (_completedLoads.TryDequeue(out CompletedTextureLoad completedLoad))
        {
            if (completedLoad.Texture.Data is null)
            {
                completedLoad.Texture.Data = completedLoad.Image;
            }
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _completedLoads.Clear();
    }

    private void QueueLoad(Texture texture, Scene scene)
    {
        if (_disposed || texture.Data is not null || texture.LoadAttempted || texture.ImageLoader is not { } imageLoader)
        {
            return;
        }

        texture.LoadAttempted = true;
        if (string.IsNullOrWhiteSpace(texture.EffectiveFilePath))
        {
            return;
        }

        ImageTranslatorManager	translatorManager	= scene.ImageTranslators;
        MipType					mipType				= scene.TextureMipType;

        Interlocked.Increment(ref _inFlightLoads);
        ThreadPool.QueueUserWorkItem(_ => Load(texture, imageLoader, translatorManager, mipType));
    }

    private void Load(Texture texture, ITextureLoader imageLoader, ImageTranslatorManager translatorManager, MipType mipType)
    {
        Image? image = TryLoadImage(imageLoader, texture, translatorManager, mipType);
        if (image is not null && !_disposed)
        {
            _completedLoads.Enqueue(new CompletedTextureLoad(texture, image));
        }

        Interlocked.Decrement(ref _inFlightLoads);
    }

    private static Image? TryLoadImage(ITextureLoader imageLoader, Texture texture, ImageTranslatorManager translatorManager, MipType mipType)
    {
        try
        {
            return imageLoader.Load(texture, translatorManager, mipType);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
