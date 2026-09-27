namespace RedFox.Graphics3D.D3D11;

/// <summary>
/// Represents a Direct3D 11 backend failure.
/// </summary>
/// <param name="message">The exception message.</param>
public sealed class D3D11Exception(string message) : InvalidOperationException(message);
