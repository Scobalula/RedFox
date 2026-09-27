using System.Text.Json.Serialization;

namespace RedFox.Plugins;

internal sealed class PluginStateFile
{
    [JsonPropertyName("autoLoad")]
    public List<string>? AutoLoad { get; set; }
}
