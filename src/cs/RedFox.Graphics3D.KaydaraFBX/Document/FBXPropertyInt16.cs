
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
    public class FBXPropertyInt16 : FBXProperty
    {
        /// <inheritdoc/>
        public override FBXPropertyType DataType => FBXPropertyType.Int16;

        /// <summary>
        /// Gets or Sets the value assigned to the property.
        /// </summary>
        public short Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FBXPropertyInt16"/> class.
        /// </summary>
        public FBXPropertyInt16() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="FBXPropertyInt16"/> class.
        /// </summary>
        /// <param name="value">The value to assign.</param>
        public FBXPropertyInt16(short value)
        {
            Value = value;
        }

        /// <inheritdoc/>
        internal override int GetSize()
        {
            return sizeof(short);
        }

        /// <inheritdoc/>
        internal override void Read(BinaryReader reader)
        {
            Value = reader.ReadInt16();
        }

        /// <inheritdoc/>
        internal override void Write(BinaryWriter writer)
        {
            writer.Write(Value);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
