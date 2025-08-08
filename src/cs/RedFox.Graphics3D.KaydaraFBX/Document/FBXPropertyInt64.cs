using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D.KaydaraFBX.Document
{
    /// <summary>
    /// A class to hold a single 64bit FBX integer property.
    /// </summary>
    public class FBXPropertyInt64 : FBXProperty
    {
        /// <inheritdoc/>
        public override FBXPropertyType DataType => FBXPropertyType.Int64;

        /// <summary>
        /// Gets or Sets the value assigned to the property.
        /// </summary>
        public long Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FBXPropertyInt64"/> class.
        /// </summary>
        public FBXPropertyInt64() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="FBXPropertyInt64"/> class.
        /// </summary>
        /// <param name="value">The value to assign.</param>
        public FBXPropertyInt64(long value)
        {
            Value = value;
        }

        /// <inheritdoc/>
        internal override int GetSize()
        {
            return sizeof(long);
        }

        /// <inheritdoc/>
        internal override void Read(BinaryReader reader)
        {
            Value = reader.ReadInt64();
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
