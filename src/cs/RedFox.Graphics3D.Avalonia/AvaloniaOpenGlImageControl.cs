using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Rendering;
using Avalonia.Threading;
using RedFox.Avalonia.Themes;
using RedFox.Graphics3D.OpenGL;
using RedFox.Graphics3D.Rendering;
using RedFox.Imaging;
using Silk.NET.OpenGL;
using System.Numerics;

namespace RedFox.Graphics3D.Avalonia;

/// <summary>
/// Hosts the RedFox OpenGL image renderer inside an Avalonia OpenGL control with zoom and pan input.
/// </summary>
public sealed class AvaloniaOpenGlImageControl : OpenGlControlBase, ICustomHitTest
{
    private const double MinimumZoom = 1.0 / 64.0;
    private const double MaximumZoom = 256.0;
    private const double WheelZoomFactor = 1.2;

    /// <summary>
    /// Defines the <see cref="Slice"/> property.
    /// </summary>
    public static readonly StyledProperty<ImageSlice?> SliceProperty =
        AvaloniaProperty.Register<AvaloniaOpenGlImageControl, ImageSlice?>(nameof(Slice));

    /// <summary>
    /// Defines the <see cref="FallbackSlice"/> property.
    /// </summary>
    public static readonly StyledProperty<ImageSlice?> FallbackSliceProperty =
        AvaloniaProperty.Register<AvaloniaOpenGlImageControl, ImageSlice?>(nameof(FallbackSlice));

    /// <summary>
    /// Defines the <see cref="Options"/> property.
    /// </summary>
    public static readonly StyledProperty<ImageViewOptions> OptionsProperty =
        AvaloniaProperty.Register<AvaloniaOpenGlImageControl, ImageViewOptions>(nameof(Options), ImageViewOptions.Default);

    /// <summary>
    /// Defines the <see cref="ClearColor"/> property.
    /// </summary>
    public static readonly StyledProperty<Vector4> ClearColorProperty =
        AvaloniaProperty.Register<AvaloniaOpenGlImageControl, Vector4>(nameof(ClearColor), RedFoxThemeColors.SceneBackgroundVector);

    /// <summary>
    /// Defines the <see cref="Zoom"/> property.
    /// </summary>
    public static readonly DirectProperty<AvaloniaOpenGlImageControl, double> ZoomProperty =
        AvaloniaProperty.RegisterDirect<AvaloniaOpenGlImageControl, double>(nameof(Zoom), control => control.Zoom);

    /// <summary>
    /// Defines the <see cref="PointerTexel"/> property.
    /// </summary>
    public static readonly DirectProperty<AvaloniaOpenGlImageControl, PixelPoint?> PointerTexelProperty =
        AvaloniaProperty.RegisterDirect<AvaloniaOpenGlImageControl, PixelPoint?>(nameof(PointerTexel), control => control.PointerTexel, (control, value) => control.PointerTexel = value);

    /// <summary>
    /// Defines the <see cref="IsSliceSupported"/> property.
    /// </summary>
    public static readonly DirectProperty<AvaloniaOpenGlImageControl, bool> IsSliceSupportedProperty =
        AvaloniaProperty.RegisterDirect<AvaloniaOpenGlImageControl, bool>(nameof(IsSliceSupported), control => control.IsSliceSupported);

    private OpenGlGraphicsDevice? _graphicsDevice;
    private ImageRenderer? _renderer;
    private double _zoom = 1.0;
    private Vector2 _offset;
    private PixelPoint? _pointerTexel;
    private bool _isSliceSupported = true;
    private bool _needsFit = true;
    private bool _isPanning;
    private Point _lastPanPosition;

