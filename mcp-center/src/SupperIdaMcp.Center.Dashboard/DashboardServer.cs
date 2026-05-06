using System.Net;
using System.Text;
using System.Text.Json;
using SupperIdaMcp.Center.Core;
using SupperIdaMcp.Center.Ida;
using SupperIdaMcp.Center.TcpHub;

namespace SupperIdaMcp.Center.Dashboard;

public sealed class DashboardServer : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly DashboardOptions _options;
    private readonly TargetRegistry _targetRegistry;
    private readonly OperationLogStore _operationLog;
    private readonly ActiveOperationStore _activeOperations;
    private readonly IdaLocator _idaLocator;
    private readonly IdaLaunchService _idaLaunchService;
    private readonly HttpListener _listener = new();

    public DashboardServer(
        DashboardOptions options,
        TargetRegistry targetRegistry,
        OperationLogStore operationLog,
        ActiveOperationStore activeOperations,
        IdaLocator idaLocator,
        IdaLaunchService idaLaunchService)
    {
        _options = options;
        _targetRegistry = targetRegistry;
        _operationLog = operationLog;
        _activeOperations = activeOperations;
        _idaLocator = idaLocator;
        _idaLaunchService = idaLaunchService;
        _listener.Prefixes.Add($"http://{options.Host}:{options.Port}/");
    }

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
            switch (context.Request.HttpMethod, path)
            {
                case ("GET", "/"):
                    await WriteHtmlAsync(context, DashboardHtml.Page, cancellationToken).ConfigureAwait(false);
                    break;
                case ("GET", "/api/state"):
                    await WriteJsonAsync(context, State(), cancellationToken).ConfigureAwait(false);
                    break;
                case ("POST", "/api/close-target"):
                    await CloseTargetAsync(context, cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    context.Response.StatusCode = 404;
                    await WriteJsonAsync(context, new { error = "not found" }, cancellationToken).ConfigureAwait(false);
                    break;
            }
        }
        catch (Exception exc)
        {
            context.Response.StatusCode = 500;
            await WriteJsonAsync(context, new { error = exc.Message }, cancellationToken).ConfigureAwait(false);
        }
    }

    private object State()
    {
        return new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            targets = _targetRegistry.ListTargets(),
            activeOperations = _activeOperations.List(),
            operationLog = _operationLog.List(200),
            installations = _idaLocator.FindInstallations(),
            launchedProcesses = _idaLaunchService.ListLaunchedProcesses()
        };
    }

    private async Task CloseTargetAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        using var document = await JsonDocument.ParseAsync(context.Request.InputStream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var root = document.RootElement;
        var instanceId = root.TryGetProperty("instanceId", out var rawInstanceId)
                         && rawInstanceId.ValueKind == JsonValueKind.String
            ? rawInstanceId.GetString()
            : null;
        var force = root.TryGetProperty("force", out var rawForce)
                    && rawForce.ValueKind == JsonValueKind.True;

        if (string.IsNullOrWhiteSpace(instanceId))
        {
            context.Response.StatusCode = 400;
            await WriteJsonAsync(context, new { error = "instanceId is required" }, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!_targetRegistry.TryGetTarget(instanceId, out var target) || target is null)
        {
            context.Response.StatusCode = 404;
            await WriteJsonAsync(context, new { error = "target is not registered" }, cancellationToken).ConfigureAwait(false);
            return;
        }

        var result = _idaLaunchService.CloseProcess(target.ProcessId, force);
        await WriteJsonAsync(context, result, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteHtmlAsync(
        HttpListenerContext context,
        string html,
        CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(html);
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        context.Response.Close();
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
