using System.Numerics;
using System.Text;

namespace RedFox.Graphics3D.SEModel
{
    /// <summary>
    /// A class to translate a Model to SEModel
    /// </summary>
    public sealed partial class SEModelTranslator : Graphics3DTranslator
    {
        /// <summary>
        /// SEModel Magic
        /// </summary>
        public static readonly byte[] Magic = [0x53, 0x45, 0x4D, 0x6F, 0x64, 0x65, 0x6C];

        /// <inheritdoc/>
        public override string Name => "SEModelTranslator";

        /// <inheritdoc/>
        public override string[] Extensions { get; } = [".semodel"];

        /// <inheritdoc/>
        public override bool SupportsReading => true;

        /// <inheritdoc/>
        public override bool SupportsWriting => true;

        /// <inheritdoc/>
        public override bool IsValid(Graphics3DScene scene, Span<byte> startOfFile, Stream stream, string? filePath, string? ext)
        {
            return !string.IsNullOrWhiteSpace(ext) && Extensions.Contains(ext);
        }

        /// <summary>
        /// Reads a UTF8 string from the file
        /// </summary>
        internal static string ReadUTF8String(BinaryReader reader)
        {
            var output = new StringBuilder(32);

            while (true)
            {
                var c = reader.ReadByte();
                if (c == 0)
                    break;
                output.Append(Convert.ToChar(c));
            }

            return output.ToString();
        }
    }
}