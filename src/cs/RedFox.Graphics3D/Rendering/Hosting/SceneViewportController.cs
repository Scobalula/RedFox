using System;
using System.Numerics;

namespace RedFox.Graphics3D.Rendering.Hosting;

/// <summary>
/// Owns reusable scene-view orchestration for an orbit camera, including input-driven camera updates,
/// optional animated-bounds refresh, clip-plane management, and final scene rendering.
/// </summary>
public sealed class SceneViewportController
{
    private const float CameraFitPadding = 1.15f;
    private const float CameraMaxDistanceScale = 24.0f;
    private const float CameraMoveSpeedScale = 0.1f;
    private const float DynamicFarBoundsScale = 12.0f;
    private const float DynamicFarDistanceScale = 4.0f;
    private const float DynamicNearBoundsScale = 0.0005f;
    private const float DynamicNearDistanceScale = 0.25f;
    private const float DynamicNearSafetyBoundsScale = 1.25f;
    private const float MinimumCameraMaxDistance = 10.0f;
    private const float MinimumCameraMoveSpeed = 0.01f;
    private const float MinimumClipPlane = 0.00001f;
    private const float MinimumFarPlane = 10.0f;
    private const float MinimumSceneRadius = 0.0001f;
    private const float TargetDepthRatio = 100000.0f;

    private readonly OrbitCamera _camera;
    private readonly Scene _scene;

    /// <summary>
    /// Gets the controlled scene.
    /// </summary>
    public Scene Scene => _scene;

    /// <summary>
    /// Gets the controlled camera.
    /// </summary>
    public OrbitCamera Camera => _camera;

    /// <summary>
    /// Gets the most recent computed scene bounds.
    /// </summary>
    public SceneBounds Bounds { get; private set; } = SceneBounds.Invalid;

