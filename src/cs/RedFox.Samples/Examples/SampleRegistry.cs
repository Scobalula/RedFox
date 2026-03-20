// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
namespace RedFox.Samples.Examples;

/// <summary>
/// Provides registration for all available samples.
/// </summary>
internal static class SampleRegistry
{
    /// <summary>
    /// Creates a registry of available sample commands.
    /// </summary>
    /// <returns>A dictionary of sample name to sample implementation.</returns>
    public static IReadOnlyDictionary<string, ISample> Create()
    {
        ISample[] sampleDefinitions =
        [
            new BytePatternParseSample(),
            new BinaryReaderWriterSample(),
            new SpanReaderSample(),
            new StreamScanSample(),
            new VirtualFileSystemSample(),
            new MurMur3Sample(),
            new CompressionSample(),
            new ProcessFinderSample(),
            new ProcessReadWriteSample()
        ];

        Dictionary<string, ISample> samples = new(StringComparer.OrdinalIgnoreCase);
        foreach (ISample sampleDefinition in sampleDefinitions)
        {
            samples.Add(sampleDefinition.Name, sampleDefinition);
        }

        return samples;
    }
}