    static AvaloniaOpenGlImageControl()
    {
        SliceProperty.Changed.AddClassHandler<AvaloniaOpenGlImageControl>((control, e) => control.OnSliceChanged(e));
        FallbackSliceProperty.Changed.AddClassHandler<AvaloniaOpenGlImageControl>((control, _) => control.RequestNextFrameRendering());
        OptionsProperty.Changed.AddClassHandler<AvaloniaOpenGlImageControl>((control, e) => control.OnOptionsChanged(e));
        ClearColorProperty.Changed.AddClassHandler<AvaloniaOpenGlImageControl>((control, _) => control.RequestNextFrameRendering());
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaOpenGlImageControl"/> class.
    /// </summary>
    public AvaloniaOpenGlImageControl()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    /// <summary>
    /// Gets or sets the image slice to display.
    /// </summary>
    public ImageSlice? Slice
    {
        get => GetValue(SliceProperty);
        set => SetValue(SliceProperty, value);
    }

    /// <summary>
    /// Gets or sets the lower-resolution slice displayed while the selected slice is uploaded.
    /// </summary>
    public ImageSlice? FallbackSlice
    {
        get => GetValue(FallbackSliceProperty);
        set => SetValue(FallbackSliceProperty, value);
    }

    /// <summary>
    /// Gets or sets the channel, exposure, normal, tiling, and orientation options.
    /// </summary>
    public ImageViewOptions Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    /// <summary>
    /// Gets or sets the color drawn behind the image.
    /// </summary>
    public Vector4 ClearColor
    {
        get => GetValue(ClearColorProperty);
        set => SetValue(ClearColorProperty, value);
    }

    /// <summary>
    /// Gets the number of physical pixels covered by one texel.
    /// </summary>
    public double Zoom
    {
        get => _zoom;
        private set => SetAndRaise(ZoomProperty, ref _zoom, Math.Clamp(value, MinimumZoom, MaximumZoom));
    }

    /// <summary>
    /// Gets or sets the texel under the pointer, or <see langword="null"/> when the pointer is outside the image.
    /// </summary>
    public PixelPoint? PointerTexel
    {
        get => _pointerTexel;
        set => SetAndRaise(PointerTexelProperty, ref _pointerTexel, value);
    }

    /// <summary>
    /// Gets a value indicating whether the graphics device can display the slice format.
    /// </summary>
    public bool IsSliceSupported
    {
        get => _isSliceSupported;
        private set => SetAndRaise(IsSliceSupportedProperty, ref _isSliceSupported, value);
    }

    /// <summary>
    /// Scales the image to fit inside the control and centers it.
    /// </summary>
    public void FitToView()
    {
        _needsFit = true;
        RequestNextFrameRendering();
    }

    /// <summary>
    /// Displays the image at one texel per physical pixel and centers it.
    /// </summary>
    public void ShowActualSize()
    {
        _needsFit = false;
        _offset = Vector2.Zero;
        Zoom = 1.0;
        RequestNextFrameRendering();
    }

    /// <inheritdoc/>
    protected override void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);
        _graphicsDevice = new OpenGlGraphicsDevice(GL.GetApi(gl.GetProcAddress));
        _renderer = new ImageRenderer(_graphicsDevice, ClearColor);
        RequestNextFrameRendering();
    }

    /// <inheritdoc/>
    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (_renderer is null || _graphicsDevice is null)
        {
            return;
        }

        _graphicsDevice.DefaultFramebufferHandle = unchecked((uint)fb);
        Vector2 viewportSize = GetViewportSize();
        if (!_graphicsDevice.TryGetDefaultFramebufferSize(out int width, out int height))
        {
            width = (int)viewportSize.X;
            height = (int)viewportSize.Y;
        }

        if (_needsFit && Slice is { } slice)
        {
            int tileCount = Options.Tile ? 3 : 1;
            _needsFit = false;
            _offset = Vector2.Zero;
            Zoom = Math.Min(width / (double)(slice.Width * tileCount), height / (double)(slice.Height * tileCount));
        }

        _renderer.ClearColor = ClearColor;
        _renderer.Resize(width, height);
        _renderer.Render(Slice, FallbackSlice, _offset, (float)_zoom, Options);
        if (_renderer.IsTextureUploadPending)
        {
            RequestNextFrameRendering();
        }

        bool isSliceSupported = _renderer.IsSliceSupported;
        if (isSliceSupported != _isSliceSupported)
        {
            Dispatcher.UIThread.Post(() => IsSliceSupported = isSliceSupported);
        }
    }

    /// <inheritdoc/>
    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        ReleaseRenderer();
        base.OnOpenGlDeinit(gl);
    }

    /// <inheritdoc/>
    protected override void OnOpenGlLost()
    {
        ReleaseRenderer();
        base.OnOpenGlLost();
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.ClickCount == 2)
        {
            FitToView();
            return;
        }

        PointerPointProperties properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsLeftButtonPressed || properties.IsMiddleButtonPressed)
        {
            _isPanning = true;
            _lastPanPosition = e.GetPosition(this);
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Point position = e.GetPosition(this);
        if (_isPanning)
        {
            Point delta = (position - _lastPanPosition) * GetRenderScaling();
            _lastPanPosition = position;
            _offset += new Vector2((float)delta.X, (float)delta.Y);
            RequestNextFrameRendering();
        }

        PointerTexel = GetTexelAt(position);
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isPanning = false;
        e.Pointer.Capture(null);
    }

    /// <inheritdoc/>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PointerTexel = null;
    }

    /// <inheritdoc/>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        double previousZoom = _zoom;
        Zoom = _zoom * Math.Pow(WheelZoomFactor, e.Delta.Y);
        Vector2 pointer = ToViewportCenterRelative(e.GetPosition(this));
        _offset = pointer - ((pointer - _offset) * (float)(_zoom / previousZoom));
        _needsFit = false;
        PointerTexel = GetTexelAt(e.GetPosition(this));
        RequestNextFrameRendering();
        e.Handled = true;
    }

    private void OnSliceChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.OldValue is ImageSlice previous && e.NewValue is ImageSlice current)
        {
            Zoom = _zoom * previous.Width / current.Width;
        }
        else
        {
            _needsFit = true;
        }

        RequestNextFrameRendering();
    }

    private void OnOptionsChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.OldValue is ImageViewOptions previous && e.NewValue is ImageViewOptions current && previous.Tile != current.Tile)
        {
            _needsFit = true;
        }

        RequestNextFrameRendering();
    }

    private PixelPoint? GetTexelAt(Point position)
    {
        if (Slice is not { } slice)
        {
            return null;
        }

        ImageViewOptions options = Options;
        int tileCount = options.Tile ? 3 : 1;
        Vector2 texel = ((ToViewportCenterRelative(position) - _offset) / (float)_zoom) + new Vector2(slice.Width * tileCount * 0.5f, slice.Height * tileCount * 0.5f);
        int x = (int)MathF.Floor(texel.X);
        int y = (int)MathF.Floor(texel.Y);
        if (x < 0 || y < 0 || x >= slice.Width * tileCount || y >= slice.Height * tileCount)
        {
            return null;
        }

        x %= slice.Width;
        y %= slice.Height;
        return new PixelPoint(x, options.FlipY ? slice.Height - 1 - y : y);
    }

    private Vector2 ToViewportCenterRelative(Point position)
    {
        double scaling = GetRenderScaling();
        return new Vector2((float)(position.X * scaling), (float)(position.Y * scaling)) - (GetViewportSize() * 0.5f);
    }

    private Vector2 GetViewportSize()
    {
        double scaling = GetRenderScaling();
        return new Vector2(Math.Max(1, (int)Math.Ceiling(Bounds.Width * scaling)), Math.Max(1, (int)Math.Ceiling(Bounds.Height * scaling)));
    }

    private double GetRenderScaling() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;

    private void ReleaseRenderer()
    {
        _renderer?.Dispose();
        _renderer = null;
        _graphicsDevice?.Dispose();
        _graphicsDevice = null;
    }

    bool ICustomHitTest.HitTest(Point point)
    {
        return new Rect(Bounds.Size).Contains(point);
    }
}
