using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D.KaydaraFBX.Document
{
    /// <summary>
    /// A class to hold a single string FBX property.
    /// </summary>
    public class FBXPropertyString : FBXProperty
    {
        /// <inheritdoc/>
        public override FBXPropertyType DataType => FBXPropertyType.String;

        /// <summary>
        /// Gets or Sets the value assigned to the property.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FBXPropertyString"/> class.
        /// </summary>
        public FBXPropertyString() { Value = string.Empty; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FBXPropertyString"/> class.
        /// </summary>
        /// <param name="value">The value to assign.</param>
        public FBXPropertyString(string value) { Value = value; }

        /// <inheritdoc/>
        internal override int GetSize()
        {
            return 4 + Value.Length;
        }

        /// <inheritdoc/>
        internal override void Read(BinaryReader reader)
        {
            Value = new string(reader.ReadChars(reader.ReadInt32()));
        }

        /// <inheritdoc/>
        internal override void Write(BinaryWriter writer)
        {
            writer.Write(Value.Length);
            writer.Write(Value.AsSpan());
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Value;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            if (obj is FBXPropertyString fbxString)
                return fbxString.Value.Equals(Value);
            if (obj is string rawString)
                return rawString.Equals(Value);

            return base.Equals(obj);
        }
    }
}
