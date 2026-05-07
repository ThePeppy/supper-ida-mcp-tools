namespace SupperIdaMcp.Center.Desktop.Setup;

internal sealed record BridgeLaunchCommand(string Command, IReadOnlyList<string> Args, string DisplayPath)
{
    public string ToManualSnippet(string endpoint)
    {
        return string.Join(Environment.NewLine, [
            "For MCP clients that only support stdio, configure:",
            string.Empty,
            $"command: {Command}",
            "args:",
            .. Args.Select(arg => $"  - {arg}"),
            $"  - --endpoint",
            $"  - {endpoint}"
        ]);
    }
}

internal static class BridgeLaunchCommandResolver
{
    public static BridgeLaunchCommand? Resolve(RepositoryPaths paths)
    {
        var packagedBridge = PackagedBridgeExecutablePath();
        if (packagedBridge is not null)
        {
            return new BridgeLaunchCommand(
                packagedBridge,
                [],
                packagedBridge);
        }

        if (paths.BridgeProjectPath is not null && File.Exists(paths.BridgeProjectPath))
        {
            return new BridgeLaunchCommand(
                "dotnet",
                ["run", "--project", paths.BridgeProjectPath, "--"],
                paths.BridgeProjectPath);
        }

        return null;
    }

    private static string? PackagedBridgeExecutablePath()
    {
        var fileName = OperatingSystem.IsWindows()
            ? "SupperIdaMcp.Center.Bridge.exe"
            : "SupperIdaMcp.Center.Bridge";
        var path = Path.Combine(AppContext.BaseDirectory, "Bridge", fileName);
        return File.Exists(path) ? path : null;
    }
}
