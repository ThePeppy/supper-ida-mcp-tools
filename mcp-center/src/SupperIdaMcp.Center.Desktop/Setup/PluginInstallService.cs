using System.Text.RegularExpressions;

namespace SupperIdaMcp.Center.Desktop.Setup;

internal sealed partial class PluginInstallService
{
    private const string LoaderName = "supper_ida_mcp_plugin.py";
    private const string PackageName = "supper_ida_plugin";
    private const string LegacyBackupDirectory = ".supper_ida_mcp_legacy_backup";
    private readonly RepositoryPaths _paths;

    public PluginInstallService(RepositoryPaths paths)
    {
        _paths = paths;
    }

    public PluginInstallStatus GetStatus()
    {
        var pluginsDirectory = DefaultPluginsDirectory();
        var loaderPath = Path.Combine(pluginsDirectory, LoaderName);
        var packagePath = Path.Combine(pluginsDirectory, PackageName);
        var warnings = DetectMisplacedLoaders(loaderPath);
        if (!File.Exists(loaderPath))
        {
            return new PluginInstallStatus(
                pluginsDirectory,
                loaderPath,
                packagePath,
                _paths.IdaPluginSourceRoot,
                IsInstalled: false,
                IsOurs: false,
                IsCompatible: false,
                InstalledVersion: null,
                ProductInfo.PluginVersion,
                "Plugin loader is not installed in the IDA user plugins directory.",
                warnings);
        }

        var content = File.ReadAllText(loaderPath);
        var isOurs = content.Contains($"SUPPER_IDA_MCP_PLUGIN_ID = {ProductInfo.PluginId.AsPythonString()}", StringComparison.Ordinal)
            || content.Contains("from supper_ida_plugin.entry import PLUGIN_ENTRY", StringComparison.Ordinal);
        var version = VersionRegex().Match(content) is { Success: true } match ? match.Groups[1].Value : null;
        var hasPackage = Directory.Exists(packagePath);
        var isCompatible = isOurs
            && hasPackage
            && string.Equals(version, ProductInfo.PluginVersion, StringComparison.Ordinal);
        var message = !isOurs
            ? "A loader with the expected filename exists, but it is not a Supper IDA MCP loader."
            : !hasPackage
                ? "Loader exists, but the plugin package folder is missing. Repair installation."
                : isCompatible
                ? "Installed plugin loader matches this center version."
                : $"Installed loader version is {version ?? "unknown"}, expected {ProductInfo.PluginVersion}.";

        return new PluginInstallStatus(
            pluginsDirectory,
            loaderPath,
            packagePath,
            _paths.IdaPluginSourceRoot,
            IsInstalled: true,
            isOurs,
            isCompatible,
            version,
            ProductInfo.PluginVersion,
            message,
            warnings);
    }

    public PluginInstallStatus InstallOrRepair()
    {
        if (_paths.IdaPluginSourceRoot is null || !Directory.Exists(_paths.IdaPluginSourceRoot))
        {
            throw new InvalidOperationException("Unable to locate ida-plugin/src. Run from the repository or install a packaged build with bundled plugin sources.");
        }

        var packageSource = Path.Combine(_paths.IdaPluginSourceRoot, PackageName);
        if (!Directory.Exists(packageSource))
        {
            throw new InvalidOperationException($"Unable to locate plugin package source: {packageSource}");
        }

        var pluginsDirectory = DefaultPluginsDirectory();
        Directory.CreateDirectory(pluginsDirectory);
        ArchiveLegacyPlugins(pluginsDirectory);
        var loaderPath = Path.Combine(pluginsDirectory, LoaderName);
        var packagePath = Path.Combine(pluginsDirectory, PackageName);
        if (Directory.Exists(packagePath))
        {
            Directory.Delete(packagePath, recursive: true);
        }

        CopyDirectory(packageSource, packagePath);
        File.WriteAllText(loaderPath, BuildLoader());
        return GetStatus();
    }

    public PluginInstallStatus ArchiveLegacyPlugins()
    {
        ArchiveLegacyPlugins(DefaultPluginsDirectory());
        return GetStatus();
    }

