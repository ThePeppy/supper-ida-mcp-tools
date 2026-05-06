using System.Text.Json;
using System.Text.Json.Serialization;

namespace SupperIdaMcp.Center.Protocol;

public sealed record ProtocolMessage
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("instanceId")]
    public string? InstanceId { get; init; }

    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; init; }

    public static ProtocolMessage Create(string type, string? instanceId = null, object? payload = null)
    {
        return new ProtocolMessage
        {
            Type = type,
            InstanceId = instanceId,
            Payload = payload is null ? null : JsonSerializer.SerializeToElement(payload)
        };
    }
}
