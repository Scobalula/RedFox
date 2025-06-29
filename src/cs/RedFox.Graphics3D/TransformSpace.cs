using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// Defines the supported transform spaces for a particular <see cref="Graphics3DObject"/>.
    /// </summary>
    public enum TransformSpace
    {
        /// <summary>
        /// Data is stored in local space.
        /// </summary>
        Local,
        
        /// <summary>
        /// Data is stored in world space.
        /// </summary>
        World,
    }
}
