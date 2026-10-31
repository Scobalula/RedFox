using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Vector = Avalonia.Vector;

namespace RedFox.Graphics3D.Avalonia;

/// <summary>
/// Shows a camera's orientation as a labelled cube. Clicking a face, edge or corner asks for that view and dragging orbits. Buttons
/// around the cube turn it a quarter at a time, reset the view, fit the model and change the up axis.
/// </summary>
public sealed class ViewCubeControl : Control
{
    /// <summary>
    /// Defines the <see cref="YawRadians"/> property.
    /// </summary>
    public static readonly StyledProperty<float> YawRadiansProperty = AvaloniaProperty.Register<ViewCubeControl, float>(nameof(YawRadians));

    /// <summary>
    /// Defines the <see cref="PitchRadians"/> property.
    /// </summary>
    public static readonly StyledProperty<float> PitchRadiansProperty = AvaloniaProperty.Register<ViewCubeControl, float>(nameof(PitchRadians));

    /// <summary>
    /// Defines the <see cref="FrontYawRadians"/> property.
    /// </summary>
    public static readonly StyledProperty<float> FrontYawRadiansProperty = AvaloniaProperty.Register<ViewCubeControl, float>(nameof(FrontYawRadians));

    /// <summary>
    /// Defines the <see cref="UpAxis"/> property.
    /// </summary>
    public static readonly StyledProperty<SceneUpAxis> UpAxisProperty = AvaloniaProperty.Register<ViewCubeControl, SceneUpAxis>(nameof(UpAxis));

    /// <summary>
    /// Defines the <see cref="FaceBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> FaceBrushProperty = AvaloniaProperty.Register<ViewCubeControl, IBrush>(nameof(FaceBrush), new SolidColorBrush(Color.Parse("#E027272A")));

    /// <summary>
    /// Defines the <see cref="LineBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> LineBrushProperty = AvaloniaProperty.Register<ViewCubeControl, IBrush>(nameof(LineBrush), new SolidColorBrush(Color.Parse("#8A8A90")));

    /// <summary>
    /// Defines the <see cref="LabelBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> LabelBrushProperty = AvaloniaProperty.Register<ViewCubeControl, IBrush>(nameof(LabelBrush), new SolidColorBrush(Color.Parse("#FAFAFA")));

    /// <summary>
    /// Defines the <see cref="HoverBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> HoverBrushProperty = AvaloniaProperty.Register<ViewCubeControl, IBrush>(nameof(HoverBrush), new SolidColorBrush(Color.Parse("#0B7D92")));

    /// <summary>
    /// Defines the <see cref="HoverLabelBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> HoverLabelBrushProperty = AvaloniaProperty.Register<ViewCubeControl, IBrush>(nameof(HoverLabelBrush), new SolidColorBrush(Color.Parse("#FAFAFA")));

    /// <summary>
    /// Defines the <see cref="ButtonLineBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> ButtonLineBrushProperty = AvaloniaProperty.Register<ViewCubeControl, IBrush>(nameof(ButtonLineBrush), new SolidColorBrush(Color.Parse("#26FFFFFF")));

    private const double CubeScale = 0.2;
    private const double ArrowDistance = 0.42;
    private const double ArrowSize = 5.0;
    private const double ArrowHitRadius = 9.0;
    private const double ButtonSize = 18.0;
    private const double IconInset = 1.0;
    private const double DragThreshold = 3.0;
    private const double FaceLabelWidth = 1.5;
    private const double FaceLabelFontSize = 9.0;
    private const double FaceLabelUnitsPerPixel = 0.075;
    private const double ButtonLabelFontSize = 12.0;
    private const float RadiansPerPixel = 0.012f;

