using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// Defines interpolation and transform operations for a value type.
    /// </summary>
    /// <typeparam name="T">The value type manipulated by this implementation.</typeparam>
    public interface IValueManipulator<T>
    {
        /// <summary>
        /// Interpolates between two values.
        /// </summary>
        /// <param name="value1">The value at interpolation amount zero.</param>
        /// <param name="value2">The value at interpolation amount one.</param>
        /// <param name="amount">The interpolation amount.</param>
        /// <returns>The interpolated value.</returns>
        public T Interpolate(T value1, T value2, float amount);

        /// <summary>
        /// Applies the specified transform interpretation to a value.
        /// </summary>
        /// <param name="value">The value to transform.</param>
        /// <param name="type">The transform interpretation to apply.</param>
        /// <returns>The transformed value.</returns>
        public T Modify(T value, TransformType type);
    }
}
