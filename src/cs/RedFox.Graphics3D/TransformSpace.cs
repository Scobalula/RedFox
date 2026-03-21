using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D;

/// <summary>
/// Specifies the coordinate spaces in which transformations can be applied.
/// </summary>
public enum TransformSpace
{
    /// <summary>
    /// Specifies that transformations are performed in the local coordinate space.
    /// </summary>
    Local,

    /// <summary>
    /// Specifies that transformations are performed in the world coordinate space.
    /// </summary>
    World,
}
