using System;
using System.Collections.Generic;
using System.Text;

namespace RedFox.GameExtraction.AssetHandlers;

/// <summary>
/// A standard <see cref="IAssetHandler"/> implementation for handling models.
/// </summary>
public class ModelHandler
{
    /// <summary>
    /// Gets the default formats to use during export.
    /// </summary>
    public static readonly string[] DefaultFormats = [".semodel", ".cast", ".fbx"];
}
