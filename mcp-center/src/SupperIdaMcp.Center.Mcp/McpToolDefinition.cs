using System.Text.Json;

namespace SupperIdaMcp.Center.Mcp;

public sealed record McpToolDefinition(
    string Name,
    string Title,
    string Description,
    JsonElement InputSchema);
