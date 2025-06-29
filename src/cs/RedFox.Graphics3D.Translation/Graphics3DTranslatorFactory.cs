using RedFox.Graphics3D.Cast;
using RedFox.Graphics3D.SEAnim;
using RedFox.Graphics3D.SEModel;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D.Translation
{
    /// <summary>
    /// A class to handle obtaining valid a <see cref="Graphics3DTranslator"/> given arbitrary data.
    /// </summary>
    public class Graphics3DTranslatorFactory
    {
        /// <summary>
        /// Gets the supported translators.
        /// </summary>
        public List<Graphics3DTranslator> Translators { get; } = [];

        /// <summary>
        /// Registers the provided translator for use in file translation.
        /// </summary>
        /// <param name="translator">The translator to register.</param>
        public void RegisterTranslator(Graphics3DTranslator translator)
        {
            Translators.Add(translator);
        }

        /// <summary>
        /// Registers the provided translator for use in file translation.
        /// </summary>
        /// <param name="translator">The translator to register.</param>
        /// <returns>The current <see cref="Graphics3DTranslatorFactory"/>.</returns>
        public Graphics3DTranslatorFactory WithTranslator(Graphics3DTranslator translator)
        {
            RegisterTranslator(translator);
            return this;
        }

        /// <summary>
        /// Registers default translators that Borks supports.
        /// </summary>
        /// <returns>The current <see cref="Graphics3DTranslatorFactory"/>.</returns>
        public Graphics3DTranslatorFactory WithDefaultTranslators()
        {
            Translators.Add(new SEModelTranslator());
            Translators.Add(new SEAnimTranslator());
            Translators.Add(new CastTranslator());
            //Translators.Add(new SEModelTranslator());
            //Translators.Add(new SMDTranslator());
            //Translators.Add(new CoDXAnimTranslator());
            //Translators.Add(new CoDXModelTranslator());

            return this;
        }

        /// <summary>
        /// Attempts to read from the provided file.
        /// </summary>
        /// <param name="filePath">Full file path.</param>
        /// <param name="io">The results from the translator.</param>
        /// <returns>True if a valid translator was found, otherwise false.</returns>
        public bool TryLoadFile(Graphics3DScene scene, string filePath)
        {
            using var stream = File.OpenRead(filePath);
            return TryLoadStream(scene, stream, filePath);
        }

        /// <summary>
        /// Attempts to read from the provided stream.
        /// </summary>
        /// <param name="stream">The stream that contains the data.</param>
        /// <param name="filePath">Full file path.</param>
        /// <param name="io">The results from the translator.</param>
        /// <returns>True if a valid translator was found, otherwise false.</returns>
        public bool TryLoadStream(Graphics3DScene scene, Stream stream, string filePath)
        {
            if(TryGetTranslator(scene, stream, filePath, out var translator) && translator.SupportsReading)
            {
                translator.Read(stream, filePath, scene);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filePath">Full file path.</param>
        /// <param name="io"></param>
        /// <returns>True if a valid translator was found, otherwise false.</returns>
        public bool TrySaveFile(Graphics3DScene scene, string filePath)
        {
            using var stream = File.Create(filePath);
            return TrySaveStream(scene, stream, filePath);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="filePath">Full file path.</param>
        /// <param name="io"></param>
        /// <returns>True if a valid translator was found, otherwise false.</returns>
        public bool TrySaveStream(Graphics3DScene scene, Stream stream, string filePath)
        {
            if (TryGetTranslator(scene, null, filePath, out var translator) && translator.SupportsReading)
            {
                translator.Write(stream, filePath, scene);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to get the translator for the given stream with the given file name.
        /// </summary>
        /// <param name="stream">The stream that contains the data, if null, only the file name is used for checks.</param>
        /// <param name="filePath">Full file path.</param>
        /// <param name="translator">The resulting translator for the given stream, if not found, this will be null</param>
        /// <returns>True if found, otherwise false.</returns>
        public bool TryGetTranslator(Graphics3DScene scene, Stream? stream, string filePath,[NotNullWhen(true)] out Graphics3DTranslator? translator)
        {
            var extension = Path.GetExtension(filePath);

            if(stream != null)
            {
                Span<byte> buffer = stackalloc byte[512];
                int bytes = stream.Read(buffer);
                stream.Seek(0, SeekOrigin.Begin);
                var exactBuffer = buffer[..bytes];

                foreach (var potentialTranslator in Translators)
                {
                    if (potentialTranslator.IsValid(scene, exactBuffer, stream, filePath, extension))
                    {
                        translator = potentialTranslator;
                        return true;
                    }
                }
            }
            else
            {
                foreach (var potentialTranslator in Translators)
                {
                    if (potentialTranslator.IsValid(scene, filePath, extension))
                    {
                        translator = potentialTranslator;
                        return true;
                    }
                }
            }

            translator = null;
            return false;
        }

        public T Load<T>(Stream stream, string name) where T : Graphics3DObject
        {
            var type = typeof(T);
            var scene = new Graphics3DScene();

            if (!TryLoadStream(scene, stream, name))
                throw new Unknown3DFileFormatException();

            var result = scene.Objects.FirstOrDefault(x => x.GetType() == type);

            if (result == null)
                throw new Empty3DFileException();

            return (T)result;
        }

        public T Load<T>(string filePath) where T : Graphics3DObject
        {
            using var stream = File.OpenRead(filePath);
            return Load<T>(stream, filePath);
        }

        public bool TryLoad<T>(Stream stream, string name, [NotNullWhen(true)] out T? result) where T : Graphics3DObject
        {
            var scene = new Graphics3DScene();

            if (!TryLoadStream(scene, stream, name))
                throw new Unknown3DFileFormatException();

            result = scene.GetFirstInstance<T>();
            return result != null;
        }

        public bool TryLoad<T>(string? filePath,[NotNullWhen(true)] out T? result) where T : Graphics3DObject
        {
            result = null;

            if (filePath is null)
                return false;

            using var stream = File.OpenRead(filePath);
            return TryLoad(stream, filePath, out result);
        }

        public void Save<T>(Stream stream, string name, T data) where T : Graphics3DObject
        {
            if (!TrySaveStream(new(data), stream, name))
                throw new Unknown3DFileFormatException();
        }

        public void Save<T>(string filePath, T data) where T : Graphics3DObject
        {
            using var stream = File.Create(filePath);
            Save(stream, filePath, data);
        }

        public string[] GetWritableExtensions() => Translators.Where(x => x.SupportsWriting).Select(x => x.Extensions[0]).ToArray();
    }
}
