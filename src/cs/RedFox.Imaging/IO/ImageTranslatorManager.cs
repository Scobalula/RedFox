using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace RedFox.Imaging.IO;

/// <summary>
/// Manages an explicitly registered collection of <see cref="ImageTranslator"/> instances and reads and writes images by selecting the appropriate translator.
/// </summary>
/// <remarks>
/// No translators are registered by default. Register them individually, or register the built-in set from the formats package.
/// </remarks>
public class ImageTranslatorManager
{
    private const int DefaultHeaderSize = 256;

    private readonly List<ImageTranslator> _translators = [];

    /// <summary>
    /// Gets a read-only list of all registered translators.
    /// </summary>
    public IReadOnlyList<ImageTranslator> Translators => _translators;

    /// <summary>
    /// Gets or sets the maximum number of pixel bytes an image may occupy when read through this manager.
    /// Enforced before decoding for translators that support <see cref="ImageTranslator.ReadInfo(Stream)"/>.
    /// </summary>
    public long MaxImageBytes { get; set; } = Array.MaxLength;

    /// <summary>
    /// Registers an image translator. Replaces any existing translator with the same name.
    /// </summary>
    /// <param name="translator">The translator to register.</param>
    public void Register(ImageTranslator translator)
    {
        ArgumentNullException.ThrowIfNull(translator);

        _translators.RemoveAll(t => t.Name == translator.Name);
        _translators.Add(translator);
    }

    /// <summary>
    /// Registers multiple image translators.
    /// </summary>
    /// <param name="translators">The translators to register.</param>
    public void RegisterRange(IEnumerable<ImageTranslator> translators)
    {
        ArgumentNullException.ThrowIfNull(translators);

        foreach (ImageTranslator translator in translators)
            Register(translator);
    }

    /// <summary>
    /// Removes a previously registered translator by name.
    /// </summary>
    /// <param name="name">The name of the translator to remove.</param>
    /// <returns><c>true</c> if a translator was removed; otherwise, <c>false</c>.</returns>
    public bool Unregister(string name)
    {
        return _translators.RemoveAll(t => t.Name == name) > 0;
    }

    /// <summary>
    /// Attempts to find a translator that supports the given file by extension only.
    /// </summary>
    /// <param name="filePath">The path of the file to translate.</param>
    /// <param name="extension">The file extension used to select a translator.</param>
    /// <param name="translator">When this method returns <c>true</c>, the selected translator.</param>
    /// <returns><c>true</c> when a matching translator is found; otherwise, <c>false</c>.</returns>
    public bool TryGetTranslator(string filePath, string extension, [NotNullWhen(true)] out ImageTranslator? translator)
    {
        translator = _translators.Find(t => t.IsValid(filePath, extension));
        return translator is not null;
    }

    /// <summary>
    /// Attempts to find a translator that supports the given file by extension and header bytes.
    /// </summary>
    /// <param name="filePath">The path of the file to translate.</param>
    /// <param name="extension">The file extension used to select a translator.</param>
    /// <param name="header">The leading bytes of the file used for format detection.</param>
    /// <param name="translator">When this method returns <c>true</c>, the selected translator.</param>
    /// <returns><c>true</c> when a matching translator is found; otherwise, <c>false</c>.</returns>
    public bool TryGetTranslator(string filePath, string extension, ReadOnlySpan<byte> header, [NotNullWhen(true)] out ImageTranslator? translator)
    {
        foreach (ImageTranslator candidate in _translators)
        {
            if (candidate.IsValid(header, filePath, extension))
            {
                translator = candidate;
                return true;
            }
        }

        translator = null;
        return false;
    }

    /// <summary>
    /// Selects the translator for the image at the current position of a seekable stream.
    /// The stream position is restored before returning.
    /// </summary>
    /// <param name="stream">The seekable stream positioned at the start of the image data.</param>
    /// <param name="filePath">The file path (used for extension matching).</param>
    /// <returns>The selected translator; its <see cref="ImageTranslator.Name"/> identifies the container.</returns>
    /// <exception cref="NotSupportedException">Thrown when no registered translator matches.</exception>
    public ImageTranslator GetTranslator(Stream stream, string filePath)
    {
        ArgumentNullException.ThrowIfNull(stream);

        long start = stream.Position;
        Span<byte> header = stackalloc byte[DefaultHeaderSize];
        int headerSize = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        stream.Position = start;

        if (headerSize <= 0)
            throw new IOException($"Failed to read header from stream for file: {filePath}");
        if (!TryGetTranslator(filePath, Path.GetExtension(filePath), header[..headerSize], out ImageTranslator? translator))
            throw new NotSupportedException($"No suitable image translator found for file: {filePath}");

        return translator;
    }

