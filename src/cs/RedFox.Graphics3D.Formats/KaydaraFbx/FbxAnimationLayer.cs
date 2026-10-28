using System.Numerics;

namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Represents an FBX animation layer and the animated properties it contains.
/// </summary>
/// <param name="weight">The layer weight from zero to one.</param>
/// <param name="isAdditive">Whether the layer adds to the layers below it instead of overriding them.</param>
public sealed class FbxAnimationLayer(float weight, bool isAdditive)
{
    /// <summary>
    /// Gets the layer weight from zero to one.
    /// </summary>
    public float Weight { get; } = weight;

    /// <summary>
    /// Gets a value indicating whether the layer adds to the layers below it instead of overriding them.
    /// </summary>
    public bool IsAdditive { get; } = isAdditive;

    /// <summary>
    /// Gets the animated properties keyed by object identifier and then property name.
    /// </summary>
    public Dictionary<long, Dictionary<string, FbxAnimationChannel>> Channels { get; } = [];

    /// <summary>
    /// Returns the channel for an object property, creating it when missing.
    /// </summary>
    /// <param name="objectId">The animated object identifier.</param>
    /// <param name="property">The animated property name.</param>
    /// <param name="defaultValue">The default value used when the channel is created.</param>
    /// <returns>The channel.</returns>
    public FbxAnimationChannel GetOrCreate(long objectId, string property, Vector3 defaultValue)
    {
        if (!Channels.TryGetValue(objectId, out Dictionary<string, FbxAnimationChannel>? properties))
            Channels[objectId] = properties = new Dictionary<string, FbxAnimationChannel>(StringComparer.Ordinal);

        if (!properties.TryGetValue(property, out FbxAnimationChannel? channel))
            properties[property] = channel = new FbxAnimationChannel(defaultValue);

        return channel;
    }

    /// <summary>
    /// Returns the channel for an object property, if any.
    /// </summary>
    /// <param name="objectId">The animated object identifier.</param>
    /// <param name="property">The animated property name.</param>
    /// <returns>The channel, or <see langword="null"/>.</returns>
    public FbxAnimationChannel? Get(long objectId, string property)
    {
        return Channels.TryGetValue(objectId, out Dictionary<string, FbxAnimationChannel>? properties) ? properties.GetValueOrDefault(property) : null;
    }
}
