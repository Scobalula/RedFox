using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace RedFox.Graphics3D.IO
{
    /// <summary>
    /// Provides an abstract base class for scene translators, defining the contract for reading and writing scene data.
    /// </summary>
    public abstract class SceneTranslator
    {
        /// <summary>
        /// Gets the name of the translator, which is used for identification and selection when multiple translators are available.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Gets whether the translator supports reading operations.
        /// </summary>
        public abstract bool CanRead { get; }

        /// <summary>
        /// Gets whether the translator supports writing operations.
        /// </summary>
        public abstract bool CanWrite { get; }

        /// <summary>
        /// Gets the extensions associated with this translator, which are used to determine if the translator can handle a given file based on its extension.
        /// </summary>
        public abstract IReadOnlyList<string> Extensions { get; }

        /// <summary>
        /// Gets the magic value used to identify the file format. 
        /// </summary>
        public virtual ReadOnlySpan<byte> MagicValue => [];

        public virtual void Read(Scene scene, string filePath, SceneTranslatorOptions options, CancellationToken? token)
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            // Pass filename without extension as the stream-based Read name parameter.
            Read(scene, stream, Path.GetFileNameWithoutExtension(filePath), options, token);
        }

        /// <summary>
        /// Legacy stream-based Read method. Existing translators override this method.
        /// New translators can optionally override the name-aware overload instead.
        /// </summary>
        public abstract void Read(Scene scene, Stream stream, string name, SceneTranslatorOptions options, CancellationToken? token);


        public virtual void Write(string filePath, Scene scene, SceneTranslatorOptions options, CancellationToken? token)
        {
            using var stream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.Asynchronous);
            Write(scene, stream, Path.GetFileNameWithoutExtension(filePath), options, token);
        }

        /// <summary>
        /// Legacy stream-based Write method. Existing translators override this method.
        /// New translators can optionally override the name-aware overload instead.
        /// </summary>
        public abstract void Write(Scene scene, Stream stream, string name, SceneTranslatorOptions options, CancellationToken? token);

        public virtual bool IsValid(string filePath, string ext, SceneTranslatorOptions options) =>
            Extensions.Contains(ext);

        public virtual bool IsValid(ReadOnlySpan<byte> startOfFile, string filePath, string ext, SceneTranslatorOptions options) =>
            IsValid(filePath, ext, options);
    }
}
