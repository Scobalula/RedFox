using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D
{
    public interface IValueManipulator<T>
    {
        public T Interpolate(T value1, T value2, float amount);

        public T Modify(T value, TransformType type);
    }
}
