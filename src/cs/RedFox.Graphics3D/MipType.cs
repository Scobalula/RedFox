namespace RedFox.Graphics3D;

/// <summary>
/// Indicates the preferred texture mip level for a load request.
/// </summary>
public enum MipType
{
    /// <summary>
    /// Requests a lower resolution mip suitable for thumbnails of models.
    /// </summary>
    UltraLow,

    /// <summary>
    /// Requests a lower resolution mip suitable for thumbnails.
    /// </summary>
    Low,

    /// <summary>
    /// Requests a mid resolution mip suitable for previews.
    /// </summary>
    Medium,

    /// <summary>
    /// Requests the highest resolution mip available.
    /// </summary>
    High,
}
