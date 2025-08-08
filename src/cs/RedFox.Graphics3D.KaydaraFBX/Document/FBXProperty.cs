using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D.KaydaraFBX.Document
{
    /// <summary>
    /// A class to hold an FBX Property.
    /// </summary>
    [DebuggerDisplay("Type: {DataType}")]
    public abstract class FBXProperty
    {
        /// <summary>
        /// Gets the FBX data type.
        /// </summary>
        public abstract FBXPropertyType DataType { get; }

        /// <summary>
        /// Gets the total size of the property value/s in bytes.
        /// </summary>
        /// <returns>The total size of the value/s in bytes..</returns>
        internal abstract int GetSize();

        /// <summary>
        /// Reads the value/s.
        /// </summary>
        /// <param name="writer">The reader to read the value/s from.</param>
        internal abstract void Read(BinaryReader reader);

        /// <summary>
        /// Writes the value/s in bytes.
        /// </summary>
        /// <param name="writer">The writer to write the value/s to.</param>
        internal abstract void Write(BinaryWriter writer);

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T Cast<T>() where T : FBXProperty => (T)this;
    }
}
