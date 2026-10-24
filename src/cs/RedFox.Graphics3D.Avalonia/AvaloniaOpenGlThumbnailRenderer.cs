using System.Diagnostics;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Platform;
using RedFox.Graphics3D.OpenGL;
using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.Rendering.Hosting;
using RedFox.Graphics3D.Rendering.Materials;
using RedFox.Imaging;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.Avalonia;

// Renders thumbnails offscreen and reads them back. It is hosted as a tiny control because Avalonia only hands out
// OpenGL contexts to controls on platforms that cannot share one, such as ANGLE on Windows.
public sealed class AvaloniaOpenGlThumbnailRenderer : OpenGlControlBase
{
    // Alpha is hidden because game textures often pack other data into it, which would leave the thumbnail looking empty.
    private static readonly ImageViewOptions ImageOptions = new(new System.Numerics.Vector4(1.0f, 1.0f, 1.0f, 0.0f), 0.0f, true, false, false);

    // Avalonia renders this control on the UI thread, so orbit captures stop for the frame once this much time is spent.
    private static readonly TimeSpan CaptureBudget = TimeSpan.FromMilliseconds(4);

    private const int MaxLoadingFrames		= 600;
    private const int UploadBudgetPerFrame	= 1024 * 1024;

    private readonly Queue<(Image Image, TaskCompletionSource<Bitmap?> Completion)>											_imageRequests	= [];
    private readonly Queue<(SceneViewportController Viewport, int ViewCount, TaskCompletionSource<Bitmap[]?> Completion)>	_sceneRequests	= [];
    private readonly List<Task<Bitmap?>>																					_sceneViews		= [];
    private readonly Queue<ThumbnailReadback>																				_readbacks		= [];
    private (SceneViewportController Viewport, int ViewCount, TaskCompletionSource<Bitmap[]?> Completion)?					_sceneRequest;

    private int						_loadingFrames;
    private bool					_supportsAsyncReadback;
    private GL?						_gl;
    private OpenGlGraphicsDevice?	_graphicsDevice;
    private ImageRenderer?			_imageRenderer;
    private SceneRenderer?			_sceneRenderer;
    private uint					_framebuffer;
    private uint					_colorBuffer;
    private uint					_depthBuffer;

    public int ThumbnailSize { get; set; } = 128;

    public Task<Bitmap?> RenderImageAsync(Image image)
    {
        TaskCompletionSource<Bitmap?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _imageRequests.Enqueue((image, completion));
        RequestNextFrameRendering();
        return completion.Task;
    }

    public Task<Bitmap[]?> RenderSceneAsync(SceneViewportController viewport, int viewCount)
    {
        TaskCompletionSource<Bitmap[]?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _sceneRequests.Enqueue((viewport, viewCount, completion));
        RequestNextFrameRendering();
        return completion.Task;
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);

        _gl				= GL.GetApi(gl.GetProcAddress);
        _graphicsDevice	= new OpenGlGraphicsDevice(_gl) { BufferUploadBudgetBytesPerFrame = UploadBudgetPerFrame };
        _imageRenderer	= new ImageRenderer(_graphicsDevice, default);
        _sceneRenderer	= new SceneRenderer(_graphicsDevice, System.Numerics.Vector4.Zero, new System.Numerics.Vector3(0.13f, 0.13f, 0.16f), new System.Numerics.Vector3(-0.4f, -1.0f, -0.2f), System.Numerics.Vector3.One, 0.8f, true, SkinningMode.Linear);
        _colorBuffer	= _gl.GenRenderbuffer();
        _depthBuffer	= _gl.GenRenderbuffer();
        _framebuffer	= _gl.GenFramebuffer();

