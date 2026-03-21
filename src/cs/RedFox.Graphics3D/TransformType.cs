using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// Specifies the type of transformation applied to data, indicating how the transformation should be interpreted relative to its position or context.
    /// </summary>
    public enum TransformType
    {
        /// <summary>
        /// Specifies that the transformation type is unknown or not specified, indicating that it should inherit from the parent
        /// transformation or be determined by the context in which it is used.
        /// </summary>
        Unknown,

        /// <summary>
        /// Indicating that the transformation should inherit from the parent transformation, meaning that it will be applied in the
        /// same coordinate space as the parent and will be affected by any transformations applied to the parent.
        /// </summary>
        Parent,

        /// <summary>
        /// Specifies that the transformation is relative,indicating that the data should be interpreted as a change or offset
        /// from a previous state or parent transformation rather than an absolute value.
        /// </summary>
        Relative,

        /// <summary>
        /// Specifies that the transformation is absolute, indicating that the data should be interpreted as an absolute value
        /// in the coordinate space, independent of any parent transformations or previous states.
        /// </summary>
        Absolute,

        /// <summary>
        /// Specifies that the transformation is additive, indicating that the data should be interpreted as an incremental change
        /// on top of the existing transformation, allowing for cumulative effects when applied repeatedly or in combination with other transformations.
        /// </summary>
        Additive,
    }
}
