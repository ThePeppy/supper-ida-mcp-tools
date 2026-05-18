using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SupperIdaMcp.Center.Desktop.Setup;

internal sealed class AgentConfigService
{
    private readonly RepositoryPaths _paths;
    private readonly string _mcpEndpoint;

    public AgentConfigService(RepositoryPaths paths, string mcpEndpoint)
    {
        _paths = paths;
        _mcpEndpoint = mcpEndpoint;
    }

    public IReadOnlyList<AgentConfigStatus> Detect()
    {
        return
        [
            DetectCodex(),
            .. DetectClaudeDesktopConfigs(),
            .. DetectOpencodeConfigs()
        ];
    }

    public AgentConfigStatus Configure(string agentName, string configPath)
    {
        if (agentName.StartsWith("Codex", StringComparison.OrdinalIgnoreCase))
        {
            ConfigureCodex(configPath);
            return DetectCodex(configPath);
        }

        if (agentName.StartsWith("Claude", StringComparison.OrdinalIgnoreCase))
        {
            ConfigureClaudeDesktop(configPath);
            return DetectClaudeDesktop(configPath);
        }

        if (agentName.StartsWith("opencode", StringComparison.OrdinalIgnoreCase))
        {
            ConfigureOpencode(configPath);
            return DetectOpencode(configPath);
        }

        throw new InvalidOperationException($"Unsupported agent: {agentName}");
    }

    public string ManualHttpSnippet()
    {
        return $$"""
For MCP clients that support Streamable HTTP, add:

Name: {{ProductInfo.AgentServerName}}
URL:  {{_mcpEndpoint}}
""";
    }

    public string ManualStdioSnippet()
    {
        var bridge = GetBridgeLaunchCommand();
        if (bridge is null)
        {
            return "Stdio bridge is unavailable because no packaged bridge executable or repository bridge project was found.";
        }

        return bridge.ToManualSnippet(_mcpEndpoint);
    }

    public string? GetBridgeProjectPath()
    {
        return _paths.BridgeProjectPath is not null && File.Exists(_paths.BridgeProjectPath)
            ? _paths.BridgeProjectPath
            : null;
    }

    public BridgeLaunchCommand? GetBridgeLaunchCommand()
    {
        return BridgeLaunchCommandResolver.Resolve(_paths);
    }

    private AgentConfigStatus DetectCodex(string? overridePath = null)
    {
        var configPath = overridePath ?? Path.Combine(Home(), ".codex", "config.toml");
        var exists = File.Exists(configPath);
        var text = exists ? File.ReadAllText(configPath) : string.Empty;
        var isConfigured = text.Contains($"[mcp_servers.{ProductInfo.AgentServerName}]", StringComparison.Ordinal)
            && text.Contains(_mcpEndpoint, StringComparison.Ordinal);
        var hasLegacy = text.Contains("[mcp_servers.ida-pro-mcp]", StringComparison.Ordinal)
            || text.Contains("127.0.0.1:13337", StringComparison.Ordinal);
        var summary = isConfigured
            ? "Configured for this desktop center."
            : hasLegacy
                ? "Legacy ida-pro-mcp config detected. Add the new center config and remove old entries when ready."
                : exists ? "Config file found, center not configured." : "Codex config file will be created.";

        return new AgentConfigStatus(
            "Codex CLI / Codex Desktop",
            configPath,
            exists,
            isConfigured,
            hasLegacy,
            CanAutoConfigure: true,
            summary,
            CodexSnippet());
    }

    private IEnumerable<AgentConfigStatus> DetectClaudeDesktopConfigs()
    {
        foreach (var path in ClaudeConfigPaths())
        {
            yield return DetectClaudeDesktop(path);
        }
    }

