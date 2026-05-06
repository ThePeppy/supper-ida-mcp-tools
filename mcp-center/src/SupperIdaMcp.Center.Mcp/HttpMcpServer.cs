using System.Net;
using System.Text;
using System.Text.Json;

namespace SupperIdaMcp.Center.Mcp;

public sealed class HttpMcpServer : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpMcpOptions _options;
    private readonly McpToolCatalog _toolCatalog;
    private readonly IdaMcpToolHandler _toolHandler;
    private readonly HttpListener _listener = new();

    public HttpMcpServer(HttpMcpOptions options, McpToolCatalog toolCatalog, IdaMcpToolHandler toolHandler)
    {
        _options = options;
        _toolCatalog = toolCatalog;
        _toolHandler = toolHandler;
        _listener.Prefixes.Add($"http://{options.Host}:{options.Port}/");
    }

    public string Url => $"http://{_options.Host}:{_options.Port}{_options.Path}";

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _listener.Start();
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var context = await _listener.GetContextAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
                _ = Task.Run(() => HandleAsync(context, cancellationToken), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_listener.IsListening)
        {
            _listener.Stop();
        }

        _listener.Close();
        return ValueTask.CompletedTask;
    }

    private async Task HandleAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            var path = context.Request.Url?.AbsolutePath ?? "/";
            if (context.Request.HttpMethod == "GET" && path == "/health")
            {
                await WriteJsonAsync(context, new { status = "ok", mcp = Url }, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod != "POST" || path != _options.Path)
            {
                context.Response.StatusCode = 404;
                await WriteJsonAsync(context, new { error = "not found" }, cancellationToken).ConfigureAwait(false);
                return;
            }

            using var document = await JsonDocument.ParseAsync(context.Request.InputStream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var response = await DispatchAsync(document.RootElement.Clone(), cancellationToken).ConfigureAwait(false);
            await WriteJsonAsync(context, response, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            context.Response.StatusCode = 400;
            await WriteJsonAsync(context, Error(null, -32700, "Parse error."), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exc)
        {
            context.Response.StatusCode = 500;
            await WriteJsonAsync(context, Error(null, -32603, exc.Message), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<object> DispatchAsync(JsonElement root, CancellationToken cancellationToken)
    {
        if (!root.TryGetProperty("method", out var methodElement)
            || methodElement.ValueKind != JsonValueKind.String)
        {
            return Error(TryGetId(root), -32600, "Invalid JSON-RPC request.");
        }

        var id = TryGetId(root);
        var method = methodElement.GetString()!;
        var parameters = root.TryGetProperty("params", out var rawParams)
            ? rawParams.Clone()
            : default(JsonElement?);

        return method switch
        {
            "initialize" => Result(id, Initialize(parameters)),
            "ping" => Result(id, new Dictionary<string, object?>()),
            "tools/list" => Result(id, ListTools()),
            "tools/call" => Result(id, await CallToolAsync(parameters, cancellationToken).ConfigureAwait(false)),
            "resources/list" => Result(id, new { resources = Array.Empty<object>() }),
            "prompts/list" => Result(id, new { prompts = Array.Empty<object>() }),
            _ => Error(id, -32601, $"Method not found: {method}")
        };
    }

    private static object Initialize(JsonElement? parameters)
    {
        var requestedProtocolVersion = "2025-06-18";
        if (parameters is { ValueKind: JsonValueKind.Object } rawParameters
            && rawParameters.TryGetProperty("protocolVersion", out var version)
            && version.ValueKind == JsonValueKind.String)
        {
            requestedProtocolVersion = version.GetString() ?? requestedProtocolVersion;
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
                version = "0.1.0"
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

    private async Task<object> CallToolAsync(JsonElement? parameters, CancellationToken cancellationToken)
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

    private static object Result(JsonElement? id, object result)
    {
        return new
        {
            jsonrpc = "2.0",
            id,
            result
        };
    }

    private static object Error(JsonElement? id, int code, string message)
    {
        return new
        {
            jsonrpc = "2.0",
            id,
            error = new
            {
                code,
                message
            }
        };
    }

    private static JsonElement? TryGetId(JsonElement root)
    {
        return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("id", out var id)
            ? id.Clone()
            : null;
    }

    private static async Task WriteJsonAsync(
        HttpListenerContext context,
        object value,
        CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        context.Response.Close();
    }
}