    /// <summary>
    /// Gets a value indicating whether <see cref="Bounds"/> currently contains a valid result.
    /// </summary>
    public bool HasBounds { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether scene bounds should be recomputed every frame before rendering.
    /// </summary>
    public bool RefreshAnimatedSceneBounds { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether clip planes should be updated from the current bounds before rendering.
    /// </summary>
    public bool UpdateClipPlanesFromBounds { get; set; } = true;

    /// <summary>
    /// Gets or sets an optional predicate that controls which nodes contribute to computed bounds.
    /// </summary>
    public Func<SceneNode, bool>? IncludeNodeInBounds { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SceneViewportController"/> class.
    /// </summary>
    /// <param name="scene">The controlled scene.</param>
    /// <param name="camera">The controlled orbit camera.</param>
    public SceneViewportController(Scene scene, OrbitCamera camera)
    {
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _camera = camera ?? throw new ArgumentNullException(nameof(camera));
    }

    /// <summary>
    /// Updates the controlled camera's aspect ratio from a viewport size.
    /// </summary>
    /// <param name="width">The viewport width in pixels.</param>
    /// <param name="height">The viewport height in pixels.</param>
    public void ResizeViewport(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        _camera.AspectRatio = (float)width / height;
    }

    /// <summary>
    /// Recomputes world-space scene bounds using the current bounds predicate.
    /// </summary>
    /// <returns><see langword="true"/> when valid bounds were found; otherwise <see langword="false"/>.</returns>
    public bool RecomputeBounds()
    {
        HasBounds = SceneBoundsCalculator.TryCompute(_scene, out SceneBounds bounds, IncludeNodeInBounds);
        Bounds = HasBounds ? bounds : SceneBounds.Invalid;
        return HasBounds;
    }

    /// <summary>
    /// Fits the orbit camera to the current computed bounds.
    /// </summary>
    /// <returns><see langword="true"/> when bounds were available and the camera was updated; otherwise <see langword="false"/>.</returns>
    public bool FitCameraToScene()
    {
        if (!HasBounds)
        {
            return false;
        }

        ApplyBoundsToCamera(Bounds, _camera, _scene.UpAxis);
        return true;
    }

    /// <summary>
    /// Updates the controlled scene and renders it using the supplied renderer and input source.
    /// </summary>
    /// <param name="renderer">The scene renderer used for drawing.</param>
    /// <param name="inputSource">The input source sampled for this frame.</param>
    /// <param name="deltaTime">Seconds elapsed since the previous frame.</param>
    public void UpdateAndRender(SceneRenderer renderer, IInputSource inputSource, float deltaTime)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(inputSource);

        UpdateCamera(inputSource.ReadInput(), deltaTime);
        UpdateScene(deltaTime);
        Render(renderer, deltaTime);
    }

    /// <summary>
    /// Updates the controlled scene and renders it using the supplied renderer without external input.
    /// </summary>
    /// <param name="renderer">The scene renderer used for drawing.</param>
    /// <param name="deltaTime">Seconds elapsed since the previous frame.</param>
    public void UpdateAndRender(SceneRenderer renderer, float deltaTime)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        UpdateCamera(CameraControllerInput.Empty, deltaTime);
        UpdateScene(deltaTime);
        Render(renderer, deltaTime);
    }

    private static void ApplyBoundsToCamera(SceneBounds bounds, OrbitCamera camera, SceneUpAxis upAxis)
    {
        SceneBounds adjustedBounds = GetAxisAdjustedBounds(bounds, upAxis);
        float radius = MathF.Max(adjustedBounds.Radius, 0.5f);
        float diagonal = MathF.Max(adjustedBounds.DiagonalLength, 1.0f);
        Vector3 preferredForward = Vector3.Normalize(new Vector3(1.0f, -0.28f, -0.2f));
        (float yaw, float pitch) = GetYawPitchFromForward(preferredForward);

        camera.OrbitTarget = adjustedBounds.Center;
        camera.YawRadians = yaw;
        camera.PitchRadians = pitch;
        camera.MinDistance = MathF.Max(radius * DynamicNearBoundsScale, MinimumClipPlane);
        camera.MaxDistance = MathF.Max(diagonal * CameraMaxDistanceScale, MinimumCameraMaxDistance);
        camera.MoveSpeed = MathF.Max(diagonal * CameraMoveSpeedScale, MinimumCameraMoveSpeed);
        camera.BoostMultiplier = 2.5f;
        camera.ZoomSensitivity = 1.0f;

        float aspect = camera.AspectRatio > 0.0f ? camera.AspectRatio : (16.0f / 9.0f);
        float fitDistance = ComputeBoundsFitDistance(adjustedBounds, preferredForward, camera.FieldOfView, aspect) * CameraFitPadding;
        camera.Distance = Math.Clamp(fitDistance, camera.MinDistance, camera.MaxDistance);
        camera.ApplyOrbit();
        UpdateDynamicClipPlanes(adjustedBounds, camera);
    }

    private static float ComputeBoundsFitDistance(SceneBounds bounds, Vector3 forward, float verticalFovDegrees, float aspectRatio)
    {
        Vector3 normalizedForward = forward.LengthSquared() < 1e-8f ? -Vector3.UnitZ : Vector3.Normalize(forward);
        Vector3 upHint = MathF.Abs(Vector3.Dot(normalizedForward, Vector3.UnitY)) > 0.98f
            ? Vector3.UnitZ
            : Vector3.UnitY;

        Vector3 right = Vector3.Cross(normalizedForward, upHint);
        if (right.LengthSquared() < 1e-8f)
        {
            right = Vector3.Cross(normalizedForward, Vector3.UnitX);
        }

        right = Vector3.Normalize(right);
        Vector3 up = Vector3.Normalize(Vector3.Cross(right, normalizedForward));

        float verticalFov = MathF.Max(verticalFovDegrees * (MathF.PI / 180.0f), 1e-3f);
        float halfVertical = verticalFov * 0.5f;
        float halfHorizontal = MathF.Atan(MathF.Tan(halfVertical) * MathF.Max(aspectRatio, 1e-3f));
        float tanVertical = MathF.Max(MathF.Tan(halfVertical), 1e-4f);
        float tanHorizontal = MathF.Max(MathF.Tan(halfHorizontal), 1e-4f);

        Vector3[] corners = GetBoundsCorners(bounds);
        Vector3 center = bounds.Center;
        float requiredDistance = 0.0f;

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 relative = corners[i] - center;
            float x = MathF.Abs(Vector3.Dot(relative, right));
            float y = MathF.Abs(Vector3.Dot(relative, up));
            float z = Vector3.Dot(relative, normalizedForward);

            float distanceForX = (x / tanHorizontal) - z;
            float distanceForY = (y / tanVertical) - z;
            float cornerDistance = MathF.Max(distanceForX, distanceForY);
            requiredDistance = MathF.Max(requiredDistance, cornerDistance);
        }

        return MathF.Max(requiredDistance, bounds.Radius * 1.1f);
    }