        _gl.BindRenderbuffer(GLEnum.Renderbuffer, _colorBuffer);
        _gl.RenderbufferStorage(GLEnum.Renderbuffer, GLEnum.Rgba8, (uint)ThumbnailSize, (uint)ThumbnailSize);
        _gl.BindRenderbuffer(GLEnum.Renderbuffer, _depthBuffer);
        _gl.RenderbufferStorage(GLEnum.Renderbuffer, GLEnum.Depth24Stencil8, (uint)ThumbnailSize, (uint)ThumbnailSize);
        _gl.BindFramebuffer(GLEnum.Framebuffer, _framebuffer);
        _gl.FramebufferRenderbuffer(GLEnum.Framebuffer, GLEnum.ColorAttachment0, GLEnum.Renderbuffer, _colorBuffer);
        _gl.FramebufferRenderbuffer(GLEnum.Framebuffer, GLEnum.DepthStencilAttachment, GLEnum.Renderbuffer, _depthBuffer);
        _graphicsDevice.DefaultFramebufferHandle = _framebuffer;
        _sceneRenderer.ShadowQuality = ShadowQuality.Off;
        _sceneRenderer.Initialize();

        // Each scene is released once drawn, which would otherwise recompile shaders for every model.
        ((MaterialTypeRegistry)_graphicsDevice.MaterialTypes).KeepUnusedPipelines = true;

        // Pixel buffers with fences let readbacks finish without stalling the UI thread; older contexts fall back to a blocking read.
        _supportsAsyncReadback = GlVersion.Type == GlProfileType.OpenGLES ? GlVersion.Major >= 3 : GlVersion.Major > 3 || (GlVersion.Major == 3 && GlVersion.Minor >= 2);

        // Shaders compile on first use, which would otherwise stall the UI thread at the first image and model thumbnail of a scroll.
        _imageRenderer.PreparePipeline();
        _sceneRenderer.PreparePipelines();
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (_gl is null || _imageRenderer is null || _sceneRenderer is null)
        {
            return;
        }

        CompleteReadbacks(_gl);

        // One image advances per frame, as a large one can take several frames to upload.
        if (_imageRequests.TryPeek(out (Image Image, TaskCompletionSource<Bitmap?> Completion) request))
        {
            try
            {
                PixelSize size = RenderImage(_imageRenderer, request.Image);

                if (!_imageRenderer.IsTextureUploadPending)
                {
                    _imageRequests.Dequeue();

                    if (_imageRenderer.IsSliceSupported)
                    {
                        ReadBitmap(_gl, size.Width, size.Height, request.Completion);
                    }
                    else
                    {
                        request.Completion.TrySetResult(null);
                    }
                }
            }
            catch (Exception exception)
            {
                _imageRequests.Dequeue();
                request.Completion.TrySetException(exception);
            }
        }

        if (_sceneRequest is null && _sceneRequests.TryDequeue(out (SceneViewportController Viewport, int ViewCount, TaskCompletionSource<Bitmap[]?> Completion) next))
        {
            _sceneRequest = next;
        }

        if (_sceneRequest is { } sceneRequest)
        {
            try
            {
                RenderSceneView(_gl, _sceneRenderer, sceneRequest.Viewport, sceneRequest.ViewCount, sceneRequest.Completion);
            }
            catch (Exception exception)
            {
                sceneRequest.Completion.TrySetException(exception);
                FinishSceneRequest(_sceneRenderer, sceneRequest.Viewport);
            }
        }

        if (_imageRequests.Count > 0 || _sceneRequest is not null || _sceneRequests.Count > 0 || _readbacks.Count > 0)
        {
            RequestNextFrameRendering();
        }

