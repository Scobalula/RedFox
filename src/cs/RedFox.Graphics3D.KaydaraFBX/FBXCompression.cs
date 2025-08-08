using RedFox.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D.KaydaraFBX
{
    internal class FBXCompression
    {
        public static List<T> ReadArray<T>(BinaryReader reader) where T : unmanaged
        {
            var arrayLength = reader.ReadInt32();
            var encoding = reader.ReadInt32();
            var compressedLength = reader.ReadInt32();

            if (encoding == 1)
                return DecompressArray<T>(reader.ReadBytes(compressedLength), arrayLength);
            else
                return [..reader.ReadStructArray<T>(arrayLength)];

        }
        public static List<T> DecompressArray<T>(byte[] buffer, int arrayLength) where T : unmanaged
        {
            return null;
        }
    }
}
