namespace RedFox.Graphics3D.D3D11;

/// <summary>
/// Represents a Direct3D 11 backend failure.
/// </summary>
public sealed class D3D11Exception : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="D3D11Exception"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public D3D11Exception(string message)
        : base(message)
    {
    }
}
