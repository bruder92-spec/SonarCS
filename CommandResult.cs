using System.Text.Json.Serialization;

namespace Sonar;

public sealed class CommandResult
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = "unknown";

    [JsonPropertyName("args")]
    public Dictionary<string, string> Args { get; set; } = new();

    public string Arg(string key) =>
        Args.TryGetValue(key, out var v) ? v : string.Empty;
}
