using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D.Interpolators
{
    public class FloatInterpolator : IValueManipulator<float>
    {
        /// <inheritdoc/>
        public float Interpolate(float value1, float value2, float amount) => float.Lerp(value1, value2, amount);

        public float Modify(float value, TransformType type)
        {
            throw new NotImplementedException();
        }
    }
}
