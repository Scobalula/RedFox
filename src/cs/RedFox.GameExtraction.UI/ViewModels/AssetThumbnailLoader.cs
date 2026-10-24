using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using RedFox.GameExtraction.UI.Models;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Avalonia;
using RedFox.Graphics3D.Rendering.Hosting;
using RedFox.Imaging;

namespace RedFox.GameExtraction.UI.ViewModels;

internal sealed class AssetThumbnailLoader(AssetManager assetManager, Func<GameExtractionConfiguration> configurationFactory, PreviewSettings previewSettings, ThumbnailCache cache)
{
    private const int CacheCapacity		= 1024;
    private const int OrbitViewCount	= 12;

    private static readonly TimeSpan OrbitFrameInterval					= TimeSpan.FromMilliseconds(150);
    private static readonly System.Numerics.Vector3 FrontViewDirection	= System.Numerics.Vector3.Normalize(new System.Numerics.Vector3(0.0f, -0.25f, -1.0f));

    private readonly List<AssetRowViewModel>											_pending	= [];
    private readonly HashSet<AssetRowViewModel>											_failed		= [];
    private readonly LinkedList<AssetRowViewModel>										_cacheOrder	= [];
    private readonly Dictionary<AssetRowViewModel, LinkedListNode<AssetRowViewModel>>	_cacheNodes	= [];

    private CancellationTokenSource	_cancellation		= new();
    private CancellationTokenSource	_orbitCancellation	= new();
    private Bitmap[]				_orbitFrames		= [];
    private DispatcherTimer?		_orbitTimer;
    private AssetRowViewModel?		_loading;
    private AssetRowViewModel?		_orbiting;
    private bool					_isRunning;

    public AvaloniaOpenGlThumbnailRenderer? Renderer { get; set; }

    public void Request(AssetRowViewModel row)
    {
        if (Renderer is null || !SupportsThumbnail(row.Category) || _failed.Contains(row) || ReferenceEquals(row, _loading) || _pending.Contains(row))
        {
            return;
        }

        if (_cacheNodes.TryGetValue(row, out LinkedListNode<AssetRowViewModel>? node))
        {
            _cacheOrder.Remove(node);
            _cacheOrder.AddLast(node);
            return;
        }

        _pending.Add(row);

        if (!_isRunning)
        {
            _ = ProcessPendingAsync();
        }
    }

    public void Cancel(AssetRowViewModel row)
    {
        _pending.Remove(row);
    }

    public void Orbit(AssetRowViewModel? row)
    {
        if (ReferenceEquals(row, _orbiting))
        {
            return;
        }

        StopOrbit();

        if (row is not null && Renderer is not null && row.Category == AssetCategory.Model && !_failed.Contains(row))
        {
            _orbiting = row;
            _ = StartOrbitAsync(row, _orbitCancellation.Token);
        }
    }

    public void Clear()
    {
        StopOrbit();
        _cancellation.Cancel();
        _cancellation = new();
        _pending.Clear();
        _failed.Clear();

        foreach (AssetRowViewModel row in _cacheOrder)
        {
            ReleaseThumbnail(row);
        }

        _cacheOrder.Clear();
        _cacheNodes.Clear();
    }

    private static bool SupportsThumbnail(AssetCategory category) => category is AssetCategory.Image or AssetCategory.Model or AssetCategory.Material;

    private static void ReleaseThumbnail(AssetRowViewModel row)
    {
        Bitmap? thumbnail	= row.Thumbnail;
        row.Thumbnail		= null;
        thumbnail?.Dispose();
    }

    private static void DisposeAll(Bitmap[] bitmaps)
    {
        foreach (Bitmap bitmap in bitmaps)
        {
            bitmap.Dispose();
        }
    }

    private static string? GetFileStamp(string path)
    {
        FileInfo file = new(path);
        return file.Exists ? $"{file.FullName}|{file.Length}|{file.LastWriteTimeUtc.Ticks}" : null;
    }

    private static unsafe Bitmap[] CreateBitmaps(ThumbnailCacheImage image)
    {
        Bitmap[] bitmaps	= new Bitmap[image.FrameCount];
        PixelSize size		= new(image.Width, image.Height);
        int frameLength		= image.Width * image.Height * 4;

        fixed (byte* pixels = image.Pixels)
        {
            for (int i = 0; i < bitmaps.Length; i++)
            {
                bitmaps[i] = new Bitmap(PixelFormats.Rgba8888, AlphaFormat.Premul, (nint)(pixels + i * frameLength), size, new Vector(96, 96), image.Width * 4);
            }
        }

        return bitmaps;
    }