    public PluginInstallStatus Uninstall()
    {
        var loaderPath = Path.Combine(DefaultPluginsDirectory(), LoaderName);
        var packagePath = Path.Combine(DefaultPluginsDirectory(), PackageName);
        if (File.Exists(loaderPath))
        {
            var content = File.ReadAllText(loaderPath);
            if (!content.Contains("supper_ida_plugin", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Refusing to remove a loader that does not look like a Supper IDA MCP loader.");
            }

            File.Delete(loaderPath);
        }

        if (Directory.Exists(packagePath))
        {
            Directory.Delete(packagePath, recursive: true);
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
            return Path.Combine(home, ".idapro", "plugins");
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

    private static string BuildLoader()
    {
        return string.Join(Environment.NewLine, [
            "\"\"\"IDA loader for Supper IDA MCP Tools.\"\"\"",
            string.Empty,
            "from __future__ import annotations",
            string.Empty,
            "import os",
            "import sys",
            string.Empty,
            $"SUPPER_IDA_MCP_PLUGIN_ID = {ProductInfo.PluginId.AsPythonString()}",
            $"SUPPER_IDA_MCP_PLUGIN_VERSION = {ProductInfo.PluginVersion.AsPythonString()}",
            "PLUGIN_DIR = os.path.dirname(os.path.abspath(__file__))",
            string.Empty,
            "if PLUGIN_DIR not in sys.path:",
            "    sys.path.insert(0, PLUGIN_DIR)",
            string.Empty,
            "from supper_ida_plugin.entry import PLUGIN_ENTRY  # noqa: E402,F401",
            string.Empty
        ]);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(directory.Replace(source, destination, StringComparison.Ordinal));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var destinationFile = file.Replace(source, destination, StringComparison.Ordinal);
            File.Copy(file, destinationFile, overwrite: true);
        }
    }

    private static IReadOnlyList<string> DetectMisplacedLoaders(string primaryLoaderPath)
    {
        var warnings = new List<string>();
        var activeDirectory = Path.GetDirectoryName(primaryLoaderPath);
        if (!string.IsNullOrWhiteSpace(activeDirectory))
        {
            foreach (var legacyPath in LegacyPluginPaths(activeDirectory))
            {
                if (FileSystemEntryExists(legacyPath))
                {
                    warnings.Add($"Legacy IDA MCP plugin found and may conflict after IDA restart: {legacyPath}");
                }
            }
        }

        foreach (var candidate in CandidatePluginDirectories().Select(path => Path.Combine(path, LoaderName)))
        {
            if (string.Equals(candidate, primaryLoaderPath, StringComparison.Ordinal))
            {
                continue;
            }

            if (FileSystemEntryExists(candidate))
            {
                warnings.Add($"Loader also exists outside IDA's active user plugin directory: {candidate}");
            }
        }

        return warnings;
    }

    private static IEnumerable<string> LegacyPluginPaths(string pluginDirectory)
    {
        yield return Path.Combine(pluginDirectory, "ida_mcp.py");
        yield return Path.Combine(pluginDirectory, "ida_mcp");
        yield return Path.Combine(pluginDirectory, "mcp-plugin.py");
    }

    private static IReadOnlyList<string> ArchiveLegacyPlugins(string pluginDirectory)
    {
        var paths = LegacyPluginPaths(pluginDirectory)
            .Where(FileSystemEntryExists)
            .ToList();
        if (paths.Count == 0)
        {
            return [];
        }

        var backupDirectory = Path.Combine(
            pluginDirectory,
            LegacyBackupDirectory,
            DateTimeOffset.Now.ToLocalTime().ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(backupDirectory);
        var archived = new List<string>();
        foreach (var path in paths)
        {
            var destination = UniqueDestination(Path.Combine(backupDirectory, Path.GetFileName(path)));
            if (Directory.Exists(path) && !IsSymbolicLink(path))
            {
                Directory.Move(path, destination);
            }
            else
            {
                File.Move(path, destination);
            }

            archived.Add(destination);
        }

        return archived;
    }

    private static string UniqueDestination(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        var index = 1;
        while (true)
        {
            var candidate = Path.Combine(directory, $"{fileName}-{index}{extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }

            index++;
        }
    }

    private static bool FileSystemEntryExists(string path)
    {
        if (File.Exists(path) || Directory.Exists(path))
        {
            return true;
        }

        try
        {
            _ = File.GetAttributes(path);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static bool IsSymbolicLink(string path)
    {
        try
        {
            return File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static IEnumerable<string> CandidatePluginDirectories()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsMacOS())
        {
            yield return Path.Combine(home, "Library", "Application Support", "Hex-Rays", "IDA Pro", "plugins");
        }

        yield return Path.Combine(home, ".idapro", "plugins");
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
