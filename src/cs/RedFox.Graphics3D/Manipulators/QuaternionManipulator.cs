using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D.Interpolators
{
    public class QuaternionManipulator : IValueManipulator<Quaternion>
    {
        /// <inheritdoc/>
        public Quaternion Interpolate(Quaternion value1, Quaternion value2, float amount) => Quaternion.Slerp(value1, value2, amount);

        /// <inheritdoc/>
        public Quaternion Modify(Quaternion value, TransformType type)
        {
            throw new NotImplementedException();
        }
    }
}
