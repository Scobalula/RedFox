using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;

using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.IO
{
    /// <summary>
    /// Manages scene translators and coordinates import/export plus manager-side merge behavior.
    /// </summary>
    public class SceneTranslatorManager
    {
        private readonly List<SceneTranslator> _translators = [];

        private const int DefaultHeaderSize = 256;

        /// <summary>
        /// Gets a read-only list of all registered translators.
        /// </summary>
        public IReadOnlyList<SceneTranslator> Translators => _translators;

        /// <summary>
        /// Registers a scene translator, replacing any existing translator with the same name.
        /// </summary>
        /// <param name="translator">The scene translator to register. If a translator with the same name already exists, it will be replaced.</param>
        public void Register(SceneTranslator translator)
        {
            _translators.RemoveAll(t => t.Name == translator.Name);
            _translators.Add(translator);
        }

        /// <summary>
        /// Unregisters a translator with the specified name.
        /// </summary>
        /// <param name="name">The name of the translator to remove. The comparison is case-sensitive.</param>
        /// <returns>true if a translator with the specified name was found and removed; otherwise, false.</returns>
        public bool Unregister(string name)
        {
            return _translators.RemoveAll(t => t.Name == name) > 0;
        }

        /// <summary>
        /// Attempts to find a suitable scene translator for the specified file and options.
        /// </summary>
        /// <param name="filePath">The full path to the file for which a translator is requested.</param>
        /// <param name="extension">The file extension, including the leading period (e.g., ".obj").</param>
        /// <param name="options">Options that influence how the translator is selected or configured.</param>
        /// <param name="translator">The translator if found, otherwise null.</param>
        /// <returns>true if found, otherwise false.</returns>
        public bool TryGetTranslator(string filePath, string extension, SceneTranslatorOptions options, [NotNullWhen(true)] out SceneTranslator? translator)
            => TryGetTranslator(filePath, extension, new SceneTranslationContext(Path.GetFileNameWithoutExtension(filePath), options), out translator);

        /// <summary>
        /// Attempts to find a suitable scene translator for the specified file and translation context.
        /// </summary>
        public bool TryGetTranslator(string filePath, string extension, SceneTranslationContext context, [NotNullWhen(true)] out SceneTranslator? translator)
        {
            foreach (var potentialTranslator in Translators)
            {
                if (potentialTranslator.IsValid(filePath, extension, context))
                {
                    translator = potentialTranslator;
                    return true;
                }
            }

            translator = null;
            return false;
        }

        /// <summary>
        /// Attempts to find a suitable scene translator for the specified file and options.
        /// </summary>
        /// <param name="filePath">The full path to the file for which a translator is requested.</param>
        /// <param name="extension">The file extension, including the leading period (e.g., ".obj").</param>
        /// <param name="header">A read-only span containing the initial bytes of the file.</param>
        /// <param name="options">Options that influence how the translator is selected or configured.</param>
        /// <param name="translator">The translator if found, otherwise null.</param>
        /// <returns>true if found, otherwise false.</returns>
        public bool TryGetTranslator(string filePath, string extension, ReadOnlySpan<byte> header, SceneTranslatorOptions options, [NotNullWhen(true)] out SceneTranslator? translator)
            => TryGetTranslator(filePath, extension, header, new SceneTranslationContext(Path.GetFileNameWithoutExtension(filePath), options), out translator);

        /// <summary>
        /// Attempts to find a suitable scene translator for the specified file and translation context.
        /// </summary>
        public bool TryGetTranslator(string filePath, string extension, ReadOnlySpan<byte> header, SceneTranslationContext context, [NotNullWhen(true)] out SceneTranslator? translator)
        {
            foreach (var potentialTranslator in Translators)
            {
                if (potentialTranslator.IsValid(filePath, extension, context, header))
                {
                    translator = potentialTranslator;
                    return true;
                }
            }

            translator = null;
            return false;
        }

        /// <summary>
        /// Reads a scene from the specified file and returns a new Scene instance populated with the file's contents.
        /// </summary>
        /// <param name="filePath">The path to the file containing the scene data to read. Cannot be null or empty.</param>
        /// <param name="options">The options that control how the scene is translated during the read operation.</param>
        /// <param name="token">An optional cancellation token that can be used to cancel the read operation.</param>
        /// <returns>A new Scene instance containing the data read from the specified file.</returns>
        public Scene Read(string filePath, SceneTranslatorOptions options, CancellationToken? token)
        {
            var scene = new Scene(Path.GetFileName(filePath));
            Read(filePath, scene, options, token);
            return scene;
        }


        public void Read(string filePath, Scene scene, SceneTranslatorOptions options, CancellationToken? token)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            using var stream = File.OpenRead(filePath);
            Read(stream, filePath, scene, options, token);
        }

        public Scene Read(Stream stream, string filePath, SceneTranslatorOptions options, CancellationToken? token)
        {
            var scene = new Scene(Path.GetFileName(filePath));
            Read(stream, filePath, scene, options, token);
            return scene;
        }

        public void Read(Stream stream, string filePath, Scene scene, SceneTranslatorOptions options, CancellationToken? token)
        {
            if (!stream.CanRead)
                throw new IOException("The supplied stream is not readable.");
            if (!stream.CanSeek)
                throw new IOException("The supplied stream must support seeking.");

            ArgumentNullException.ThrowIfNull(options);

            var extension = Path.GetExtension(filePath);
            var readStart = stream.Position;
            SceneTranslationContext context = SceneTranslator.CreateReadContext(filePath, options);

            Span<byte> header = stackalloc byte[DefaultHeaderSize];
            var headerSize = stream.Read(header);

            if (!TryGetTranslator(filePath, extension, header[..headerSize], context, out var translator))
                throw new IOException($"No suitable translator found for file: {filePath}");

            stream.Position = readStart;
            translator.Read(scene, stream, context, token);
        }

        public void Write(string filePath, Scene scene, SceneTranslatorOptions options, CancellationToken? token)
        {
            var extension = Path.GetExtension(filePath);
            SceneTranslationContext context = SceneTranslator.CreateWriteContext(filePath, options);

            if (!TryGetTranslator(filePath, extension, context, out var translator))
                throw new IOException($"No suitable translator found for file: {filePath}");

            using var stream = File.Create(filePath);
            translator.Write(scene, stream, context, token);
        }

        public void Write(Stream stream, string filePath, Scene scene, SceneTranslatorOptions options, CancellationToken? token)
        {
            var extension = Path.GetExtension(filePath);
            SceneTranslationContext context = SceneTranslator.CreateWriteContext(filePath, options);

            if (!TryGetTranslator(filePath, extension, context, out var translator))
                throw new IOException($"No suitable translator found for file: {filePath}");

            translator.Write(scene, stream, context, token);
        }
    }
}
