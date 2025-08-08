using RedFox.Graphics3D.KaydaraFBX.Document;
using RedFox.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D.KaydaraFBX.Reading
{
    public static class FBXNodeReader
    {
        public static FBXNode? ReadNode(BinaryReader reader, bool is64)
        {
            long endOffset = is64 ? reader.ReadInt64() : reader.ReadUInt32();
            long propCount = is64 ? reader.ReadInt64() : reader.ReadUInt32();
            long propLen = is64 ? reader.ReadInt64() : reader.ReadUInt32();
            long nameLen = reader.ReadByte();

            if (endOffset == 0)
                return null;

            var node = new FBXNode(new string(reader.ReadChars((int)nameLen)), (int)propCount);


            for (long i = 0; i < propCount; i++)
                node.Properties.Add(FBXPropertyReader.ReadProperty(reader, is64));

            while (reader.BaseStream.Position < endOffset && ReadNode(reader, is64) is FBXNode child)
                node.Children.Add(child);

            return node;

        }
    }
}
