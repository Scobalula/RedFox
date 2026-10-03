// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

namespace RedFox.Audio.IO;

/// <summary>
/// Manages a collection of audio translators and selects the appropriate translator for reading and writing files.
/// </summary>
public class AudioTranslatorManager
{
    private const int HeaderSize = 64;

    private readonly List<AudioTranslator> _translators = [];

    /// <summary>
    /// Gets the registered translators.
    /// </summary>
    public IReadOnlyList<AudioTranslator> Translators => _translators;

    /// <summary>
    /// Registers an audio translator, replacing any existing translator with the same name.
    /// </summary>
    /// <param name="translator">The translator to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when the translator is null.</exception>
    public void Register(AudioTranslator translator)
    {
        ArgumentNullException.ThrowIfNull(translator);

        _translators.RemoveAll(t => t.Name == translator.Name);
        _translators.Add(translator);
    }

    /// <summary>
    /// Unregisters a translator by name.
    /// </summary>
    /// <param name="name">The name of the translator to unregister.</param>
    /// <returns><see langword="true"/> if a translator was unregistered; otherwise <see langword="false"/>.</returns>
    public bool Unregister(string name) => _translators.RemoveAll(t => t.Name == name) > 0;

    /// <summary>
    /// Attempts to find a translator that can handle the specified file.
    /// </summary>
    /// <param name="filePath">The file path to check.</param>
    /// <param name="extension">The file extension.</param>
    /// <param name="header">The first bytes of the file for format validation.</param>
    /// <param name="translator">The translator that can handle the file, if found.</param>
    /// <returns><see langword="true"/> if a suitable translator was found; otherwise <see langword="false"/>.</returns>
    public bool TryGetTranslator(string filePath, string extension, ReadOnlySpan<byte> header, [NotNullWhen(true)] out AudioTranslator? translator)
    {
        foreach (AudioTranslator candidate in _translators)
        {
            if (candidate.CanRead && candidate.IsValid(header, filePath, extension))
            {
                translator = candidate;
                return true;
            }
        }

        translator = null;
        return false;
    }

    /// <summary>
    /// Reads an audio file from the specified path using the appropriate translator.
    /// </summary>
    /// <param name="filePath">The path to the audio file.</param>
    /// <returns>An <see cref="AudioClip"/> containing the audio data.</returns>
    /// <exception cref="NotSupportedException">Thrown when no suitable translator is found.</exception>
    public AudioClip Read(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        return Read(stream, filePath);
    }

    /// <summary>
    /// Reads an audio stream using the appropriate translator selected by file path.
    /// </summary>
    /// <param name="stream">The stream containing audio data.</param>
    /// <param name="filePath">The file path for translator selection (used to determine format).</param>
    /// <returns>An <see cref="AudioClip"/> containing the audio data.</returns>
    /// <exception cref="NotSupportedException">Thrown when no suitable translator is found.</exception>
    public AudioClip Read(Stream stream, string filePath)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanSeek)
        {
            using MemoryStream buffer = new();
            stream.CopyTo(buffer);
            buffer.Position = 0;
            return Read(buffer, filePath);
        }

        long start = stream.Position;
        Span<byte> header = stackalloc byte[HeaderSize];
        int headerSize = stream.ReadAtLeast(header, header.Length, false);
        stream.Position = start;

        if (!TryGetTranslator(filePath, Path.GetExtension(filePath), header[..headerSize], out AudioTranslator? translator))
            throw new NotSupportedException($"No suitable audio translator found for file: {filePath}");

        return translator.Read(stream);
    }

    /// <summary>
    /// Writes an audio clip to a file using the appropriate translator.
    /// </summary>
    /// <param name="filePath">The path where the file will be written.</param>
    /// <param name="clip">The audio clip to write.</param>
    /// <exception cref="NotSupportedException">Thrown when no suitable translator is found.</exception>
    public void Write(string filePath, AudioClip clip) => GetWriteTranslator(filePath).Write(filePath, clip);

    /// <summary>
    /// Writes an audio clip to a file with the specified options.
    /// </summary>
    /// <param name="filePath">The path where the file will be written.</param>
    /// <param name="clip">The audio clip to write.</param>
    /// <param name="options">The encoding options.</param>
    /// <exception cref="NotSupportedException">Thrown when no suitable translator is found.</exception>
    public void Write(string filePath, AudioClip clip, AudioTranslatorOptions options) => GetWriteTranslator(filePath).Write(filePath, clip, options);

    /// <summary>
    /// Writes an audio clip to a stream using the appropriate translator.
    /// </summary>
    /// <param name="stream">The stream where the file will be written.</param>
    /// <param name="filePath">The file path used to select the appropriate translator.</param>
    /// <param name="clip">The audio clip to write.</param>
    /// <exception cref="NotSupportedException">Thrown when no suitable translator is found.</exception>
    public void Write(Stream stream, string filePath, AudioClip clip) => GetWriteTranslator(filePath).Write(stream, clip);

    /// <summary>
    /// Writes an audio clip to a stream with the specified options.
    /// </summary>
    /// <param name="stream">The stream where the file will be written.</param>
    /// <param name="filePath">The file path used to select the appropriate translator.</param>
    /// <param name="clip">The audio clip to write.</param>
    /// <param name="options">The encoding options.</param>
    /// <exception cref="NotSupportedException">Thrown when no suitable translator is found.</exception>
    public void Write(Stream stream, string filePath, AudioClip clip, AudioTranslatorOptions options) => GetWriteTranslator(filePath).Write(stream, clip, options);

    private AudioTranslator GetWriteTranslator(string filePath)
    {
        string extension = Path.GetExtension(filePath);
        return _translators.Find(t => t.CanWrite && t.IsValid(filePath, extension)) ?? throw new NotSupportedException($"No suitable audio translator found for file: {filePath}");
    }
}
