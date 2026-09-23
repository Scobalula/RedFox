using System;
using System.Collections.Generic;
using System.Text;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// Specifies the winding order used to interpret mesh faces.
    /// </summary>
    public enum MeshFaceOrder
    {
        /// <summary>
        /// Vertices are ordered clockwise when viewed from the front face.
        /// </summary>
        Clockwise,
        /// <summary>
        /// Vertices are ordered counterclockwise when viewed from the front face.
        /// </summary>
        CounterClockwise
    }
}