    private static unsafe ThumbnailCacheImage CopyPixels(Bitmap[] bitmaps)
    {
        PixelSize size	= bitmaps[0].PixelSize;
        int frameLength	= size.Width * size.Height * 4;
        byte[] pixels	= new byte[frameLength * bitmaps.Length];

        fixed (byte* destination = pixels)
        {
            for (int i = 0; i < bitmaps.Length; i++)
            {
                bitmaps[i].CopyPixels(new PixelRect(size), (nint)(destination + i * frameLength), frameLength, size.Width * 4);
            }
        }

        return new ThumbnailCacheImage(size.Width, size.Height, bitmaps.Length, pixels);
    }

    private UInt128? CreateCacheKey(AssetRowViewModel row, SceneUpAxis upAxis, int viewCount)
    {
        Asset asset					= row.Asset;
        AssetSourceRequest? request	= row.Source.Request;
        string? stamp				= request switch
        {
            { Kind: AssetSourceKind.File, Location: { } location }		=> GetFileStamp(location),
            { Kind: AssetSourceKind.Directory, Location: { } location }	=> GetFileStamp(Path.Combine(location, asset.Name)),
            _															=> null,
        };

        // Without a file to stamp, only a loader-provided key can tell when the content has changed.
        if (stamp is null && asset.ThumbnailKey is null)
        {
            return null;
        }

        string axis = asset.Category == AssetCategory.Model ? upAxis.ToString() : string.Empty;
        return ThumbnailCache.CreateKey($"{stamp}|{request?.ProcessName}|{asset.Type}|{asset.Name}|{asset.ThumbnailKey}|{axis}|{viewCount}|{Renderer?.ThumbnailSize}");
    }

    // The orbit is rendered and cached with the still, so selecting a model never waits on the renderer.
    private async Task<Bitmap?> RenderModelThumbnailAsync(AvaloniaOpenGlThumbnailRenderer renderer, SceneViewportController viewport, UInt128? cacheKey, UInt128? orbitKey)
    {
        Bitmap[]? views = await renderer.RenderSceneAsync(viewport, orbitKey is null ? 1 : OrbitViewCount).ConfigureAwait(true);

        if (views is null)
        {
            return null;
        }

        StoreInCache(cacheKey, [views[0]]);
        StoreInCache(orbitKey, views);
        DisposeAll(views[1..]);
        return views[0];
    }

    private void StoreInCache(UInt128? cacheKey, Bitmap[]? bitmaps)
    {
        if (cacheKey is { } key && bitmaps is { Length: > 0 })
        {
            ThumbnailCacheImage image = CopyPixels(bitmaps);
            _ = Task.Run(() => cache.Store(key, image));
        }
    }

    private SceneViewportController? CreateViewport(Scene scene, SceneUpAxis upAxis, CancellationToken cancellationToken)
    {
        scene.UpAxis = upAxis;
        scene.TextureMipType = MipType.UltraLow;
        scene.ImageTranslators.RegisterRange(assetManager.GetRequiredService<ImageTranslatorService>().Manager.Translators);

        foreach (SceneNode node in scene.EnumerateDescendants())
        {
            cancellationToken.ThrowIfCancellationRequested();
            node.ResetLiveTransform();

            if (node is SkeletonBone bone)
            {
                bone.ShowSkeletonBone = false;
            }
            else if (node is Mesh { Normals: not { ElementCount: > 0 } } mesh)
            {
                mesh.GenerateNormals(cancellationToken);
            }
        }

        OrbitCamera camera					= new("ThumbnailCamera") { AspectRatio = 1.0f };
        SceneViewportController viewport	= new(scene, camera) { IncludeNodeInBounds = node => node is Mesh, FitViewDirection = FrontViewDirection };

        if (!viewport.RecomputeBounds())
        {
            return null;
        }

        viewport.FitCameraToScene();
        return viewport;
    }

