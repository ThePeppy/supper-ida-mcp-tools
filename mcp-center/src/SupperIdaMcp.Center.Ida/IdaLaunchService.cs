using System.Diagnostics;

namespace SupperIdaMcp.Center.Ida;

public sealed class IdaLaunchService
{
    private readonly IdaLocator _locator;
    private readonly IdaProcessRegistry _processRegistry;

    public IdaLaunchService(IdaLocator locator, IdaProcessRegistry processRegistry)
    {
        _locator = locator;
        _processRegistry = processRegistry;
    }

    public LaunchedIdaProcess Launch(IdaLaunchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.InputPath))
        {
            throw new ArgumentException("inputPath is required.");
        }

        var inputPath = Path.GetFullPath(request.InputPath);
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException("Input file does not exist.", inputPath);
        }

        var executablePath = ResolveExecutablePath(request.IdaPath);
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory
        };

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add(inputPath);

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("IDA process did not start.");
        var launched = new LaunchedIdaProcess(
            process.Id,
            executablePath,
            inputPath,
            DateTimeOffset.UtcNow,
            process.HasExited);
        _processRegistry.Upsert(launched);
        return launched;
    }

    public IReadOnlyCollection<LaunchedIdaProcess> ListLaunchedProcesses()
    {
        return _processRegistry.List();
    }

    public object CloseProcess(int processId, bool force)
    {
        var process = Process.GetProcessById(processId);
        var closeMainWindow = false;
        try
        {
            closeMainWindow = process.CloseMainWindow();
        }
        catch
        {
            closeMainWindow = false;
        }

        if (!process.WaitForExit(2_000) && force)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5_000);
        }

        return new
        {
            processId,
            closeMainWindow,
            process.HasExited,
            force
        };
    }

    private string ResolveExecutablePath(string? requestedPath)
    {
        var install = _locator.FindInstallations(requestedPath)
            .FirstOrDefault(candidate => candidate.Exists);
        if (install is null)
        {
            throw new FileNotFoundException("IDA executable was not found. Set SUPPER_IDA_PATH or pass idaPath.");
        }

        return install.Path;
    }
}