    private AgentConfigStatus DetectClaudeDesktop(string configPath)
    {
        var exists = File.Exists(configPath);
        var text = exists ? File.ReadAllText(configPath) : string.Empty;
        var isConfigured = text.Contains($"\"{ProductInfo.AgentServerName}\"", StringComparison.Ordinal)
            && text.Contains(_mcpEndpoint, StringComparison.Ordinal)
            && text.Contains("SupperIdaMcp.Center.Bridge", StringComparison.Ordinal);
        var hasLegacy = text.Contains("\"ida-pro-mcp\"", StringComparison.Ordinal)
            || text.Contains("13337", StringComparison.Ordinal);
        var summary = isConfigured
            ? "Configured through the local stdio bridge."
            : hasLegacy
                ? "Legacy IDA MCP config detected. Add the new bridge config and remove old entries when ready."
                : exists ? "Config file found, center bridge not configured." : "Config file will be created.";

        return new AgentConfigStatus(
            Path.GetDirectoryName(configPath)?.EndsWith("Claude-3p", StringComparison.OrdinalIgnoreCase) == true
                ? "Claude Desktop (3p profile)"
                : "Claude Desktop",
            configPath,
            exists,
            isConfigured,
            hasLegacy,
            CanAutoConfigure: BridgeLaunchCommand() is not null,
            summary,
            ClaudeSnippet());
    }

    private void ConfigureCodex(string configPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        var text = File.Exists(configPath) ? File.ReadAllText(configPath) : string.Empty;
        if (!string.IsNullOrWhiteSpace(text))
        {
            Backup(configPath);
        }

        var cleaned = RemoveTomlSection(text, $"mcp_servers.{ProductInfo.AgentServerName}").TrimEnd();
        var next = cleaned.Length == 0
            ? CodexSnippet()
            : cleaned + Environment.NewLine + Environment.NewLine + CodexSnippet();
        File.WriteAllText(configPath, next + Environment.NewLine, Encoding.UTF8);
    }

    private void ConfigureClaudeDesktop(string configPath)
    {
        var bridge = BridgeLaunchCommand()
            ?? throw new InvalidOperationException("Bridge executable or project path was not found.");
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        if (File.Exists(configPath))
        {
            Backup(configPath);
        }

        JsonObject root;
        if (File.Exists(configPath) && !string.IsNullOrWhiteSpace(File.ReadAllText(configPath)))
        {
            root = JsonNode.Parse(File.ReadAllText(configPath)) as JsonObject
                ?? throw new InvalidOperationException("Claude config root must be a JSON object.");
        }
        else
        {
            root = new JsonObject();
        }

        var servers = root["mcpServers"] as JsonObject;
        if (servers is null)
        {
            servers = new JsonObject();
            root["mcpServers"] = servers;
        }

        var bridgeArgs = new JsonArray();
        foreach (var arg in bridge.Args)
        {
            bridgeArgs.Add(arg);
        }

        bridgeArgs.Add("--endpoint");
        bridgeArgs.Add(_mcpEndpoint);

        servers[ProductInfo.AgentServerName] = new JsonObject
        {
            ["command"] = bridge.Command,
            ["args"] = bridgeArgs
        };

        File.WriteAllText(
            configPath,
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine,
            Encoding.UTF8);
    }

    private string CodexSnippet()
    {
        return $$"""
[mcp_servers.{{ProductInfo.AgentServerName}}]
url = "{{_mcpEndpoint}}"
""";
    }

    private string ClaudeSnippet()
    {
        var bridge = BridgeLaunchCommand();
        var command = bridge?.Command ?? "/path/to/SupperIdaMcp.Center.Bridge";
        var commandJson = JsonSerializer.Serialize(command);
        var args = bridge?.Args ?? [];
        var argsJson = string.Join(
            "," + Environment.NewLine,
            args.Append("--endpoint").Append(_mcpEndpoint).Select(arg => $"        {JsonSerializer.Serialize(arg)}"));
        return $$"""
{
  "mcpServers": {
    "{{ProductInfo.AgentServerName}}": {
      "command": {{commandJson}},
      "args": [
{{argsJson}}
      ]
    }
  }
}
""";
    }