        _gl.BindFramebuffer(GLEnum.Framebuffer, (uint)fb);
        _gl.ClearColor(0.0f, 0.0f, 0.0f, 0.0f);
        _gl.Clear(ClearBufferMask.ColorBufferBit);
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _gl?.DeleteFramebuffer(_framebuffer);
        _gl?.DeleteRenderbuffer(_colorBuffer);
        _gl?.DeleteRenderbuffer(_depthBuffer);
        ReleaseRenderer();
        base.OnOpenGlDeinit(gl);
    }

    protected override void OnOpenGlLost()
    {
        ReleaseRenderer();
        base.OnOpenGlLost();
    }

    private PixelSize RenderImage(ImageRenderer imageRenderer, Image image)
    {
        // Render the smallest mip that still covers the thumbnail, so linear filtering never minifies by more than half.
        int mipLevel = 0;
        while (mipLevel + 1 < image.MipLevels && Math.Max(image.Width >> (mipLevel + 1), image.Height >> (mipLevel + 1)) >= ThumbnailSize)
        {
            mipLevel++;
        }

        ImageSlice slice	= image.GetSlice(mipLevel);
        float zoom			= Math.Min(1.0f, (float)ThumbnailSize / Math.Max(slice.Width, slice.Height));
        int width			= Math.Max(1, (int)(slice.Width * zoom));
        int height			= Math.Max(1, (int)(slice.Height * zoom));

        imageRenderer.Resize(width, height);

        // Without a fallback slice the upload is held to the device's per-frame budget, so a large image without mips uploads over several frames.
        imageRenderer.Render(slice, null, System.Numerics.Vector2.Zero, zoom, ImageOptions);

        return new PixelSize(width, height);
    }

    private void RenderSceneView(GL gl, SceneRenderer sceneRenderer, SceneViewportController viewport, int viewCount, TaskCompletionSource<Bitmap[]?> completion)
    {
        long frameStart = Stopwatch.GetTimestamp();

        sceneRenderer.Resize(ThumbnailSize, ThumbnailSize);
        viewport.UpdateAndRender(sceneRenderer, 0.0f);

        if (sceneRenderer.HasPendingWork && ++_loadingFrames < MaxLoadingFrames)
        {
            return;
        }

        while (true)
        {
            TaskCompletionSource<Bitmap?> view = new(TaskCreationOptions.RunContinuationsAsynchronously);
            ReadBitmap(gl, ThumbnailSize, ThumbnailSize, view);
            _sceneViews.Add(view.Task);

            if (_sceneViews.Count == viewCount)
            {
                _ = CompleteSceneAsync([.. _sceneViews], completion);
                _sceneViews.Clear();
                FinishSceneRequest(sceneRenderer, viewport);
                return;
            }

            viewport.Camera.YawRadians += MathF.Tau / viewCount;
            viewport.Camera.ApplyOrbit();

            // The next frame renders the rotated view before carrying on.
            if (Stopwatch.GetElapsedTime(frameStart) >= CaptureBudget)
            {
                return;
            }

            viewport.UpdateAndRender(sceneRenderer, 0.0f);
        }
    }

    private void FinishSceneRequest(SceneRenderer sceneRenderer, SceneViewportController viewport)
    {
        DisposeSceneViews();
        _sceneRequest	= null;
        _loadingFrames	= 0;
        sceneRenderer.ReleaseResources(viewport.Scene);
    }

    // Views left here belong to a capture that failed or was abandoned part way through.
    private void DisposeSceneViews()
    {
        _ = DisposeViewsAsync([.. _sceneViews]);
        _sceneViews.Clear();
    }

    private static async Task CompleteSceneAsync(Task<Bitmap?>[] views, TaskCompletionSource<Bitmap[]?> completion)
    {
        Bitmap?[] bitmaps = await Task.WhenAll(views).ConfigureAwait(true);

        if (bitmaps.Any(bitmap => bitmap is null) || !completion.TrySetResult([.. bitmaps.OfType<Bitmap>()]))
        {
            completion.TrySetResult(null);
            DisposeAll(bitmaps);
        }
    }

    private static async Task DisposeViewsAsync(Task<Bitmap?>[] views)
    {
        DisposeAll(await Task.WhenAll(views).ConfigureAwait(true));
    }

    private static void DisposeAll(Bitmap?[] bitmaps)
    {
        foreach (Bitmap? bitmap in bitmaps)
        {
            bitmap?.Dispose();
        }
    }

    // A blocking read waits for the GPU to finish everything queued, including the main preview's frames, so reads are
    // copied into a pixel buffer and collected on a later frame once their fence has signalled.
    private unsafe void ReadBitmap(GL gl, int width, int height, TaskCompletionSource<Bitmap?> completion)
    {
        int length = width * height * 4;

        gl.BindFramebuffer(GLEnum.Framebuffer, _framebuffer);

        if (!_supportsAsyncReadback)
        {
            byte[] pixels = new byte[length];
            gl.ReadPixels(0, 0, (uint)width, (uint)height, GLEnum.Rgba, GLEnum.UnsignedByte, pixels.AsSpan());

            fixed (byte* source = pixels)
            {
                completion.TrySetResult(CreateBitmap(source, width, height));
            }

            return;
        }

        uint buffer = gl.GenBuffer();
        gl.BindBuffer(GLEnum.PixelPackBuffer, buffer);
        gl.BufferData(GLEnum.PixelPackBuffer, (nuint)length, null, GLEnum.StreamRead);
        gl.ReadPixels(0, 0, (uint)width, (uint)height, GLEnum.Rgba, GLEnum.UnsignedByte, null);
        gl.BindBuffer(GLEnum.PixelPackBuffer, 0);

        nint fence = gl.FenceSync(GLEnum.SyncGpuCommandsComplete, 0u);
        gl.Flush();
        _readbacks.Enqueue(new ThumbnailReadback(buffer, fence, width, height, completion));
    }

    private unsafe void CompleteReadbacks(GL gl)
    {
        // Fences signal in submission order, so collection stops at the first read still in flight.
        while (_readbacks.TryPeek(out ThumbnailReadback readback))
        {
            GLEnum status = gl.ClientWaitSync(readback.Fence, 0u, 0);

            if (status == GLEnum.TimeoutExpired)
            {
                return;
            }

            _readbacks.Dequeue();
            Bitmap? bitmap = null;

            if (status is GLEnum.AlreadySignaled or GLEnum.ConditionSatisfied)
            {
                gl.BindBuffer(GLEnum.PixelPackBuffer, readback.Buffer);
                byte* pixels = (byte*)gl.MapBufferRange(GLEnum.PixelPackBuffer, 0, (nuint)(readback.Width * readback.Height * 4), (uint)GLEnum.MapReadBit);

                if (pixels is not null)
                {
                    bitmap = CreateBitmap(pixels, readback.Width, readback.Height);
                    gl.UnmapBuffer(GLEnum.PixelPackBuffer);
                }

                gl.BindBuffer(GLEnum.PixelPackBuffer, 0);
            }

            gl.DeleteSync(readback.Fence);
            gl.DeleteBuffer(readback.Buffer);
            readback.Completion.TrySetResult(bitmap);
        }
    }

    private void ReleaseReadbacks()
    {
        foreach (ThumbnailReadback readback in _readbacks)
        {
            _gl?.DeleteSync(readback.Fence);
            _gl?.DeleteBuffer(readback.Buffer);
            readback.Completion.TrySetResult(null);
        }

        _readbacks.Clear();
    }

    private static unsafe Bitmap CreateBitmap(byte* pixels, int width, int height)
    {
        WriteableBitmap bitmap = new(new PixelSize(width, height), new Vector(96, 96), PixelFormats.Rgba8888, AlphaFormat.Premul);
        using ILockedFramebuffer frame = bitmap.Lock();
        int rowLength = width * 4;

        // OpenGL rows run bottom to top.
        for (int y = 0; y < height; y++)
        {
            System.Buffer.MemoryCopy(pixels + (height - 1 - y) * rowLength, (byte*)frame.Address + y * frame.RowBytes, frame.RowBytes, rowLength);
        }

        return bitmap;
    }

    private void ReleaseRenderer()
    {
        while (_imageRequests.TryDequeue(out (Image Image, TaskCompletionSource<Bitmap?> Completion) request))
        {
            request.Completion.TrySetResult(null);
        }

        while (_sceneRequests.TryDequeue(out (SceneViewportController Viewport, int ViewCount, TaskCompletionSource<Bitmap[]?> Completion) request))
        {
            request.Completion.TrySetResult(null);
        }

        _sceneRequest?.Completion.TrySetResult(null);
        ReleaseReadbacks();
        DisposeSceneViews();
        _sceneRequest	= null;
        _loadingFrames	= 0;
        _imageRenderer?.Dispose();
        _imageRenderer = null;
        _sceneRenderer?.Dispose();
        _sceneRenderer = null;
        (_graphicsDevice?.MaterialTypes as MaterialTypeRegistry)?.ReleaseUnusedPipelines();
        _graphicsDevice?.Dispose();
        _graphicsDevice = null;
        _gl = null;
    }
}
