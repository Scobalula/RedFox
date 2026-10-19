using System.Runtime.InteropServices;
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

    private const int MaxLoadingFrames = 600;

    private readonly Queue<(Image Image, TaskCompletionSource<Bitmap?> Completion)>											_imageRequests	= [];
    private readonly Queue<(SceneViewportController Viewport, int ViewCount, TaskCompletionSource<Bitmap[]?> Completion)>	_sceneRequests	= [];
    private (SceneViewportController Viewport, int ViewCount, TaskCompletionSource<Bitmap[]?> Completion)?					_sceneRequest;

    private int						_loadingFrames;
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
        _graphicsDevice	= new OpenGlGraphicsDevice(_gl);
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
        _sceneRenderer.Initialize();

        // Each scene is released once drawn, which would otherwise recompile shaders for every model.
        ((MaterialTypeRegistry)_graphicsDevice.MaterialTypes).KeepUnusedPipelines = true;
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (_gl is null || _imageRenderer is null || _sceneRenderer is null)
        {
            return;
        }

        while (_imageRequests.TryDequeue(out (Image Image, TaskCompletionSource<Bitmap?> Completion) request))
        {
            try
            {
                request.Completion.TrySetResult(RenderImage(_gl, _imageRenderer, request.Image));
            }
            catch (Exception exception)
            {
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

    private Bitmap? RenderImage(GL gl, ImageRenderer imageRenderer, Image image)
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
        imageRenderer.Render(slice, System.Numerics.Vector2.Zero, zoom, ImageOptions);

        return imageRenderer.IsSliceSupported ? ReadBitmap(gl, width, height) : null;
    }

    private void RenderSceneView(GL gl, SceneRenderer sceneRenderer, SceneViewportController viewport, int viewCount, TaskCompletionSource<Bitmap[]?> completion)
    {
        sceneRenderer.Resize(ThumbnailSize, ThumbnailSize);
        viewport.UpdateAndRender(sceneRenderer, 0.0f);

        if (sceneRenderer.HasPendingWork && ++_loadingFrames < MaxLoadingFrames)
        {
            return;
        }

        Bitmap[] views = new Bitmap[viewCount];

        for (int i = 0; i < viewCount; i++)
        {
            if (i > 0)
            {
                viewport.Camera.YawRadians += MathF.Tau / viewCount;
                viewport.Camera.ApplyOrbit();
                viewport.UpdateAndRender(sceneRenderer, 0.0f);
            }

            views[i] = ReadBitmap(gl, ThumbnailSize, ThumbnailSize);
        }

        completion.TrySetResult(views);
        FinishSceneRequest(sceneRenderer, viewport);
    }

    private void FinishSceneRequest(SceneRenderer sceneRenderer, SceneViewportController viewport)
    {
        _sceneRequest	= null;
        _loadingFrames	= 0;
        sceneRenderer.ReleaseResources(viewport.Scene);
    }

    private Bitmap ReadBitmap(GL gl, int width, int height)
    {
        byte[] pixels = new byte[width * height * 4];

        gl.BindFramebuffer(GLEnum.Framebuffer, _framebuffer);
        gl.ReadPixels(0, 0, (uint)width, (uint)height, GLEnum.Rgba, GLEnum.UnsignedByte, pixels.AsSpan());

        WriteableBitmap bitmap = new(new PixelSize(width, height), new Vector(96, 96), PixelFormats.Rgba8888, AlphaFormat.Premul);
        using ILockedFramebuffer frame = bitmap.Lock();

        // OpenGL rows run bottom to top.
        for (int y = 0; y < height; y++)
        {
            Marshal.Copy(pixels, (height - 1 - y) * width * 4, frame.Address + y * frame.RowBytes, width * 4);
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
