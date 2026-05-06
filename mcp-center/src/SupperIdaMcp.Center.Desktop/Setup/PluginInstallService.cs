using System.Text.RegularExpressions;

namespace SupperIdaMcp.Center.Desktop.Setup;

internal sealed partial class PluginInstallService
{
    private const string LoaderName = "supper_ida_mcp_plugin.py";
    private readonly RepositoryPaths _paths;

    public PluginInstallService(RepositoryPaths paths)
    {
        _paths = paths;
    }

    public PluginInstallStatus GetStatus()
    {
        var pluginsDirectory = DefaultPluginsDirectory();
        var loaderPath = Path.Combine(pluginsDirectory, LoaderName);
        if (!File.Exists(loaderPath))
        {
            return new PluginInstallStatus(
                pluginsDirectory,
                loaderPath,
                _paths.IdaPluginSourceRoot,
                IsInstalled: false,
                IsOurs: false,
                IsCompatible: false,
                InstalledVersion: null,
                ProductInfo.PluginVersion,
                "Plugin loader is not installed.");
        }

        var content = File.ReadAllText(loaderPath);
        var isOurs = content.Contains($"SUPPER_IDA_MCP_PLUGIN_ID = {ProductInfo.PluginId.AsPythonString()}", StringComparison.Ordinal)
            || content.Contains("from supper_ida_plugin.entry import PLUGIN_ENTRY", StringComparison.Ordinal);
        var version = VersionRegex().Match(content) is { Success: true } match ? match.Groups[1].Value : null;
        var isCompatible = isOurs && string.Equals(version, ProductInfo.PluginVersion, StringComparison.Ordinal);
        var message = !isOurs
            ? "A loader with the expected filename exists, but it is not a Supper IDA MCP loader."
            : isCompatible
                ? "Installed plugin loader matches this center version."
                : $"Installed loader version is {version ?? "unknown"}, expected {ProductInfo.PluginVersion}.";

        return new PluginInstallStatus(
            pluginsDirectory,
            loaderPath,
            _paths.IdaPluginSourceRoot,
            IsInstalled: true,
            isOurs,
            isCompatible,
            version,
            ProductInfo.PluginVersion,
            message);
    }

    public PluginInstallStatus InstallOrRepair()
    {
        if (_paths.IdaPluginSourceRoot is null || !Directory.Exists(_paths.IdaPluginSourceRoot))
        {
            throw new InvalidOperationException("Unable to locate ida-plugin/src. Run from the repository or install a packaged build with bundled plugin sources.");
        }

        var pluginsDirectory = DefaultPluginsDirectory();
        Directory.CreateDirectory(pluginsDirectory);
        var loaderPath = Path.Combine(pluginsDirectory, LoaderName);
        File.WriteAllText(loaderPath, BuildLoader(_paths.IdaPluginSourceRoot));
        return GetStatus();
    }

    public PluginInstallStatus Uninstall()
    {
        var loaderPath = Path.Combine(DefaultPluginsDirectory(), LoaderName);
        if (File.Exists(loaderPath))
        {
            var content = File.ReadAllText(loaderPath);
            if (!content.Contains("supper_ida_plugin", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Refusing to remove a loader that does not look like a Supper IDA MCP loader.");
            }

            File.Delete(loaderPath);
        }

        return GetStatus();
    }

    private static string DefaultPluginsDirectory()
    {
        var idaUser = Environment.GetEnvironmentVariable("IDAUSR");
        if (!string.IsNullOrWhiteSpace(idaUser))
        {
            return Path.Combine(ExpandHome(idaUser), "plugins");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(home, "Library", "Application Support", "Hex-Rays", "IDA Pro", "plugins");
        }

        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Hex-Rays", "IDA Pro", "plugins");
        }

        return Path.Combine(home, ".idapro", "plugins");
    }

    private static string ExpandHome(string path)
    {
        if (path == "~")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]);
        }

        return path;
    }

    private static string BuildLoader(string sourceRoot)
    {
        return string.Join(Environment.NewLine, [
            "\"\"\"IDA loader for Supper IDA MCP Tools.\"\"\"",
            string.Empty,
            "from __future__ import annotations",
            string.Empty,
            "import sys",
            string.Empty,
            $"SUPPER_IDA_MCP_PLUGIN_ID = {ProductInfo.PluginId.AsPythonString()}",
            $"SUPPER_IDA_MCP_PLUGIN_VERSION = {ProductInfo.PluginVersion.AsPythonString()}",
            $"SOURCE_ROOT = {sourceRoot.AsPythonString()}",
            string.Empty,
            "if SOURCE_ROOT not in sys.path:",
            "    sys.path.insert(0, SOURCE_ROOT)",
            string.Empty,
            "from supper_ida_plugin.entry import PLUGIN_ENTRY  # noqa: E402,F401",
            string.Empty
        ]);
    }

    [GeneratedRegex("SUPPER_IDA_MCP_PLUGIN_VERSION\\s*=\\s*[\"']([^\"']+)[\"']")]
    private static partial Regex VersionRegex();
}

internal static class PythonStringExtensions
{
    public static string AsPythonString(this string value)
    {
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
