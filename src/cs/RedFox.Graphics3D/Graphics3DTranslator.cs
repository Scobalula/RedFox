using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// A class that defines a class for translating 3D data.
    /// </summary>
    public abstract class Graphics3DTranslator
    {
        /// <summary>
        /// Gets the name of the translator.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Gets whether or not this translator supports reading.
        /// </summary>
        public abstract bool SupportsReading { get; }

        /// <summary>
        /// Gets whether or not this translator supports writing.
        /// </summary>
        public abstract bool SupportsWriting { get; }

        /// <summary>
        /// Gets the file extensions this translator supports.
        /// </summary>
        public abstract string[] Extensions { get; }

        /// <summary>
        /// Translates data stored within the file and adds any valid data translated to the <see cref="Graphics3DScene"/>.
        /// </summary>
        /// <param name="filePath">The path of the file being translated.</param>
        /// <param name="scene">The <see cref="Graphics3DScene"/> the data is being loaded into.</param>
        public virtual void Read(string filePath, Graphics3DScene scene)
        {
            using var stream = File.OpenRead(filePath);
            Read(stream, filePath, scene);
        }

        /// <summary>
        /// Translates data stored within the stream and adds any valid data translated to the <see cref="Graphics3DScene"/>.
        /// </summary>
        /// <param name="stream">The stream to be translated.</param>
        /// <param name="filePath">The path of the file being translated.</param>
        /// <param name="scene">The <see cref="Graphics3DScene"/> the data is being translated into.</param>
        public abstract void Read(Stream stream, string filePath, Graphics3DScene scene);

        /// <summary>
        /// Translates the data to the provided file.
        /// </summary>
        /// <param name="filePath">The path of the file being translated.</param>
        /// <param name="scene">The <see cref="Graphics3DScene"/> the data is being translated from.</param>
        public virtual void Write(string filePath, Graphics3DScene scene)
        {
            using var stream = File.Create(filePath);
            Write(stream, filePath, scene);
        }

        /// <summary>
        /// Translates the data to the provided stream.
        /// </summary>
        /// <param name="stream">The stream to be translated.</param>
        /// <param name="filePath">The path of the file being translated.</param>
        /// <param name="scene">The <see cref="Graphics3DScene"/> the data is being translated from.</param>
        public abstract void Write(Stream stream, string filePath, Graphics3DScene scene);

        /// <summary>
        /// Checks if the provided input is supported by this translator.
        /// </summary>
        /// <param name="scene">The <see cref="Graphics3DScene"/> instance.</param>
        /// <param name="filePath">The path of the file being translated.</param>
        /// <param name="ext">The extension of path of the file being translated.</param>
        /// <returns>True if supported by this translator, otherwise false.</returns>
        public virtual bool IsValid(Graphics3DScene scene, string? filePath, string? ext) =>
            Extensions.Contains(ext);

        /// <summary>
        /// Checks if the provided input is supported by this translator.
        /// </summary>
        /// <param name="scene">The <see cref="Graphics3DScene"/> instance.</param>
        /// <param name="startOfFile">An initial buffer from the start of the file.</param>
        /// <param name="stream">The stream to be translated.</param>
        /// <param name="filePath">The path of the file being translated.</param>
        /// <param name="ext">The extension of path of the file being translated.</param>
        /// <returns>True if supported by this translator, otherwise false.</returns>
        public virtual bool IsValid(Graphics3DScene scene, Span<byte> startOfFile, Stream stream, string? filePath, string? ext) =>
            IsValid(scene, filePath, ext);
    }
}
