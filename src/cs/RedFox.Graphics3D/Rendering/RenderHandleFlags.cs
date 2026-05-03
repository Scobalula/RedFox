using System;
using System.Collections.Generic;
using System.Text;

namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// 
/// </summary>
public enum RenderHandleFlags
{
    /// <summary>
    /// Specifies that no options are set.
    /// </summary>
    None = 0,

    /// <summary>
    /// Represents a flag indicating that the handle is a subordinate or child handle.
    /// These handles will not be rendered directly by the render system, but are instead owned and updated by a parent handle.
    /// </summary>
    SubHandle = 1 << 1,
}
