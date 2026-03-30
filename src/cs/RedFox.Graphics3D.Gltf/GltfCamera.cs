namespace RedFox.Graphics3D.Gltf;

/// <summary>
/// Represents a glTF camera with either perspective or orthographic projection.
/// </summary>
public sealed class GltfCamera
{
    /// <summary>
    /// Gets or sets the optional name of the camera.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the projection type ("perspective" or "orthographic").
    /// </summary>
    public string Type { get; set; } = "perspective";

    /// <summary>
    /// Gets or sets the vertical field of view in radians (perspective only).
    /// </summary>
    public float YFov { get; set; }

    /// <summary>
    /// Gets or sets the aspect ratio (perspective only). Zero means unspecified.
    /// </summary>
    public float AspectRatio { get; set; }

    /// <summary>
    /// Gets or sets the near clipping plane distance.
    /// </summary>
    public float ZNear { get; set; }

    /// <summary>
    /// Gets or sets the far clipping plane distance. Zero means infinite (perspective only).
    /// </summary>
    public float ZFar { get; set; }

    /// <summary>
    /// Gets or sets the horizontal magnification (orthographic only).
    /// </summary>
    public float XMag { get; set; }

    /// <summary>
    /// Gets or sets the vertical magnification (orthographic only).
    /// </summary>
    public float YMag { get; set; }
}
