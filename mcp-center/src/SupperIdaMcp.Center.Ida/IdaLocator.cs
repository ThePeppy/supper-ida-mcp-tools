using System.Runtime.InteropServices;

namespace SupperIdaMcp.Center.Ida;

public sealed class IdaLocator
{
    private readonly string? _configuredPath;

    public IdaLocator(string? configuredPath = null)
    {
        _configuredPath = configuredPath;
    }

    public string? FindDefaultInstallPath()
    {
        return FindInstallations().FirstOrDefault(install => install.Exists)?.Path;
    }

    public IReadOnlyList<IdaInstall> FindInstallations(string? explicitPath = null)
    {
        var candidates = new List<IdaInstall>();
        AddConfigured(candidates, explicitPath, "argument");
        AddConfigured(candidates, _configuredPath, "configuration");
        AddConfigured(candidates, Environment.GetEnvironmentVariable("SUPPER_IDA_PATH"), "SUPPER_IDA_PATH");
        AddConfigured(candidates, Environment.GetEnvironmentVariable("IDA_PATH"), "IDA_PATH");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            AddMacCandidates(candidates);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            AddWindowsCandidates(candidates);
        }
        else
        {
            AddUnixCandidates(candidates);
        }

        return candidates
            .GroupBy(candidate => NormalizeKey(candidate.Path), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(candidate => candidate.Exists)
            .ThenBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddConfigured(List<IdaInstall> candidates, string? path, string source)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        foreach (var resolved in ResolveExecutableCandidates(path.Trim()))
        {
            candidates.Add(ToInstall(resolved, source));
        }
    }

    private static void AddMacCandidates(List<IdaInstall> candidates)
    {
        foreach (var root in new[] { "/Applications", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Applications") })
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var app in SafeEnumerateDirectories(root, "IDA*.app"))
            {
                foreach (var executable in ResolveExecutableCandidates(app))
                {
                    candidates.Add(ToInstall(executable, "macOS applications"));
                }
            }
        }
    }

    private static void AddWindowsCandidates(List<IdaInstall> candidates)
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };

        foreach (var root in roots.Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path)))
        {
            foreach (var directory in SafeEnumerateDirectories(root, "IDA*"))
            {
                foreach (var executable in ResolveExecutableCandidates(directory))
                {
                    candidates.Add(ToInstall(executable, "windows program files"));
                }
            }
        }
    }

    private static void AddUnixCandidates(List<IdaInstall> candidates)
    {
        foreach (var root in new[] { "/opt", "/usr/local", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) })
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var directory in SafeEnumerateDirectories(root, "ida*"))
            {
                foreach (var executable in ResolveExecutableCandidates(directory))
                {
                    candidates.Add(ToInstall(executable, "unix search"));
                }
            }
        }
    }

    private static IEnumerable<string> ResolveExecutableCandidates(string path)
    {
        if (File.Exists(path))
        {
            yield return Path.GetFullPath(path);
            yield break;
        }

        if (!Directory.Exists(path))
        {
            yield return Path.GetFullPath(path);
            yield break;
        }

        if (path.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
        {
            var macOsDir = Path.Combine(path, "Contents", "MacOS");
            foreach (var name in PreferredExecutableNames())
            {
                var candidate = Path.Combine(macOsDir, name);
                if (File.Exists(candidate))
                {
                    yield return Path.GetFullPath(candidate);
                }
            }
            yield break;
        }

        foreach (var name in PreferredExecutableNames())
        {
            var candidate = Path.Combine(path, name);
            if (File.Exists(candidate))
            {
                yield return Path.GetFullPath(candidate);
            }
        }
    }

    private static IReadOnlyList<string> PreferredExecutableNames()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ["ida64.exe", "ida.exe", "idat64.exe", "idat.exe"]
            : ["ida64", "ida", "idat64", "idat"];
    }

    private static IdaInstall ToInstall(string executablePath, string source)
    {
        return new IdaInstall(
            executablePath,
            Path.GetFileName(executablePath),
            RuntimeInformation.OSDescription,
            source,
            File.Exists(executablePath));
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string root, string pattern)
    {
        try
        {
            return Directory.EnumerateDirectories(root, pattern, SearchOption.TopDirectoryOnly).ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static string NormalizeKey(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }
}
