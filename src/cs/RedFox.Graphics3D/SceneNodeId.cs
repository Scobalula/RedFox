namespace RedFox.Graphics3D;

/// <summary>
/// Provides functionality for generating unique identifiers for scene nodes in a thread-safe manner.
/// </summary>
public static class SceneNodeId
{
    private static int _idCounter = 0;

    /// <summary>
    /// Generates and returns a unique identifier for a scene node in a thread-safe manner.
    /// </summary>
    /// <returns>A unique integer identifier that can be used to distinguish scene nodes.</returns>
    public static int GetNextId() => Interlocked.Increment(ref _idCounter);
}