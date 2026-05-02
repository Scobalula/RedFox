using System;
using System.Collections.Generic;
using System.Text;

namespace RedFox.Compression.GDeflate
{
    public unsafe struct GDeflatePage
    {
        public void* Data;
        public int Bytes;
    }
}
