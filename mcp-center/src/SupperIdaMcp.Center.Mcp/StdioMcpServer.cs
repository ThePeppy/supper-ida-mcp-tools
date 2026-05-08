using System.Text;
using System.Text.Json;

namespace SupperIdaMcp.Center.Mcp;

public sealed class StdioMcpServer
{
    private const string FallbackProtocolVersion = "2025-06-18";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly McpToolCatalog _toolCatalog;
    private readonly IdaMcpToolHandler _toolHandler;

    public StdioMcpServer(McpToolCatalog toolCatalog, IdaMcpToolHandler toolHandler)
    {
        _toolCatalog = toolCatalog;
        _toolHandler = toolHandler;
    }

    public async Task RunAsync(Stream input, Stream output, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(input, Encoding.UTF8, leaveOpen: true);
        await using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true)
        {
            AutoFlush = true,
            NewLine = "\n"
        };

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            await HandleLineAsync(line, writer, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task HandleLineAsync(
        string line,
        StreamWriter writer,
        CancellationToken cancellationToken)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (!root.TryGetProperty("method", out var methodElement)
                || methodElement.ValueKind != JsonValueKind.String)
            {
                await WriteErrorAsync(writer, TryGetId(root), -32600, "Invalid JSON-RPC request.").ConfigureAwait(false);
                return;
            }

            var method = methodElement.GetString()!;
            var hasId = root.TryGetProperty("id", out var idElement);
            var id = hasId ? idElement.Clone() : default(JsonElement?);
            var parameters = root.TryGetProperty("params", out var rawParams)
                ? rawParams.Clone()
                : default(JsonElement?);

            if (!hasId)
            {
                return;
            }

            switch (method)
            {
                case "initialize":
                    await WriteResultAsync(writer, id!.Value, Initialize(parameters)).ConfigureAwait(false);
                    break;
                case "ping":
                    await WriteResultAsync(writer, id!.Value, new Dictionary<string, object?>()).ConfigureAwait(false);
                    break;
                case "tools/list":
                    await WriteResultAsync(writer, id!.Value, ListTools()).ConfigureAwait(false);
                    break;
                case "tools/call":
                    await WriteResultAsync(
                            writer,
                            id!.Value,
                            await CallToolAsync(parameters, cancellationToken).ConfigureAwait(false))
                        .ConfigureAwait(false);
                    break;
                case "resources/list":
                    await WriteResultAsync(writer, id!.Value, new { resources = Array.Empty<object>() }).ConfigureAwait(false);
                    break;
                case "prompts/list":
                    await WriteResultAsync(writer, id!.Value, new { prompts = Array.Empty<object>() }).ConfigureAwait(false);
                    break;
                default:
                    await WriteErrorAsync(writer, id!.Value, -32601, $"Method not found: {method}").ConfigureAwait(false);
                    break;
            }
        }
        catch (JsonException)
        {
            await WriteErrorAsync(writer, null, -32700, "Parse error.").ConfigureAwait(false);
        }
        catch (Exception exc)
        {
            await WriteErrorAsync(writer, null, -32603, exc.Message).ConfigureAwait(false);
        }
    }

    private static object Initialize(JsonElement? parameters)
    {
        var requestedProtocolVersion = FallbackProtocolVersion;
        if (parameters is { ValueKind: JsonValueKind.Object } rawParameters
            && rawParameters.TryGetProperty("protocolVersion", out var version)
            && version.ValueKind == JsonValueKind.String)
        {
            requestedProtocolVersion = version.GetString() ?? FallbackProtocolVersion;
        }

        return new
        {
            protocolVersion = requestedProtocolVersion,
            capabilities = new
            {
                tools = new
                {
                    listChanged = true
                }
            },
            serverInfo = new
            {
                name = "supper-ida-mcp-center",
                title = "Supper IDA MCP Center",
                version = "0.1.2"
            }
        };
    }

    private object ListTools()
    {
        return new
        {
            tools = _toolCatalog.ListTools().Select(tool => new
            {
                name = tool.Name,
                title = tool.Title,
                description = tool.Description,
                inputSchema = tool.InputSchema
            })
        };
    }

    private async Task<object> CallToolAsync(
        JsonElement? parameters,
        CancellationToken cancellationToken)
    {
        if (parameters is not { ValueKind: JsonValueKind.Object } rawParameters
            || !rawParameters.TryGetProperty("name", out var rawName)
            || rawName.ValueKind != JsonValueKind.String)
        {
            return ToolText("Missing tools/call params.name.", isError: true);
        }

        var arguments = rawParameters.TryGetProperty("arguments", out var rawArguments)
            ? rawArguments.Clone()
            : JsonSerializer.SerializeToElement(new Dictionary<string, object?>(), JsonOptions);
        var result = await _toolHandler
            .CallAsync(rawName.GetString()!, arguments, cancellationToken)
            .ConfigureAwait(false);

        return ToolText(result.Text, result.IsError);
    }

    private static object ToolText(string text, bool isError)
    {
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text
                }
            },
            isError
        };
    }

    private static JsonElement? TryGetId(JsonElement root)
    {
        return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("id", out var id)
            ? id.Clone()
            : null;
    }

    private static async Task WriteResultAsync(StreamWriter writer, JsonElement id, object result)
    {
        await writer.WriteLineAsync(JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id,
            result
        }, JsonOptions)).ConfigureAwait(false);
    }

    private static async Task WriteErrorAsync(
        StreamWriter writer,
        JsonElement? id,
        int code,
        string message)
    {
        await writer.WriteLineAsync(JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id,
            error = new
            {
                code,
                message
            }
        }, JsonOptions)).ConfigureAwait(false);
    }
}
