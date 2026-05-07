using System.Diagnostics;
using System.Text.Json;
using SupperIdaMcp.Center.Core;
using SupperIdaMcp.Center.Ida;
using SupperIdaMcp.Center.TcpHub;

namespace SupperIdaMcp.Center.Mcp;

public sealed class IdaMcpToolHandler
{
    private const int MaxStoredDetailChars = 120_000;

    private static readonly JsonSerializerOptions WriteJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly TargetRegistry _targetRegistry;
    private readonly OperationLogStore _operationLog;
    private readonly ActiveOperationStore _activeOperations;
    private readonly IdaLocator _idaLocator;
    private readonly IdaLaunchService _idaLaunchService;

    public IdaMcpToolHandler(
        TargetRegistry targetRegistry,
        OperationLogStore operationLog,
        ActiveOperationStore activeOperations,
        IdaLocator idaLocator,
        IdaLaunchService idaLaunchService)
    {
        _targetRegistry = targetRegistry;
        _operationLog = operationLog;
        _activeOperations = activeOperations;
        _idaLocator = idaLocator;
        _idaLaunchService = idaLaunchService;
    }

    public async Task<McpToolCallResult> CallAsync(
        string toolName,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = toolName switch
            {
                "ida_list_targets" => Success(_targetRegistry.ListTargets()),
                "ida_get_target" => GetTarget(arguments),
                "ida_ping_target" => await InvokeTargetToolAsync(toolName, arguments, "target.ping", EmptyObject(), cancellationToken)
                    .ConfigureAwait(false),
                "ida_get_metadata" => await InvokeTargetToolAsync(toolName, arguments, "target.get_metadata", EmptyObject(), cancellationToken)
                    .ConfigureAwait(false),
                "ida_find_installations" => Success(_idaLocator.FindInstallations(GetOptionalString(arguments, "idaPath"))),
                "ida_launch_file" => await LaunchFileAsync(arguments, cancellationToken).ConfigureAwait(false),
                "ida_close_target" => CloseTarget(arguments),
                "ida_list_launched_processes" => Success(_idaLaunchService.ListLaunchedProcesses()),
                "ida_list_functions" => await InvokeTargetToolAsync(toolName, arguments, "analysis.list_functions", arguments, cancellationToken)
                    .ConfigureAwait(false),
                "ida_get_function" => await InvokeTargetToolAsync(toolName, arguments, "analysis.get_function", arguments, cancellationToken)
                    .ConfigureAwait(false),
                "ida_decompile" => await InvokeTargetToolAsync(toolName, arguments, "analysis.decompile", arguments, cancellationToken)
                    .ConfigureAwait(false),
                "ida_disassemble" => await InvokeTargetToolAsync(toolName, arguments, "analysis.disassemble", arguments, cancellationToken)
                    .ConfigureAwait(false),
                "ida_xrefs" => await InvokeTargetToolAsync(toolName, arguments, "analysis.xrefs", arguments, cancellationToken)
                    .ConfigureAwait(false),
                "ida_list_strings" => await InvokeTargetToolAsync(toolName, arguments, "analysis.list_strings", arguments, cancellationToken)
                    .ConfigureAwait(false),
                "ida_list_imports" => await InvokeTargetToolAsync(toolName, arguments, "analysis.list_imports", arguments, cancellationToken)
                    .ConfigureAwait(false),
                "ida_get_bytes" => await InvokeTargetToolAsync(toolName, arguments, "analysis.get_bytes", arguments, cancellationToken)
                    .ConfigureAwait(false),
                "ida_search_text" => await InvokeTargetToolAsync(toolName, arguments, "analysis.search_text", arguments, cancellationToken)
                    .ConfigureAwait(false),
                "ida_rename" => await InvokeTargetToolAsync(toolName, arguments, "analysis.rename", arguments, cancellationToken)
                    .ConfigureAwait(false),
                "ida_set_comment" => await InvokeTargetToolAsync(toolName, arguments, "analysis.set_comment", arguments, cancellationToken)
                    .ConfigureAwait(false),
                "ida_call_tool" => await CallTargetToolAsync(toolName, arguments, cancellationToken).ConfigureAwait(false),
                "ida_operation_log" => ListOperationLog(arguments),
                _ => Error($"Unknown tool: {toolName}")
            };

            stopwatch.Stop();
            if (ShouldLogCenterTool(toolName))
            {
                LogCenterToolCall(toolName, arguments, result, stopwatch.Elapsed);
            }

            return result;
        }
        catch (Exception exc)
        {
            stopwatch.Stop();
            if (ShouldLogCenterTool(toolName))
            {
                LogCenterException(toolName, arguments, exc, stopwatch.Elapsed);
            }

            return Error(exc.Message);
        }
    }

    private async Task<McpToolCallResult> LaunchFileAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var inputPath = GetRequiredString(arguments, "inputPath");
        var idaPath = GetOptionalString(arguments, "idaPath");
        var waitSeconds = GetOptionalInt(arguments, "waitSeconds", 20, 0, 300);
        var extraArguments = GetOptionalStringArray(arguments, "arguments");
        var launched = _idaLaunchService.Launch(new IdaLaunchRequest(inputPath, idaPath, extraArguments));
        TargetInfo? target = null;

        if (waitSeconds > 0)
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(waitSeconds);
            while (DateTimeOffset.UtcNow < deadline)
            {
                target = _targetRegistry.ListTargets()
                    .FirstOrDefault(candidate => SamePath(candidate.InputPath, launched.InputPath));
                if (target is not null)
                {
                    break;
                }

                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }
        }

        return Success(new
        {
            launched,
            registeredTarget = target
        });
    }

    private McpToolCallResult CloseTarget(JsonElement arguments)
    {
        var instanceId = GetRequiredString(arguments, "instanceId");
        var force = GetOptionalBool(arguments, "force", false);
        if (!_targetRegistry.TryGetTarget(instanceId, out var target) || target is null)
        {
            return Error($"IDA target is not registered: {instanceId}");
        }

        var result = _idaLaunchService.CloseProcess(target.ProcessId, force);
        return Success(result);
    }

    private McpToolCallResult GetTarget(JsonElement arguments)
    {
        var instanceId = GetRequiredString(arguments, "instanceId");
        return _targetRegistry.TryGetTarget(instanceId, out var target)
            ? Success(target)
            : Error($"IDA target is not registered: {instanceId}");
    }

    private async Task<McpToolCallResult> CallTargetToolAsync(
        string mcpToolName,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var toolName = GetRequiredString(arguments, "tool");
        var toolArguments = TryGetProperty(arguments, "arguments", out var rawArguments)
            ? rawArguments.Clone()
            : EmptyObject();

        return await InvokeTargetToolAsync(mcpToolName, arguments, toolName, toolArguments, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<McpToolCallResult> InvokeTargetToolAsync(
        string mcpToolName,
        JsonElement arguments,
        string pluginToolName,
        JsonElement pluginArguments,
        CancellationToken cancellationToken)
    {
        var instanceId = GetRequiredString(arguments, "instanceId");
        if (!_targetRegistry.TryGetTarget(instanceId, out var target) || target is null)
        {
            _operationLog.Add(LogEntry(
                instanceId,
                instanceId,
                pluginToolName,
                mcpToolName,
                success: false,
                elapsed: TimeSpan.Zero,
                error: $"IDA target is not registered: {instanceId}",
                requestJson: TargetRequestJson(mcpToolName, instanceId, pluginToolName, pluginArguments),
                responseJson: null));
            return Error($"IDA target is not registered: {instanceId}");
        }

        if (!_targetRegistry.TryGetConnection(instanceId, out var connection) || connection is null)
        {
            _operationLog.Add(LogEntry(
                target.InstanceId,
                target.Alias,
                pluginToolName,
                mcpToolName,
                success: false,
                elapsed: TimeSpan.Zero,
                error: $"IDA target has no active connection: {instanceId}",
                requestJson: TargetRequestJson(mcpToolName, instanceId, pluginToolName, pluginArguments),
                responseJson: null));
            return Error($"IDA target has no active connection: {instanceId}");
        }

        var stopwatch = Stopwatch.StartNew();
        var activeOperation = _activeOperations.Begin(target.InstanceId, target.Alias, pluginToolName);
        var requestJson = TargetRequestJson(mcpToolName, target.InstanceId, pluginToolName, pluginArguments);
        try
        {
            var response = await connection
                .InvokeToolAsync(pluginToolName, pluginArguments, TimeSpan.FromSeconds(60), cancellationToken)
                .ConfigureAwait(false);
            stopwatch.Stop();
            var success = ReadOk(response);
            var error = success ? null : ReadError(response);
            if (success && string.Equals(pluginToolName, "target.get_metadata", StringComparison.Ordinal))
            {
                RefreshTargetMetadata(response);
            }

            _operationLog.Add(LogEntry(
                target.InstanceId,
                target.Alias,
                pluginToolName,
                mcpToolName,
                success,
                stopwatch.Elapsed,
                error,
                requestJson,
                DetailJson(response)));

            return success ? Success(response) : Error(error ?? "IDA tool call failed.");
        }
        catch (Exception exc)
        {
            stopwatch.Stop();
            _operationLog.Add(LogEntry(
                target.InstanceId,
                target.Alias,
                pluginToolName,
                mcpToolName,
                false,
                stopwatch.Elapsed,
                exc.Message,
                requestJson,
                null));
            return Error(exc.Message);
        }
        finally
        {
            _activeOperations.End(activeOperation.OperationId);
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

    private void LogCenterToolCall(
        string toolName,
        JsonElement arguments,
        McpToolCallResult result,
        TimeSpan elapsed)
    {
        var (targetInstanceId, targetAlias) = ResolveLogTarget(arguments);
        _operationLog.Add(LogEntry(
            targetInstanceId,
            targetAlias,
            toolName,
            toolName,
            !result.IsError,
            elapsed,
            result.IsError ? result.Text : null,
            CenterRequestJson(toolName, arguments),
            DetailText(result.Text)));
    }

    private void LogCenterException(
        string toolName,
        JsonElement arguments,
        Exception exc,
        TimeSpan elapsed)
    {
        var (targetInstanceId, targetAlias) = ResolveLogTarget(arguments);
        _operationLog.Add(LogEntry(
            targetInstanceId,
            targetAlias,
            toolName,
            toolName,
            success: false,
            elapsed,
            exc.Message,
            CenterRequestJson(toolName, arguments),
            null));
    }

    private (string TargetInstanceId, string TargetAlias) ResolveLogTarget(JsonElement arguments)
    {
        var instanceId = GetOptionalString(arguments, "instanceId");
        if (!string.IsNullOrWhiteSpace(instanceId)
            && _targetRegistry.TryGetTarget(instanceId, out var target)
            && target is not null)
        {
            return (target.InstanceId, target.Alias);
        }

        return (instanceId ?? "center", instanceId ?? "Center");
    }

    private static OperationLogEntry LogEntry(
        string targetInstanceId,
        string targetAlias,
        string toolName,
        string? mcpToolName,
        bool success,
        TimeSpan elapsed,
        string? error,
        string? requestJson,
        string? responseJson)
    {
        return new OperationLogEntry(
            NewLogEntryId(),
            DateTimeOffset.UtcNow,
            targetInstanceId,
            targetAlias,
            toolName,
            mcpToolName,
            success,
            elapsed,
            LimitDetail(error),
            LimitDetail(requestJson),
            LimitDetail(responseJson));
    }

    private static string NewLogEntryId()
    {
        return $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():x}-{Guid.NewGuid():N}"[..25];
    }

    private static bool ShouldLogCenterTool(string toolName)
    {
        return toolName is not "ida_operation_log" && !IsRoutedTargetTool(toolName);
    }

    private static bool IsRoutedTargetTool(string toolName)
    {
        return toolName is "ida_ping_target"
            or "ida_get_metadata"
            or "ida_list_functions"
            or "ida_get_function"
            or "ida_decompile"
            or "ida_disassemble"
            or "ida_xrefs"
            or "ida_list_strings"
            or "ida_list_imports"
            or "ida_get_bytes"
            or "ida_search_text"
            or "ida_rename"
            or "ida_set_comment"
            or "ida_call_tool";
    }

    private static string CenterRequestJson(string mcpToolName, JsonElement arguments)
    {
        return DetailJson(new
        {
            mcpTool = mcpToolName,
            arguments
        });
    }

    private static string TargetRequestJson(
        string mcpToolName,
        string instanceId,
        string pluginToolName,
        JsonElement pluginArguments)
    {
        return DetailJson(new
        {
            mcpTool = mcpToolName,
            instanceId,
            pluginTool = pluginToolName,
            pluginArguments
        });
    }

    private static string DetailJson(object value)
    {
        return LimitDetail(JsonSerializer.Serialize(value, WriteJsonOptions))!;
    }

    private static string DetailJson(JsonElement value)
    {
        return LimitDetail(JsonSerializer.Serialize(value, WriteJsonOptions))!;
    }

    private static string DetailText(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            return DetailJson(document.RootElement);
        }
        catch (JsonException)
        {
            return LimitDetail(text)!;
        }
    }

    private static string? LimitDetail(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= MaxStoredDetailChars)
        {
            return value;
        }

        return value[..MaxStoredDetailChars] + "\n... truncated by Supper IDA MCP Center ...";
    }

    private static string GetRequiredString(JsonElement arguments, string propertyName)
    {
        if (!TryGetProperty(arguments, propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new ArgumentException($"Missing required string argument: {propertyName}");
        }

        return property.GetString()!;
    }

    private static string? GetOptionalString(JsonElement arguments, string propertyName)
    {
        return TryGetProperty(arguments, propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static int GetOptionalInt(JsonElement arguments, string propertyName, int defaultValue, int minimum, int maximum)
    {
        if (!TryGetProperty(arguments, propertyName, out var property) || property.ValueKind != JsonValueKind.Number)
        {
            return defaultValue;
        }

        return Math.Clamp(property.GetInt32(), minimum, maximum);
    }

    private static bool GetOptionalBool(JsonElement arguments, string propertyName, bool defaultValue)
    {
        if (!TryGetProperty(arguments, propertyName, out var property))
        {
            return defaultValue;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => defaultValue
        };
    }

    private static IReadOnlyList<string> GetOptionalStringArray(JsonElement arguments, string propertyName)
    {
        if (!TryGetProperty(arguments, propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!)
            .ToArray();
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

    private void RefreshTargetMetadata(JsonElement response)
    {
        if (!response.TryGetProperty("result", out var rawResult)
            || rawResult.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var metadata = rawResult.Deserialize<TargetMetadata>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (metadata is null)
        {
            return;
        }

        _targetRegistry.UpsertMetadata(metadata, connection: null, DateTimeOffset.UtcNow);
    }

    private static string? ReadError(JsonElement response)
    {
        return response.ValueKind == JsonValueKind.Object
            && response.TryGetProperty("error", out var error)
            && error.ValueKind == JsonValueKind.String
            ? error.GetString()
            : null;
    }

    private static bool SamePath(string? left, string right)
    {
        if (string.IsNullOrWhiteSpace(left))
        {
            return false;
        }

        try
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
        catch
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
