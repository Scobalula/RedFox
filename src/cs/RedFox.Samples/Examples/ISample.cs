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
/// Defines a runnable sample command.
/// </summary>
internal interface ISample
{
    /// <summary>
    /// Gets the sample command name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a short sample description.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Executes the sample.
    /// </summary>
    /// <param name="arguments">Sample-specific arguments.</param>
    /// <returns>A process exit code.</returns>
    int Run(string[] arguments);
}
