using System;
using System.Collections.Generic;
using System.Text;
using RedFox.Graphics2D;

namespace RedFox.Graphics3D;

/// <summary>
/// Represents a texture resource loaded from a file and associated with a specific slot in a scene graph.
/// </summary>
/// <param name="filePath">The full path to the image file to be used as the texture.</param>
/// <param name="slot">The identifier for the slot to which this texture is assigned. Used to reference the texture within the scene.</param>
public class Texture(string filePath, string slot) : SceneNode(Path.GetFileNameWithoutExtension(filePath))
{
    /// <summary>
    /// Gets or sets the slot identifier associated with this texture.
    /// </summary>
    public string Slot { get; set; } = slot;

    /// <summary>
    /// Gets or sets the full path to the file associated with this texture.
    /// </summary>
    public string FilePath { get; set; } = filePath;

    /// <summary>
    /// Gets or sets the resolved on-disk path for this texture.
    /// This can differ from <see cref="FilePath"/> when the source scene stores a relative reference.
    /// </summary>
    public string? ResolvedFilePath { get; set; }

    /// <summary>
    /// Gets the best path to use when loading the texture from disk.
    /// </summary>
    public string EffectiveFilePath => string.IsNullOrWhiteSpace(ResolvedFilePath) ? FilePath : ResolvedFilePath;

    /// <summary>
    /// Gets or sets the image data associated with this texture. This may be <see langword="null"/> if the image data has not been loaded yet.
    /// </summary>
    public Image? Data { get; set; }

    /// <summary>
    /// Gets or sets the image loader used to retrieve and process images.
    /// </summary>
    public IImageLoader? ImageLoader { get; set; }
}