    private async Task ProcessPendingAsync()
    {
        _isRunning = true;

        while (_pending.Count > 0)
        {
            // Newest requests first, so the tiles scrolled to most recently fill in before older ones.
            AssetRowViewModel row = _pending[^1];
            _pending.RemoveAt(_pending.Count - 1);

            CancellationToken cancellationToken			= _cancellation.Token;
            GameExtractionConfiguration configuration	= configurationFactory();
            SceneUpAxis upAxis							= previewSettings.UpAxis;
            UInt128? cacheKey							= null;
            UInt128? orbitKey							= null;
            Bitmap? thumbnail							= null;
            _loading									= row;

            // Keys stat the source file, so they are created with the read rather than on the UI thread.
            object? source = await Task.Run(() =>
            {
                cacheKey = CreateCacheKey(row, upAxis, 1);
                orbitKey = row.Category == AssetCategory.Model ? CreateCacheKey(row, upAxis, OrbitViewCount) : null;
                return ReadThumbnailSourceAsync(row.Asset, cacheKey, configuration, upAxis, cancellationToken);
            }).ConfigureAwait(true);

            try
            {
                if (source is Bitmap[] cached)
                {
                    thumbnail = cached[0];
                }
                else if (source is Image image && Renderer is not null)
                {
                    thumbnail = await Renderer.RenderImageAsync(image).ConfigureAwait(true);
                    StoreInCache(cacheKey, thumbnail is null ? null : [thumbnail]);
                }
                else if (source is SceneViewportController viewport && Renderer is not null)
                {
                    thumbnail = await RenderModelThumbnailAsync(Renderer, viewport, cacheKey, orbitKey).ConfigureAwait(true);
                }
            }
            catch (Exception)
            {
                // Assets that cannot be rendered keep their category icon.
            }

            _loading = null;

            if (cancellationToken.IsCancellationRequested)
            {
                thumbnail?.Dispose();
            }
            else if (thumbnail is null)
            {
                _failed.Add(row);
            }
            else
            {
                AddToCache(row, thumbnail);
            }
        }

        _isRunning = false;
    }

    private async Task StartOrbitAsync(AssetRowViewModel row, CancellationToken cancellationToken)
    {
        GameExtractionConfiguration configuration	= configurationFactory();
        SceneUpAxis upAxis							= previewSettings.UpAxis;
        UInt128? cacheKey							= null;
        Bitmap[]? frames							= null;

        object? source = await Task.Run(() =>
        {
            cacheKey = CreateCacheKey(row, upAxis, OrbitViewCount);
            return ReadThumbnailSourceAsync(row.Asset, cacheKey, configuration, upAxis, cancellationToken);
        }).ConfigureAwait(true);

        try
        {
            if (source is Bitmap[] cached)
            {
                frames = cached;
            }
            else if (source is SceneViewportController viewport && Renderer is not null)
            {
                frames = await Renderer.RenderSceneAsync(viewport, OrbitViewCount).ConfigureAwait(true);
                StoreInCache(cacheKey, frames);
            }
        }
        catch (Exception)
        {
            // A model that cannot be rendered keeps its still thumbnail.
        }

        if (frames is null)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            DisposeAll(frames);
            return;
        }

        int frameIndex	= 0;
        _orbitFrames	= frames;
        row.OrbitFrame	= frames[0];
        _orbitTimer		= new DispatcherTimer(OrbitFrameInterval, DispatcherPriority.Background, (_, _) => row.OrbitFrame = frames[++frameIndex % frames.Length]);
    }

    private void StopOrbit()
    {
        _orbitCancellation.Cancel();
        _orbitCancellation = new();
        _orbitTimer?.Stop();
        _orbitTimer = null;

        if (_orbiting is not null)
        {
            _orbiting.OrbitFrame = null;
            _orbiting = null;
        }

        DisposeAll(_orbitFrames);
        _orbitFrames = [];
    }

    private async Task<object?> ReadThumbnailSourceAsync(Asset asset, UInt128? cacheKey, GameExtractionConfiguration configuration, SceneUpAxis upAxis, CancellationToken cancellationToken)
    {
        try
        {
            if (cacheKey is { } key && cache.TryGet(key, out ThumbnailCacheImage image))
            {
                return CreateBitmaps(image);
            }

            AssetReadResult result = await assetManager.ReadAsync(asset, configuration, AssetReadPurpose.Thumbnail, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            return result.Data switch
            {
                Image data					=> data,
                Texture texture				=> texture.Data ?? texture.ImageLoader?.Load(texture, assetManager.GetRequiredService<ImageTranslatorService>().Manager, MipType.Low),
                Scene scene					=> CreateViewport(scene, upAxis, cancellationToken),
                IEnumerable<Scene> scenes	=> scenes.FirstOrDefault() is { } scene ? CreateViewport(scene, upAxis, cancellationToken) : null,
                _							=> null,
            };
        }
        catch (Exception)
        {
            // Caught here so an asset that cannot be read never rethrows on the UI thread, where it would stall scrolling.
            return null;
        }
    }

    private void AddToCache(AssetRowViewModel row, Bitmap thumbnail)
    {
        row.Thumbnail		= thumbnail;
        _cacheNodes[row]	= _cacheOrder.AddLast(row);

        if (_cacheOrder.Count > CacheCapacity)
        {
            AssetRowViewModel evicted = _cacheOrder.First!.Value;
            _cacheOrder.RemoveFirst();
            _cacheNodes.Remove(evicted);
            ReleaseThumbnail(evicted);
        }
    }
}
