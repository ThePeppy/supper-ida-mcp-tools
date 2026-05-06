using System.Text.Json;

namespace SupperIdaMcp.Center.Mcp;

public sealed class McpToolCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IReadOnlyList<McpToolDefinition> _tools;

    public McpToolCatalog()
    {
        _tools =
        [
            Tool(
                "ida_list_targets",
                "List IDA targets",
                "List all IDA windows currently registered in the local center.",
                new
                {
                    type = "object",
                    properties = new Dictionary<string, object?>(),
                    required = Array.Empty<string>()
                }),
            Tool(
                "ida_get_target",
                "Get IDA target",
                "Get one registered IDA window by instance id.",
                new
                {
                    type = "object",
                    properties = new Dictionary<string, object?>
                    {
                        ["instanceId"] = new { type = "string", description = "Registered IDA target instance id." }
                    },
                    required = new[] { "instanceId" }
                }),
            Tool(
                "ida_ping_target",
                "Ping IDA target",
                "Send a lightweight command to a selected IDA window and return its response.",
                new
                {
                    type = "object",
                    properties = new Dictionary<string, object?>
                    {
                        ["instanceId"] = new { type = "string", description = "Registered IDA target instance id." }
                    },
                    required = new[] { "instanceId" }
                }),
            Tool(
                "ida_get_metadata",
                "Get IDA metadata",
                "Ask a selected IDA window to return current binary and database metadata.",
                new
                {
                    type = "object",
                    properties = new Dictionary<string, object?>
                    {
                        ["instanceId"] = new { type = "string", description = "Registered IDA target instance id." }
                    },
                    required = new[] { "instanceId" }
                }),
            Tool(
                "ida_call_tool",
                "Call IDA tool",
                "Call a center-registered plugin executor tool on a selected IDA window.",
                new
                {
                    type = "object",
                    properties = new Dictionary<string, object?>
                    {
                        ["instanceId"] = new { type = "string", description = "Registered IDA target instance id." },
                        ["tool"] = new { type = "string", description = "Plugin executor tool name." },
                        ["arguments"] = new { type = "object", description = "Tool arguments.", additionalProperties = true }
                    },
                    required = new[] { "instanceId", "tool" }
                }),
            Tool(
                "ida_operation_log",
                "List operation log",
                "List recent center-side agent operation log entries.",
                new
                {
                    type = "object",
                    properties = new Dictionary<string, object?>
                    {
                        ["limit"] = new { type = "integer", description = "Maximum entries to return.", minimum = 1, maximum = 1000 }
                    },
                    required = Array.Empty<string>()
                })
        ];
    }

    public IReadOnlyList<McpToolDefinition> ListTools() => _tools;

    private static McpToolDefinition Tool(
        string name,
        string title,
        string description,
        object inputSchema)
    {
        return new McpToolDefinition(
            name,
            title,
            description,
            JsonSerializer.SerializeToElement(inputSchema, JsonOptions));
    }
}
