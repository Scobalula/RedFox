using RedFox.Graphics3D.KaydaraFBX.Document;
using RedFox.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D.KaydaraFBX.Reading
{
    public static class FBXDocumentReader
    {
        public static FBXDocument ReadDocument(string filePath)
        {
            using var reader = new BinaryReader(File.OpenRead(filePath));
            return ReadDocument(reader);
        }
        public static FBXDocument ReadDocument(BinaryReader reader)
        {
            var header = reader.ReadUTF8NullTerminatedString();

            if (!header.Equals("Kaydara FBX Binary  "))
                throw new InvalidDataException();

            var a = reader.ReadByte();
            var isBigEndian = reader.ReadByte();
            var version = reader.ReadUInt32();

            var document = new FBXDocument(version);

            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                var node = FBXNodeReader.ReadNode(reader, version > 7500);

                if (node is null)
                    break;

                document.Nodes.Add(node);
            }

            return document;
        }
    }
}
