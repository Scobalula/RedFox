using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D.KaydaraFBX
{
    public enum FBXPropertyType : byte
    {
        Boolean = 0x43,
        Int16 = 0x59,
        Int32 = 0x49,
        Int64 = 0x4C,
        Single = 0x46,
        Double = 0x44,
        BooleanArray = 0x63,
        Int32Array = 0x69,
        Int64Array = 0x6C,
        SingleArray = 0x66,
        DoubleArray = 0x64,
        Raw = 0x52,
        String = 0x53,
    }
}
