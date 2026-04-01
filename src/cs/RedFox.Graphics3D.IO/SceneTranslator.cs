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

        /// <summary>
        /// Reads scene data from the specified file and populates the provided scene object.
        /// </summary>
        /// <param name="scene">The scene object to populate with data read from the file. Cannot be null.</param>
        /// <param name="filePath">The path to the file containing the scene data to read. Must refer to an existing file.</param>
        /// <param name="options">Options that control how the scene data is read and translated.</param>
        /// <param name="token">An optional cancellation token that can be used to cancel the read operation.</param>
        public virtual void Read(Scene scene, string filePath, SceneTranslatorOptions options, CancellationToken? token)
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            Read(scene, stream, CreateReadContext(filePath, options), token);
        }

        /// <summary>
        /// Reads scene data from the specified stream using the supplied translation context.
        /// </summary>
        /// <param name="scene">The scene object to populate with data read from the stream.</param>
        /// <param name="stream">The input stream containing scene data.</param>
        /// <param name="context">The translation context for this operation.</param>
        /// <param name="token">An optional cancellation token that can be used to cancel the read operation.</param>
        public virtual void Read(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
        {
            ArgumentNullException.ThrowIfNull(context);
            Read(scene, stream, context.Name, context.Options, token);
        }

        /// <summary>
        /// Reads scene data from the specified file and populates the provided scene object.
        /// </summary>
        /// <param name="scene">The scene object to populate with data read from the file.</param>
        /// <param name="name">The name of the file being read.</param>
        /// <param name="options">Options that control how the scene data is read and translated.</param>
        /// <param name="token">An optional cancellation token that can be used to cancel the read operation.</param>
        public abstract void Read(Scene scene, Stream stream, string name, SceneTranslatorOptions options, CancellationToken? token);

        /// <summary>
        /// Writes scene data to the specified file.
        /// </summary>
        /// <param name="scene">The scene object to write to the file.</param>
        /// <param name="filePath">The name of the file being written.</param>
        /// <param name="options">Options that control how the scene data is written and translated.</param>
        /// <param name="token">An optional cancellation token that can be used to cancel the write operation.</param>
        public virtual void Write(Scene scene, string filePath, SceneTranslatorOptions options, CancellationToken? token)
        {
            using var stream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.Asynchronous);
            Write(scene, stream, CreateWriteContext(filePath, options), token);
        }

        /// <summary>
        /// Writes the scene data to the specified stream using the supplied translation context.
        /// </summary>
        /// <param name="scene">The scene object to write to the stream.</param>
        /// <param name="stream">The stream to write the scene data to.</param>
        /// <param name="context">The translation context for this operation.</param>
        /// <param name="token">An optional cancellation token that can be used to cancel the write operation.</param>
        public virtual void Write(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
        {
            ArgumentNullException.ThrowIfNull(context);
            Write(scene, stream, context.Name, context.Options, token);
        }

        /// <summary>
        /// Writes the scene data to the specified stream.
        /// </summary>
        /// <param name="scene">The scene object to write to the stream.</param>
        /// <param name="stream">The stream to write the scene data to.</param>
        /// <param name="name">The name of the scene or file being written.</param>
        /// <param name="options">Options that control how the scene data is written and translated.</param>
        /// <param name="token">An optional cancellation token that can be used to cancel the write operation.</param>
        public abstract void Write(Scene scene, Stream stream, string name, SceneTranslatorOptions options, CancellationToken? token);

        /// <summary>
        /// Determines whether the specified file extension is supported for translation.
        /// </summary>
        /// <param name="filePath">The path of the file to validate.</param>
        /// <param name="ext">The file extension to validate, including the leading period (for example, ".obj").</param>
        /// <param name="context">The translation context.</param>
        /// <returns>true if the specified extension is supported; otherwise, false.</returns>
        public virtual bool IsValid(string filePath, string ext, SceneTranslationContext context) =>
            IsValid(filePath, ext, context.Options);

        /// <summary>
        /// Determines whether the specified file extension is supported for translation.
        /// </summary>
        /// <param name="filePath">The path of the file to validate.</param>
        /// <param name="ext">The file extension to validate, including the leading period (for example, ".obj").</param>
        /// <param name="options">The options to use when validating the file.</param>
        /// <returns>true if the specified extension is supported; otherwise, false.</returns>
        public virtual bool IsValid(string filePath, string ext, SceneTranslatorOptions options) =>
            Extensions.Contains(ext);

        /// <summary>
        /// Determines whether the specified file extension is supported for translation.
        /// </summary>
        /// <param name="filePath">The path of the file to validate.</param>
        /// <param name="ext">The file extension to validate, including the leading period (for example, ".obj").</param>
        /// <param name="context">The translation context.</param>
        /// <param name="startOfFile">A buffer of initial data from the start of the file.</param>
        /// <returns>true if the specified extension is supported; otherwise, false.</returns>
        public virtual bool IsValid(string filePath, string ext, SceneTranslationContext context, ReadOnlySpan<byte> startOfFile) =>
            IsValid(filePath, ext, context.Options, startOfFile);

        /// <summary>
        /// Determines whether the specified file extension is supported for translation.
        /// </summary>
        /// <param name="filePath">The path of the file to validate.</param>
        /// <param name="ext">The file extension to validate, including the leading period (for example, ".obj").</param>
        /// <param name="options">The options to use when validating the file.</param>
        /// <param name="startOfFile">A buffer of initial data from the start of the file.</param>
        /// <returns>true if the specified extension is supported; otherwise, false.</returns>
        public virtual bool IsValid(string filePath, string ext, SceneTranslatorOptions options, ReadOnlySpan<byte> startOfFile) =>
            IsValid(filePath, ext, options);

        protected internal static SceneTranslationContext CreateReadContext(string filePath, SceneTranslatorOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            string fullPath = Path.GetFullPath(filePath);
            options.SourceFilePath = fullPath;
            options.SourceDirectoryPath = Path.GetDirectoryName(fullPath);

            return new SceneTranslationContext(Path.GetFileNameWithoutExtension(filePath), options)
            {
                SourceFilePath = fullPath,
                SourceDirectoryPath = Path.GetDirectoryName(fullPath),
            };
        }

        protected internal static SceneTranslationContext CreateWriteContext(string filePath, SceneTranslatorOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            string fullPath = Path.GetFullPath(filePath);
            return new SceneTranslationContext(Path.GetFileNameWithoutExtension(filePath), options)
            {
                TargetFilePath = fullPath,
                TargetDirectoryPath = Path.GetDirectoryName(fullPath),
            };
        }
    }
}
