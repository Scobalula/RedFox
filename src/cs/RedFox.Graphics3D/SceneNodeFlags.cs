using System;
using System.Collections.Generic;
using System.Text;

namespace RedFox.Graphics3D
{
    [Flags]
    public enum SceneNodeFlags
    {
        None,
        Export,

        /// <summary>
        /// Indicates that the feature is disabled.
        /// </summary>
        Disabled,
    }
}
