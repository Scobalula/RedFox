using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RedFox.Graphics3D.KaydaraFBX.Document;

namespace RedFox.Graphics3D.KaydaraFBX.Reading
{
    public static class FBXPropertyReader
    {
        public static FBXProperty ReadProperty(BinaryReader reader, bool is64)
        {
            var propType = (FBXPropertyType)reader.ReadByte();

            FBXProperty prop = propType switch
            {
                FBXPropertyType.Int32       => new FBXPropertyInt32(),
                FBXPropertyType.String      => new FBXPropertyString(),
                FBXPropertyType.Boolean     => new FBXPropertyBoolean(),
                FBXPropertyType.Int16       => new FBXPropertyInt16(),
                FBXPropertyType.Int64       => new FBXPropertyInt64(),
                FBXPropertyType.Single      => new FBXPropertySingle(),
                FBXPropertyType.Double      => new FBXPropertyDouble(),
                FBXPropertyType.Raw         => new FBXPropertyRaw(),
                FBXPropertyType.Int32Array  => throw new NotImplementedException(),
                FBXPropertyType.Int64Array  => throw new NotImplementedException(),
                FBXPropertyType.SingleArray => throw new NotImplementedException(),
                FBXPropertyType.DoubleArray => throw new NotImplementedException(),
                _ => throw new NotImplementedException()
            };

            prop.Read(reader);

            return prop;
        }
    }
}