    private static SceneBounds GetAxisAdjustedBounds(SceneBounds bounds, SceneUpAxis upAxis)
    {
        Matrix4x4 sceneAxisMatrix = GetSceneAxisMatrix(upAxis);
        if (sceneAxisMatrix == Matrix4x4.Identity)
        {
            return bounds;
        }

        Vector3[] corners = GetBoundsCorners(bounds);
        Vector3 transformedMin = new(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 transformedMax = new(float.MinValue, float.MinValue, float.MinValue);

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 transformed = Vector3.Transform(corners[i], sceneAxisMatrix);
            transformedMin = Vector3.Min(transformedMin, transformed);
            transformedMax = Vector3.Max(transformedMax, transformed);
        }

        return new SceneBounds(transformedMin, transformedMax);
    }

    private static Vector3[] GetBoundsCorners(SceneBounds bounds)
    {
        Vector3 min = bounds.Min;
        Vector3 max = bounds.Max;

        return
        [
            new Vector3(min.X, min.Y, min.Z),
            new Vector3(max.X, min.Y, min.Z),
            new Vector3(min.X, max.Y, min.Z),
            new Vector3(max.X, max.Y, min.Z),
            new Vector3(min.X, min.Y, max.Z),
            new Vector3(max.X, min.Y, max.Z),
            new Vector3(min.X, max.Y, max.Z),
            new Vector3(max.X, max.Y, max.Z)
        ];
    }

    private static Matrix4x4 GetSceneAxisMatrix(SceneUpAxis upAxis)
    {
        return upAxis switch
        {
            SceneUpAxis.X => Matrix4x4.CreateRotationZ(MathF.PI * 0.5f),
            SceneUpAxis.Z => Matrix4x4.CreateRotationX(-MathF.PI * 0.5f),
            _ => Matrix4x4.Identity,
        };
    }

    private static (float YawRadians, float PitchRadians) GetYawPitchFromForward(Vector3 forward)
    {
        Vector3 normalized = forward;
        if (normalized.LengthSquared() < 1e-8f)
        {
            normalized = -Vector3.UnitZ;
        }
        else
        {
            normalized = Vector3.Normalize(normalized);
        }

        float yaw = MathF.Atan2(normalized.X, -normalized.Z);
        float pitch = MathF.Asin(Math.Clamp(normalized.Y, -1.0f, 1.0f));
        return (yaw, pitch);
    }

    private static void UpdateDynamicClipPlanes(SceneBounds bounds, OrbitCamera camera)
    {
        float radius = MathF.Max(bounds.Radius, MinimumSceneRadius);
        float distanceToCenter = Vector3.Distance(camera.Position, bounds.Center);
        float farPlane = MathF.Max(
            distanceToCenter + (radius * DynamicFarDistanceScale),
            MathF.Max(radius * DynamicFarBoundsScale, MinimumFarPlane));

        float distanceOutsideBounds = distanceToCenter - (radius * DynamicNearSafetyBoundsScale);
        float nearFromDistance = distanceOutsideBounds > 0.0f ? distanceOutsideBounds * DynamicNearDistanceScale : 0.0f;
        float nearFromScale = radius * DynamicNearBoundsScale;
        float nearFromDepthRatio = farPlane / TargetDepthRatio;
        float nearPlane = MathF.Max(
            MinimumClipPlane,
            MathF.Max(nearFromDistance, MathF.Max(nearFromScale, nearFromDepthRatio)));
        nearPlane = MathF.Min(nearPlane, farPlane * 0.0005f);

        camera.NearPlane = nearPlane;
        camera.FarPlane = farPlane;
    }

    private void Render(SceneRenderer renderer, float deltaTime)
    {
        if (UpdateClipPlanesFromBounds && HasBounds)
        {
            UpdateDynamicClipPlanes(GetAxisAdjustedBounds(Bounds, _scene.UpAxis), _camera);
        }

        CameraView view = _camera.GetView();
        renderer.Render(_scene, view, deltaTime);
    }

    private void UpdateCamera(in CameraControllerInput input, float deltaTime)
    {
        _camera.UpdateInput(deltaTime, input);
    }

    private void UpdateScene(float deltaTime)
    {
        if (RefreshAnimatedSceneBounds)
        {
            RecomputeBounds();
        }

        _scene.Update(deltaTime);
    }
}