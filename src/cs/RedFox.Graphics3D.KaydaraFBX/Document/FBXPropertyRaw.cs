using RedFox.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D.KaydaraFBX.Document
{
    /// <summary>
    /// A class to hold a raw binary FBX property.
    /// </summary>
    public class FBXPropertyRaw : FBXProperty
    {
        /// <inheritdoc/>
        public override FBXPropertyType DataType => FBXPropertyType.Int32;

        /// <summary>
        /// Gets or Sets the values assigned to the property.
        /// </summary>
        public List<byte> Values { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FBXPropertyRaw"/> class.
        /// </summary>
        public FBXPropertyRaw() { Values = Enumerable.Empty<byte>().ToList(); }

        /// <summary>
        /// Initializes a new instance of the <see cref="FBXPropertyRaw"/> class.
        /// </summary>
        /// <param name="value">The value to assign.</param>
        public FBXPropertyRaw(List<byte> value) { Values = value; }

        /// <inheritdoc/>
        internal override int GetSize()
        {
            return 4 + Values.Count;
        }

        /// <inheritdoc/>
        internal override void Read(BinaryReader reader)
        {
            Values = [..reader.ReadStructArray<byte>(reader.ReadInt32())];
        }

        /// <inheritdoc/>
        internal override void Write(BinaryWriter writer)
        {
            writer.Write(Values.Count);
            writer.Write(CollectionsMarshal.AsSpan(Values));
        }
    }
}