    /// <summary>
    /// Reads an image from a file, automatically selecting the appropriate translator.
    /// </summary>
    /// <param name="filePath">The path to the image file.</param>
    /// <returns>The loaded <see cref="Image"/>.</returns>
    public Image Read(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        return Read(stream, filePath);
    }

    /// <summary>
    /// Reads an image from a stream, using the file path for extension matching and the stream's leading bytes for header validation.
    /// Non-seekable streams are buffered in memory first.
    /// </summary>
    /// <param name="stream">The stream containing image data, positioned at its start.</param>
    /// <param name="filePath">The file path (used for extension matching).</param>
    /// <returns>The loaded <see cref="Image"/>.</returns>
    /// <exception cref="InvalidDataException">Thrown when the image exceeds <see cref="MaxImageBytes"/> or is malformed.</exception>
    public Image Read(Stream stream, string filePath)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanSeek)
            return Read(BufferStream(stream), filePath);

        ImageTranslator translator = GetTranslator(stream, filePath);

        if (translator.CanReadInfo)
        {
            long start = stream.Position;
            ValidateSize(translator.ReadInfo(stream), filePath);
            stream.Position = start;
        }

        return translator.Read(stream);
    }

    /// <summary>
    /// Reads the dimensions, format, and layout of an image file without decoding pixel data.
    /// </summary>
    /// <param name="filePath">The path to the image file.</param>
    /// <returns>The layout of the image.</returns>
    public ImageInfo ReadInfo(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        return ReadInfo(stream, filePath);
    }

    /// <summary>
    /// Reads the dimensions, format, and layout of an image in a stream without decoding pixel data.
    /// Use <see cref="GetTranslator(Stream, string)"/> to identify the container.
    /// </summary>
    /// <param name="stream">The stream containing image data, positioned at its start.</param>
    /// <param name="filePath">The file path (used for extension matching).</param>
    /// <returns>The layout of the image.</returns>
    /// <exception cref="NotSupportedException">Thrown when the matching translator cannot read metadata on its own.</exception>
    public ImageInfo ReadInfo(Stream stream, string filePath)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanSeek)
            return ReadInfo(BufferStream(stream), filePath);

        return GetTranslator(stream, filePath).ReadInfo(stream);
    }

    /// <summary>
    /// Writes an image to a file, automatically selecting the appropriate translator based on extension.
    /// </summary>
    /// <param name="filePath">The destination file path.</param>
    /// <param name="image">The image to write.</param>
    public void Write(string filePath, Image image)
    {
        GetWriteTranslator(filePath).Write(filePath, image);
    }

    /// <summary>
    /// Writes an image to a file using the specified per-call translation options.
    /// </summary>
    /// <param name="filePath">The destination file path.</param>
    /// <param name="image">The image to write.</param>
    /// <param name="options">Per-call translation hints such as quality, compression preference, and bit depth.</param>
    public void Write(string filePath, Image image, ImageTranslatorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        GetWriteTranslator(filePath).Write(filePath, image, options);
    }

    /// <summary>
    /// Writes an image to a stream, using the file path for extension-based translator selection.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="filePath">The file path (used for extension matching).</param>
    /// <param name="image">The image to write.</param>
    public void Write(Stream stream, string filePath, Image image)
    {
        GetWriteTranslator(filePath).Write(stream, image);
    }

    /// <summary>
    /// Writes an image to a stream using the specified per-call translation options.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="filePath">The file path (used for extension matching).</param>
    /// <param name="image">The image to write.</param>
    /// <param name="options">Per-call translation hints such as quality, compression preference, and bit depth.</param>
    public void Write(Stream stream, string filePath, Image image, ImageTranslatorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        GetWriteTranslator(filePath).Write(stream, image, options);
    }

    private ImageTranslator GetWriteTranslator(string filePath)
    {
        if (!TryGetTranslator(filePath, Path.GetExtension(filePath), out ImageTranslator? translator))
            throw new NotSupportedException($"No suitable image translator found for file: {filePath}");

        return translator;
    }

    private void ValidateSize(ImageInfo info, string filePath)
    {
        long byteCount = info.CalculateTotalByteCount();

        if (byteCount > MaxImageBytes)
            throw new InvalidDataException($"Image '{filePath}' requires {byteCount} bytes, which exceeds the limit of {MaxImageBytes} bytes.");
    }

    private static MemoryStream BufferStream(Stream stream)
    {
        MemoryStream buffer = new();
        stream.CopyTo(buffer);
        buffer.Position = 0;
        return buffer;
    }
}
