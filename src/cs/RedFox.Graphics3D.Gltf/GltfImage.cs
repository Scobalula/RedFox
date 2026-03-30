namespace RedFox.Graphics3D.Gltf;

/// <summary>
/// Represents an image resource in a glTF document, either embedded via a buffer view
/// or referenced by an external URI.
/// </summary>
public sealed class GltfImage
{
    /// <summary>
    /// Gets or sets the optional name of this image.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the URI for the image (for external references), or null if embedded.
    /// </summary>
    public string? Uri { get; set; }

    /// <summary>
    /// Gets or sets the MIME type of the image (e.g., "image/png", "image/jpeg").
    /// </summary>
    public string? MimeType { get; set; }

    /// <summary>
    /// Gets or sets the buffer view index that contains the image data, or -1 if URI-based.
    /// </summary>
    public int BufferView { get; set; } = -1;
}