    private string? BridgeProjectPath()
    {
        return GetBridgeProjectPath();
    }

    private BridgeLaunchCommand? BridgeLaunchCommand()
    {
        return GetBridgeLaunchCommand();
    }

    private static IReadOnlyList<string> ClaudeConfigPaths()
    {
        if (OperatingSystem.IsMacOS())
        {
            return
            [
                Path.Combine(Home(), "Library", "Application Support", "Claude", "claude_desktop_config.json"),
                Path.Combine(Home(), "Library", "Application Support", "Claude-3p", "claude_desktop_config.json")
            ];
        }

        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return [Path.Combine(appData, "Claude", "claude_desktop_config.json")];
        }

        return [Path.Combine(Home(), ".config", "Claude", "claude_desktop_config.json")];
    }

    private static string RemoveTomlSection(string text, string sectionName)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var lines = text.Replace("\r\n", "\n").Split('\n');
        var builder = new StringBuilder();
        var skipping = false;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed == $"[{sectionName}]")
            {
                skipping = true;
                continue;
            }

            if (skipping && trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
            {
                skipping = false;
            }

            if (!skipping)
            {
                builder.AppendLine(line);
            }
        }

        return builder.ToString();
    }

    private static void Backup(string path)
    {
        var backup = path + ".bak-" + DateTimeOffset.Now.ToString("yyyyMMddHHmmss");
        File.Copy(path, backup, overwrite: false);
    }

    private static string Home()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private IEnumerable<AgentConfigStatus> DetectOpencodeConfigs()
    {
        foreach (var path in OpencodeConfigPaths())
        {
            yield return DetectOpencode(path);
        }
    }

    private AgentConfigStatus DetectOpencode(string configPath)
    {
        var exists = File.Exists(configPath);
        var text = exists ? File.ReadAllText(configPath) : string.Empty;
        var isConfigured = text.Contains($"\"{ProductInfo.AgentServerName}\"", StringComparison.Ordinal)
            && text.Contains(_mcpEndpoint, StringComparison.Ordinal);
        var summary = isConfigured
            ? "Configured for this desktop center."
            : exists ? "Config file found, center not configured." : "Config file will be created.";

        return new AgentConfigStatus(
            "opencode",
            configPath,
            exists,
            isConfigured,
            HasLegacyConfig: false,
            CanAutoConfigure: true,
            summary,
            OpencodeSnippet());
    }

    private void ConfigureOpencode(string configPath)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(configPath)!);
        if (File.Exists(configPath))
        {
            Backup(configPath);
        }

        JsonObject root;
        if (File.Exists(configPath) && !string.IsNullOrWhiteSpace(File.ReadAllText(configPath)))
        {
            root = JsonNode.Parse(File.ReadAllText(configPath)) as JsonObject
                ?? throw new InvalidOperationException("opencode config root must be a JSON object.");
        }
        else
        {
            root = new JsonObject();
        }

        var mcp = root["mcp"] as JsonObject;
        if (mcp is null)
        {
            mcp = new JsonObject();
            root["mcp"] = mcp;
        }

        mcp[ProductInfo.AgentServerName] = new JsonObject
        {
            ["type"] = "remote",
            ["url"] = _mcpEndpoint,
            ["enabled"] = true
        };

        File.WriteAllText(
            configPath,
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine,
            Encoding.UTF8);
    }

    private string OpencodeSnippet()
    {
        return $$"""
{
  "mcp": {
    "{{ProductInfo.AgentServerName}}": {
      "type": "remote",
      "url": "{{_mcpEndpoint}}",
      "enabled": true
    }
  }
}
""";
    }

    private static IReadOnlyList<string> OpencodeConfigPaths()
    {
        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return [System.IO.Path.Combine(appData, "opencode", "opencode.json")];
        }

        return [System.IO.Path.Combine(Home(), ".config", "opencode", "opencode.json")];
    }
}
