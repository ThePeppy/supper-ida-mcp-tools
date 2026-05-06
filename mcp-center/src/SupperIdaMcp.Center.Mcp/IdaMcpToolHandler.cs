using System.Diagnostics;
using System.Text.Json;
using SupperIdaMcp.Center.Core;
using SupperIdaMcp.Center.TcpHub;

namespace SupperIdaMcp.Center.Mcp;

public sealed class IdaMcpToolHandler
{
    private static readonly JsonSerializerOptions WriteJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly TargetRegistry _targetRegistry;
    private readonly OperationLogStore _operationLog;

    public IdaMcpToolHandler(TargetRegistry targetRegistry, OperationLogStore operationLog)
    {
        _targetRegistry = targetRegistry;
        _operationLog = operationLog;
    }

    public async Task<McpToolCallResult> CallAsync(
        string toolName,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            return toolName switch
            {
                "ida_list_targets" => Success(_targetRegistry.ListTargets()),
                "ida_get_target" => GetTarget(arguments),
                "ida_ping_target" => await InvokeTargetToolAsync(arguments, "target.ping", EmptyObject(), cancellationToken)
                    .ConfigureAwait(false),
                "ida_get_metadata" => await InvokeTargetToolAsync(arguments, "target.get_metadata", EmptyObject(), cancellationToken)
                    .ConfigureAwait(false),
                "ida_list_functions" => await InvokeTargetToolAsync(arguments, "analysis.list_functions", arguments, cancellationToken)
                    .ConfigureAwait(false),
                "ida_get_function" => await InvokeTargetToolAsync(arguments, "analysis.get_function", arguments, cancellationToken)
                    .ConfigureAwait(false),
                "ida_decompile" => await InvokeTargetToolAsync(arguments, "analysis.decompile", arguments, cancellationToken)
                    .ConfigureAwait(false),
                "ida_disassemble" => await InvokeTargetToolAsync(arguments, "analysis.disassemble", arguments, cancellationToken)
                    .ConfigureAwait(false),
                "ida_xrefs" => await InvokeTargetToolAsync(arguments, "analysis.xrefs", arguments, cancellationToken)
                    .ConfigureAwait(false),
                "ida_list_strings" => await InvokeTargetToolAsync(arguments, "analysis.list_strings", arguments, cancellationToken)
                    .ConfigureAwait(false),
                "ida_list_imports" => await InvokeTargetToolAsync(arguments, "analysis.list_imports", arguments, cancellationToken)
                    .ConfigureAwait(false),
                "ida_get_bytes" => await InvokeTargetToolAsync(arguments, "analysis.get_bytes", arguments, cancellationToken)
                    .ConfigureAwait(false),
                "ida_search_text" => await InvokeTargetToolAsync(arguments, "analysis.search_text", arguments, cancellationToken)
                    .ConfigureAwait(false),
                "ida_rename" => await InvokeTargetToolAsync(arguments, "analysis.rename", arguments, cancellationToken)
                    .ConfigureAwait(false),
                "ida_set_comment" => await InvokeTargetToolAsync(arguments, "analysis.set_comment", arguments, cancellationToken)
                    .ConfigureAwait(false),
                "ida_call_tool" => await CallTargetToolAsync(arguments, cancellationToken).ConfigureAwait(false),
                "ida_operation_log" => ListOperationLog(arguments),
                _ => Error($"Unknown tool: {toolName}")
            };
        }
        catch (Exception exc)
        {
            return Error(exc.Message);
        }
    }

    private McpToolCallResult GetTarget(JsonElement arguments)
    {
        var instanceId = GetRequiredString(arguments, "instanceId");
        return _targetRegistry.TryGetTarget(instanceId, out var target)
            ? Success(target)
            : Error($"IDA target is not registered: {instanceId}");
    }

    private async Task<McpToolCallResult> CallTargetToolAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var toolName = GetRequiredString(arguments, "tool");
        var toolArguments = TryGetProperty(arguments, "arguments", out var rawArguments)
            ? rawArguments.Clone()
            : EmptyObject();

        return await InvokeTargetToolAsync(arguments, toolName, toolArguments, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<McpToolCallResult> InvokeTargetToolAsync(
        JsonElement arguments,
        string pluginToolName,
        JsonElement pluginArguments,
        CancellationToken cancellationToken)
    {
        var instanceId = GetRequiredString(arguments, "instanceId");
        if (!_targetRegistry.TryGetTarget(instanceId, out var target) || target is null)
        {
            return Error($"IDA target is not registered: {instanceId}");
        }

        if (!_targetRegistry.TryGetConnection(instanceId, out var connection) || connection is null)
        {
            return Error($"IDA target has no active connection: {instanceId}");
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await connection
                .InvokeToolAsync(pluginToolName, pluginArguments, TimeSpan.FromSeconds(60), cancellationToken)
                .ConfigureAwait(false);
            stopwatch.Stop();
            var success = ReadOk(response);
            var error = success ? null : ReadError(response);
            _operationLog.Add(new OperationLogEntry(
                DateTimeOffset.UtcNow,
                target.InstanceId,
                target.Alias,
                pluginToolName,
                success,
                stopwatch.Elapsed,
                error));

            return success ? Success(response) : Error(error ?? "IDA tool call failed.");
        }
        catch (Exception exc)
        {
            stopwatch.Stop();
            _operationLog.Add(new OperationLogEntry(
                DateTimeOffset.UtcNow,
                target.InstanceId,
                target.Alias,
                pluginToolName,
                false,
                stopwatch.Elapsed,
                exc.Message));
            return Error(exc.Message);
        }
    }

    private McpToolCallResult ListOperationLog(JsonElement arguments)
    {
        var limit = 100;
        if (TryGetProperty(arguments, "limit", out var rawLimit) && rawLimit.ValueKind == JsonValueKind.Number)
        {
            limit = rawLimit.GetInt32();
        }

        return Success(_operationLog.List(limit));
    }

    private static McpToolCallResult Success(object? value)
    {
        return new McpToolCallResult(JsonSerializer.Serialize(value, WriteJsonOptions));
    }

    private static McpToolCallResult Error(string message)
    {
        return new McpToolCallResult(message, IsError: true);
    }

    private static string GetRequiredString(JsonElement arguments, string propertyName)
    {
        if (!TryGetProperty(arguments, propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new ArgumentException($"Missing required string argument: {propertyName}");
        }

        return property.GetString()!;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement property)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out property))
        {
            return true;
        }

        property = default;
        return false;
    }

    private static JsonElement EmptyObject()
    {
        return JsonSerializer.SerializeToElement(new Dictionary<string, object?>());
    }

    private static bool ReadOk(JsonElement response)
    {
        return response.ValueKind == JsonValueKind.Object
            && response.TryGetProperty("ok", out var ok)
            && ok.ValueKind == JsonValueKind.True;
    }

    private static string? ReadError(JsonElement response)
    {
        return response.ValueKind == JsonValueKind.Object
            && response.TryGetProperty("error", out var error)
            && error.ValueKind == JsonValueKind.String
            ? error.GetString()
            : null;
    }
}
