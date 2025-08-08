using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D.KaydaraFBX.Document
{
    /// <summary>
    /// A class to hold a single 32bit FBX integer property.
    /// </summary>
    public class FBXPropertyInt32 : FBXProperty
    {
        /// <inheritdoc/>
        public override FBXPropertyType DataType => FBXPropertyType.Int32;

        /// <summary>
        /// Gets or Sets the value assigned to the property.
        /// </summary>
        public int Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FBXPropertyInt32"/> class.
        /// </summary>
        public FBXPropertyInt32() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="FBXPropertyInt32"/> class.
        /// </summary>
        /// <param name="value">The value to assign.</param>
        public FBXPropertyInt32(int value)
        {
            Value = value;
        }

        /// <inheritdoc/>
        internal override int GetSize()
        {
            return sizeof(int);
        }

        /// <inheritdoc/>
        internal override void Read(BinaryReader reader)
        {
            Value = reader.ReadInt32();
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