    private static readonly Geometry HomeIcon = Geometry.Parse("M8,2 L14.5,8 L12.5,8 L12.5,14 L9.5,14 L9.5,10 L6.5,10 L6.5,14 L3.5,14 L3.5,8 L1.5,8 Z");
    private static readonly Geometry FitIcon = Geometry.Parse("M3,6 L3,3 L6,3 M10,3 L13,3 L13,6 M13,10 L13,13 L10,13 M6,13 L3,13 L3,10 M6.5,6.5 L9.5,6.5 L9.5,9.5 L6.5,9.5 Z");
    private static readonly Typeface LabelFace = new(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);

    private ViewCubeRegion? _hoveredRegion;
    private ViewCubeButton _hoveredButton;
    private Point? _pressPosition;
    private Point _lastDragPosition;
    private bool _isDragging;

    static ViewCubeControl()
    {
        AffectsRender<ViewCubeControl>(YawRadiansProperty, PitchRadiansProperty, FrontYawRadiansProperty, UpAxisProperty);
        AffectsRender<ViewCubeControl>(FaceBrushProperty, LineBrushProperty, LabelBrushProperty, HoverBrushProperty, HoverLabelBrushProperty, ButtonLineBrushProperty);
    }

    /// <summary>
    /// Occurs when a region or turn arrow is clicked, with the orientation to move to.
    /// </summary>
    public event EventHandler<ViewCubeOrbitEventArgs>? ViewRequested;

    /// <summary>
    /// Occurs while the cube is dragged, with the change of orientation.
    /// </summary>
    public event EventHandler<ViewCubeOrbitEventArgs>? OrbitRequested;

    /// <summary>
    /// Occurs when the reset view button is clicked.
    /// </summary>
    public event EventHandler? ResetRequested;

    /// <summary>
    /// Occurs when the fit button is clicked.
    /// </summary>
    public event EventHandler? FitRequested;

    /// <summary>
    /// Occurs when the up axis button is clicked.
    /// </summary>
    public event EventHandler? UpAxisRequested;

    /// <summary>
    /// Gets or sets the camera yaw.
    /// </summary>
    public float YawRadians
    {
        get => GetValue(YawRadiansProperty);
        set => SetValue(YawRadiansProperty, value);
    }

    /// <summary>
    /// Gets or sets the camera pitch.
    /// </summary>
    public float PitchRadians
    {
        get => GetValue(PitchRadiansProperty);
        set => SetValue(PitchRadiansProperty, value);
    }

    /// <summary>
    /// Gets or sets the yaw of the front face around the up axis.
    /// </summary>
    public float FrontYawRadians
    {
        get => GetValue(FrontYawRadiansProperty);
        set => SetValue(FrontYawRadiansProperty, value);
    }

    /// <summary>
    /// Gets or sets the up axis shown on the up axis button.
    /// </summary>
    public SceneUpAxis UpAxis
    {
        get => GetValue(UpAxisProperty);
        set => SetValue(UpAxisProperty, value);
    }

