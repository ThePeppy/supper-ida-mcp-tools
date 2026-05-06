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
                "ida_find_installations",
                "Find IDA installations",
                "Locate IDA executables on the local machine.",
                new
                {
                    type = "object",
                    properties = new Dictionary<string, object?>
                    {
                        ["idaPath"] = new { type = "string", description = "Optional explicit executable, app bundle, or install directory." }
                    },
                    required = Array.Empty<string>()
                }),
            Tool(
                "ida_launch_file",
                "Launch IDA file",
                "Launch IDA with a selected input file and optionally wait for plugin registration.",
                new
                {
                    type = "object",
                    properties = new Dictionary<string, object?>
                    {
                        ["inputPath"] = new { type = "string", description = "Binary path to open in IDA." },
                        ["idaPath"] = new { type = "string", description = "Optional explicit executable, app bundle, or install directory." },
                        ["arguments"] = new { type = "array", items = new { type = "string" }, description = "Additional IDA CLI arguments before inputPath." },
                        ["waitSeconds"] = new { type = "integer", minimum = 0, maximum = 300, description = "Seconds to wait for the plugin to register the opened file." }
                    },
                    required = new[] { "inputPath" }
                }),
            Tool(
                "ida_list_launched_processes",
                "List launched IDA processes",
                "List IDA processes started by this center.",
                new
                {
                    type = "object",
                    properties = new Dictionary<string, object?>(),
                    required = Array.Empty<string>()
                }),
            Tool(
                "ida_close_target",
                "Close IDA target",
                "Close or optionally force-kill one registered IDA window by target id.",
                new
                {
                    type = "object",
                    properties = new Dictionary<string, object?>
                    {
                        ["instanceId"] = new { type = "string", description = "Registered IDA target instance id." },
                        ["force"] = new { type = "boolean", description = "Kill the process if the window does not close promptly." }
                    },
                    required = new[] { "instanceId" }
                }),
            TargetTool(
                "ida_list_functions",
                "List functions",
                "List functions from one IDA database.",
                new Dictionary<string, object?>
                {
                    ["offset"] = new { type = "integer", description = "Pagination offset.", minimum = 0 },
                    ["count"] = new { type = "integer", description = "Maximum rows to return.", minimum = 1, maximum = 1000 },
                    ["filter"] = new { type = "string", description = "Optional case-insensitive function name filter." }
                }),
            TargetTool(
                "ida_get_function",
                "Get function",
                "Resolve one function by address or name.",
                new Dictionary<string, object?>
                {
                    ["query"] = new { type = "string", description = "Function address, name, or address inside the function." }
                },
                "query"),
            TargetTool(
                "ida_decompile",
                "Decompile function",
                "Decompile one function in a selected IDA window.",
                new Dictionary<string, object?>
                {
                    ["query"] = new { type = "string", description = "Function address, name, or address inside the function." }
                },
                "query"),
            TargetTool(
                "ida_disassemble",
                "Disassemble function",
                "Return disassembly lines for one function.",
                new Dictionary<string, object?>
                {
                    ["query"] = new { type = "string", description = "Function address, name, or address inside the function." },
                    ["maxLines"] = new { type = "integer", description = "Maximum disassembly lines.", minimum = 1, maximum = 5000 }
                },
                "query"),
            TargetTool(
                "ida_xrefs",
                "List xrefs",
                "List code references to or from an address.",
                new Dictionary<string, object?>
                {
                    ["query"] = new { type = "string", description = "Address or symbol name." },
                    ["direction"] = new { type = "string", @enum = new[] { "to", "from", "both" }, description = "Reference direction." },
                    ["max"] = new { type = "integer", description = "Maximum xrefs.", minimum = 1, maximum = 5000 }
                },
                "query"),
            TargetTool(
                "ida_list_strings",
                "List strings",
                "List strings from one IDA database.",
                new Dictionary<string, object?>
                {
                    ["offset"] = new { type = "integer", description = "Pagination offset.", minimum = 0 },
                    ["count"] = new { type = "integer", description = "Maximum rows to return.", minimum = 1, maximum = 1000 },
                    ["filter"] = new { type = "string", description = "Optional case-insensitive text filter." }
                }),
            TargetTool(
                "ida_list_imports",
                "List imports",
                "List imports from one IDA database.",
                new Dictionary<string, object?>
                {
                    ["offset"] = new { type = "integer", description = "Pagination offset.", minimum = 0 },
                    ["count"] = new { type = "integer", description = "Maximum rows to return.", minimum = 1, maximum = 1000 },
                    ["filter"] = new { type = "string", description = "Optional case-insensitive import or module filter." }
                }),
            TargetTool(
                "ida_get_bytes",
                "Read bytes",
                "Read raw bytes from one IDA database.",
                new Dictionary<string, object?>
                {
                    ["address"] = new { type = "string", description = "Address or symbol name." },
                    ["size"] = new { type = "integer", description = "Byte count.", minimum = 1, maximum = 1048576 }
                },
                "address"),
            TargetTool(
                "ida_search_text",
                "Search disassembly text",
                "Regex search over generated disassembly text.",
                new Dictionary<string, object?>
                {
                    ["pattern"] = new { type = "string", description = "Case-insensitive regular expression." },
                    ["max"] = new { type = "integer", description = "Maximum hits.", minimum = 1, maximum = 1000 }
                },
                "pattern"),
            TargetTool(
                "ida_rename",
                "Rename address",
                "Rename an address or function in one IDA database.",
                new Dictionary<string, object?>
                {
                    ["address"] = new { type = "string", description = "Address or symbol name." },
                    ["newName"] = new { type = "string", description = "New IDA name." }
                },
                "address",
                "newName"),
            TargetTool(
                "ida_set_comment",
                "Set comment",
                "Set a regular or repeatable comment at one address.",
                new Dictionary<string, object?>
                {
                    ["address"] = new { type = "string", description = "Address or symbol name." },
                    ["text"] = new { type = "string", description = "Comment text." },
                    ["repeatable"] = new { type = "boolean", description = "Use repeatable comment slot." }
                },
                "address",
                "text"),
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

    private static McpToolDefinition TargetTool(
        string name,
        string title,
        string description,
        Dictionary<string, object?> properties,
        params string[] requiredProperties)
    {
        properties = new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase)
        {
            ["instanceId"] = new { type = "string", description = "Registered IDA target instance id." }
        };

        var required = new[] { "instanceId" }.Concat(requiredProperties).ToArray();
        return Tool(
            name,
            title,
            description,
            new
            {
                type = "object",
                properties,
                required
            });
    }
}
