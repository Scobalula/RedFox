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
    public class FBXPropertySingle : FBXProperty
    {
        /// <inheritdoc/>
        public override FBXPropertyType DataType => FBXPropertyType.Single;

        /// <summary>
        /// Gets or Sets the value assigned to the property.
        /// </summary>
        public float Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FBXPropertySingle"/> class.
        /// </summary>
        public FBXPropertySingle() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="FBXPropertySingle"/> class.
        /// </summary>
        /// <param name="value">The value to assign.</param>
        public FBXPropertySingle(float value)
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
            Value = reader.ReadSingle();
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
