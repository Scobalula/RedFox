
using RedFox.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D.KaydaraFBX.Document
{
    /// <summary>
    /// A class to hold a single 16bit FBX integer property.
    /// </summary>
    public class FBXPropertyInt16Array : FBXProperty
    {
        /// <inheritdoc/>
        public override FBXPropertyType DataType => FBXPropertyType.Int16;

        /// <summary>
        /// Gets or Sets the value assigned to the property.
        /// </summary>
        public List<short> Values { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FBXPropertyInt16"/> class.
        /// </summary>
        public FBXPropertyInt16Array() { Values = new(); }

        /// <summary>
        /// Initializes a new instance of the <see cref="FBXPropertyInt16"/> class.
        /// </summary>
        /// <param name="value">The value to assign.</param>
        public FBXPropertyInt16Array(IEnumerable<short> values) { Values = new(values); }

        /// <inheritdoc/>
        internal override int GetSize()
        {
            return sizeof(short);
        }

        /// <inheritdoc/>
        internal override void Read(BinaryReader reader)
        {
            Values = FBXCompression.ReadArray<short>(reader);
        }

        /// <inheritdoc/>
        internal override void Write(BinaryWriter writer)
        {
            //writer.Write(Value);
        }
    }
}
