using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D.Interpolators
{
    public class Vector3Interpolator : IValueManipulator<Vector3>
    {
        /// <inheritdoc/>
        public Vector3 Interpolate(Vector3 value1, Vector3 value2, float amount) => Vector3.Lerp(value1, value2, amount);

        public Vector3 Modify(Vector3 value, TransformType type)
        {
            throw new NotImplementedException();
        }
    }
}
