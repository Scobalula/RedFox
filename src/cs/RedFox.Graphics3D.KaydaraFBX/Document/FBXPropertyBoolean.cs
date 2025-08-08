using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D.KaydaraFBX.Document
{
    /// <summary>
    /// A class to hold a single 64bit FBX floating point property.
    /// </summary>
    public class FBXPropertyBoolean : FBXProperty
    {
        /// <inheritdoc/>
        public override FBXPropertyType DataType => FBXPropertyType.Double;

        /// <summary>
        /// Gets or Sets the value assigned to the property.
        /// </summary>
        public bool Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FBXPropertyBoolean"/> class.
        /// </summary>
        public FBXPropertyBoolean() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="FBXPropertyBoolean"/> class.
        /// </summary>
        /// <param name="value">The value to assign.</param>
        public FBXPropertyBoolean(bool value)
        {
            Value = value;
        }

        /// <inheritdoc/>
        internal override int GetSize()
        {
            return sizeof(byte);
        }

        /// <inheritdoc/>
        internal override void Read(BinaryReader reader)
        {
            Value = reader.ReadBoolean();
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