    /// <summary>
    /// Gets or sets the face colour.
    /// </summary>
    public IBrush FaceBrush
    {
        get => GetValue(FaceBrushProperty);
        set => SetValue(FaceBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the colour of the cube's edges.
    /// </summary>
    public IBrush LineBrush
    {
        get => GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the colour of the labels, arrows and icons.
    /// </summary>
    public IBrush LabelBrush
    {
        get => GetValue(LabelBrushProperty);
        set => SetValue(LabelBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the colour of the part under the pointer.
    /// </summary>
    public IBrush HoverBrush
    {
        get => GetValue(HoverBrushProperty);
        set => SetValue(HoverBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the colour of an arrow or icon under the pointer.
    /// </summary>
    public IBrush HoverLabelBrush
    {
        get => GetValue(HoverLabelBrushProperty);
        set => SetValue(HoverLabelBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the colour of the button outlines.
    /// </summary>
    public IBrush ButtonLineBrush
    {
        get => GetValue(ButtonLineBrushProperty);
        set => SetValue(ButtonLineBrushProperty, value);
    }

    private double Scale => Math.Min(Bounds.Width, Bounds.Height) * CubeScale;

    private Point Centre => new(Bounds.Width * 0.5, Bounds.Height * 0.5);

    private double ArrowOffset => Math.Min(Bounds.Width, Bounds.Height) * ArrowDistance;

    private Rect ResetBounds => new(0.0, 0.0, ButtonSize, ButtonSize);

    private Rect FitBounds => new(Bounds.Width - ButtonSize, 0.0, ButtonSize, ButtonSize);

    private Rect UpAxisBounds => new(Bounds.Width - ButtonSize, Bounds.Height - ButtonSize, ButtonSize, ButtonSize);

    private Quaternion CubeToView => ViewCubeGeometry.GetCubeToView(new ViewCubeOrbit(YawRadians, PitchRadians), FrontYawRadians);

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        Quaternion cubeToView = CubeToView;

        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));

        foreach (ViewCubeFace face in ViewCubeGeometry.Faces)
        {
            if (Vector3.Transform(face.Normal, cubeToView).Z > 0.0f)
            {
                DrawFace(context, face, cubeToView);
            }
        }

        DrawArrow(context, ViewCubeButton.TurnTop, new Vector(0.0, -1.0));
        DrawArrow(context, ViewCubeButton.TurnBottom, new Vector(0.0, 1.0));
        DrawArrow(context, ViewCubeButton.TurnLeft, new Vector(-1.0, 0.0));
        DrawArrow(context, ViewCubeButton.TurnRight, new Vector(1.0, 0.0));

        DrawButton(context, ViewCubeButton.Reset, ResetBounds);
        DrawButton(context, ViewCubeButton.Fit, FitBounds);
        DrawButton(context, ViewCubeButton.UpAxis, UpAxisBounds);

        using (context.PushTransform(Matrix.CreateTranslation(ResetBounds.X + IconInset, ResetBounds.Y + IconInset)))
        {
            context.DrawGeometry(GetLabelBrush(ViewCubeButton.Reset), null, HomeIcon);
        }

        using (context.PushTransform(Matrix.CreateTranslation(FitBounds.X + IconInset, FitBounds.Y + IconInset)))
        {
            context.DrawGeometry(null, new Pen(GetLabelBrush(ViewCubeButton.Fit)), FitIcon);
        }

        FormattedText upAxis = new(UpAxis.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LabelFace, ButtonLabelFontSize, GetLabelBrush(ViewCubeButton.UpAxis));
        context.DrawText(upAxis, new Point(UpAxisBounds.Center.X - (upAxis.Width * 0.5), UpAxisBounds.Center.Y - (upAxis.Height * 0.5)));
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        Point position = e.GetPosition(this);

        if (_pressPosition is { } press)
        {
            if (!_isDragging && Point.Distance(position, press) >= DragThreshold)
            {
                _isDragging = true;
            }

            if (_isDragging)
            {
                Vector delta		= position - _lastDragPosition;
                _lastDragPosition	= position;
                OrbitRequested?.Invoke(this, new ViewCubeOrbitEventArgs(new ViewCubeOrbit((float)-delta.X * RadiansPerPixel, (float)-delta.Y * RadiansPerPixel)));
                return;
            }
        }

        UpdateHover(position);
    }

    /// <inheritdoc/>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetHover(null, ViewCubeButton.None);
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _pressPosition		= e.GetPosition(this);
        _lastDragPosition	= e.GetPosition(this);
        _isDragging			= false;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_pressPosition is null)
        {
            return;
        }

        bool wasDragging	= _isDragging;
        Point position		= e.GetPosition(this);

        _pressPosition	= null;
        _isDragging		= false;
        e.Pointer.Capture(null);
        e.Handled = true;

        if (wasDragging)
        {
            UpdateHover(position);
            return;
        }

        switch (FindButton(position))
        {
            case ViewCubeButton.Reset:
                ResetRequested?.Invoke(this, EventArgs.Empty);
                break;

            case ViewCubeButton.Fit:
                FitRequested?.Invoke(this, EventArgs.Empty);
                break;

            case ViewCubeButton.UpAxis:
                UpAxisRequested?.Invoke(this, EventArgs.Empty);
                break;

            case ViewCubeButton.TurnTop:
                RequestTurn(0, -1);
                break;

            case ViewCubeButton.TurnBottom:
                RequestTurn(0, 1);
                break;

            case ViewCubeButton.TurnLeft:
                RequestTurn(-1, 0);
                break;

            case ViewCubeButton.TurnRight:
                RequestTurn(1, 0);
                break;

            default:
                RequestRegion(position);
                break;
        }
    }

    private void RequestTurn(int yawQuarters, int pitchQuarters)
    {
        ViewRequested?.Invoke(this, new ViewCubeOrbitEventArgs(ViewCubeGeometry.GetTurn(new ViewCubeOrbit(YawRadians, PitchRadians), FrontYawRadians, yawQuarters, pitchQuarters)));
    }

    private void RequestRegion(Point position)
    {
        if (FindRegion(position) is { } region)
        {
            ViewRequested?.Invoke(this, new ViewCubeOrbitEventArgs(ViewCubeGeometry.GetOrbit(region, new ViewCubeOrbit(YawRadians, PitchRadians), FrontYawRadians)));
        }
    }

    private void UpdateHover(Point position)
    {
        ViewCubeButton button = FindButton(position);
        SetHover(button == ViewCubeButton.None ? FindRegion(position) : null, button);
        UpdateToolTip(position);
    }

    private void SetHover(ViewCubeRegion? region, ViewCubeButton button)
    {
        if (region != _hoveredRegion || button != _hoveredButton)
        {
            _hoveredRegion	= region;
            _hoveredButton	= button;
            InvalidateVisual();
        }
    }

    private void UpdateToolTip(Point position)
    {
        string? tip = GetToolTip(position);

        if (!Equals(ToolTip.GetTip(this), tip))
        {
            ToolTip.SetIsOpen(this, false);
            ToolTip.SetTip(this, tip);
            ToolTip.SetIsOpen(this, tip is not null);
        }
    }

    private string? GetToolTip(Point position)
    {
        return FindButton(position) switch
        {
            ViewCubeButton.Reset		=> "Reset view",
            ViewCubeButton.Fit			=> "Fit to model",
            ViewCubeButton.UpAxis		=> $"Up axis: {UpAxis} (click to change)",
            ViewCubeButton.TurnTop		=> "Turn to the top",
            ViewCubeButton.TurnBottom	=> "Turn to the bottom",
            ViewCubeButton.TurnLeft		=> "Turn to the left",
            ViewCubeButton.TurnRight	=> "Turn to the right",
            _							=> null,
        };
    }

    private ViewCubeButton FindButton(Point position)
    {
        if (ResetBounds.Contains(position))
        {
            return ViewCubeButton.Reset;
        }

        if (FitBounds.Contains(position))
        {
            return ViewCubeButton.Fit;
        }

        if (UpAxisBounds.Contains(position))
        {
            return ViewCubeButton.UpAxis;
        }

        if (IsOverArrow(position, new Vector(0.0, -1.0)))
        {
            return ViewCubeButton.TurnTop;
        }

        if (IsOverArrow(position, new Vector(0.0, 1.0)))
        {
            return ViewCubeButton.TurnBottom;
        }

        if (IsOverArrow(position, new Vector(-1.0, 0.0)))
        {
            return ViewCubeButton.TurnLeft;
        }

        return IsOverArrow(position, new Vector(1.0, 0.0)) ? ViewCubeButton.TurnRight : ViewCubeButton.None;
    }

    private bool IsOverArrow(Point position, Vector direction)
    {
        return Point.Distance(position, Centre + (direction * ArrowOffset)) <= ArrowHitRadius;
    }

    private ViewCubeRegion? FindRegion(Point position)
    {
        if (Scale <= 0.0)
        {
            return null;
        }

        Vector offset = (position - Centre) / Scale;
        return ViewCubeGeometry.HitTest(new Vector2((float)offset.X, (float)-offset.Y), CubeToView);
    }

    private Point Project(Vector3 point, Quaternion cubeToView)
    {
        Vector3 view = Vector3.Transform(point, cubeToView);
        return Centre + (new Vector(view.X, -view.Y) * Scale);
    }

    private void DrawButton(DrawingContext context, ViewCubeButton button, Rect bounds)
    {
        context.DrawRectangle(button == _hoveredButton ? HoverBrush : FaceBrush, new Pen(ButtonLineBrush), bounds.Deflate(0.5));
    }

    private IBrush GetLabelBrush(ViewCubeButton button)
    {
        return button == _hoveredButton ? HoverLabelBrush : LabelBrush;
    }

    private void DrawArrow(DrawingContext context, ViewCubeButton button, Vector direction)
    {
        Point centre	= Centre + (direction * ArrowOffset);
        Vector side		= new Vector(-direction.Y, direction.X) * ArrowSize;
        Point tip		= centre - (direction * ArrowSize);
        Point back		= centre + (direction * ArrowSize);

        StreamGeometry geometry = new();

        using (StreamGeometryContext stream = geometry.Open())
        {
            stream.BeginFigure(tip, true);
            stream.LineTo(back + side);
            stream.LineTo(back - side);
            stream.EndFigure(true);
        }

        context.DrawGeometry(GetLabelBrush(button), null, geometry);
    }

    private void DrawFace(DrawingContext context, ViewCubeFace face, Quaternion cubeToView)
    {
        Vector3[] corners = [
            face.Normal - face.Right - face.Up,
            face.Normal + face.Right - face.Up,
            face.Normal + face.Right + face.Up,
            face.Normal - face.Right + face.Up,
        ];

        context.DrawGeometry(FaceBrush, new Pen(LineBrush), CreatePolygon(corners, cubeToView));

        if (_hoveredRegion is { } region && ViewCubeGeometry.TryGetPatch(region, face, out Vector3[] patch))
        {
            context.DrawGeometry(HoverBrush, null, CreatePolygon(patch, cubeToView));
        }

        DrawFaceLabel(context, face, cubeToView);
    }

    private void DrawFaceLabel(DrawingContext context, ViewCubeFace face, Quaternion cubeToView)
    {
        FormattedText text = new(face.Label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LabelFace, FaceLabelFontSize, LabelBrush);

        double unitsPerPixel	= Math.Min(FaceLabelWidth / text.Width, FaceLabelUnitsPerPixel);
        Vector right			= ToScreen(Vector3.Transform(face.Right, cubeToView)) * (Scale * unitsPerPixel);
        Vector down				= ToScreen(Vector3.Transform(-face.Up, cubeToView)) * (Scale * unitsPerPixel);
        Point centre			= Project(face.Normal, cubeToView);

        using (context.PushTransform(new Matrix(right.X, right.Y, down.X, down.Y, centre.X, centre.Y)))
        {
            context.DrawText(text, new Point(-text.Width * 0.5, -text.Height * 0.5));
        }
    }

    private StreamGeometry CreatePolygon(IReadOnlyList<Vector3> points, Quaternion cubeToView)
    {
        StreamGeometry geometry = new();

        using (StreamGeometryContext context = geometry.Open())
        {
            context.BeginFigure(Project(points[0], cubeToView), true);

            for (int index = 1; index < points.Count; index++)
            {
                context.LineTo(Project(points[index], cubeToView));
            }

            context.EndFigure(true);
        }

        return geometry;
    }

    private static Vector ToScreen(Vector3 direction)
    {
        return new Vector(direction.X, -direction.Y);
    }
}
