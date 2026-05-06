# Agent Configuration

The product entrypoint is the `mcp-center` desktop app. Agents should connect to
that running desktop center instead of starting another center instance.

## HTTP-Capable Agents

Use the local MCP HTTP endpoint:

```text
Name: supper-ida-mcp-center
URL:  http://127.0.0.1:9401/mcp
```

## Stdio-Only Agents

Use the bridge. The bridge reads JSON-RPC from stdio and forwards it to the
desktop center HTTP endpoint.

```text
command: dotnet
args:
  - run
  - --project
  - /path/to/mcp-center/src/SupperIdaMcp.Center.Bridge/SupperIdaMcp.Center.Bridge.csproj
  - --
  - --endpoint
  - http://127.0.0.1:9401/mcp
```

## Codex

Settings can update `~/.codex/config.toml`.

Manual snippet:

```toml
[mcp_servers.supper-ida-mcp-center]
url = "http://127.0.0.1:9401/mcp"
```

The Settings page also detects legacy `ida-pro-mcp` entries so users can remove
old MCP server definitions when ready.

## Claude Desktop

Settings can update `claude_desktop_config.json`.

Manual snippet:

```json
{
  "mcpServers": {
    "supper-ida-mcp-center": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/path/to/mcp-center/src/SupperIdaMcp.Center.Bridge/SupperIdaMcp.Center.Bridge.csproj",
        "--",
        "--endpoint",
        "http://127.0.0.1:9401/mcp"
      ]
    }
  }
}
```

Before modifying an existing config file, Settings writes a timestamped `.bak`
backup next to the original file.
