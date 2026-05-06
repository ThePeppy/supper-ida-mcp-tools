# Architecture

## Target Design

```text
MCP Client / Agent
        |
        | MCP
        v
mcp-center (.NET)
        |
        | TCP JSON-RPC
        v
ida-plugin Python executors inside IDA windows
```

## Responsibilities

### mcp-center

- Exposes the only MCP endpoint used by agents.
- Owns tool schemas and tool registration.
- Tracks IDA targets and their health.
- Routes tool calls to a target IDA executor.
- Starts and stops IDA processes on macOS and Windows.
- Records operation logs and current agent activity.

### ida-plugin

- Runs inside IDA Pro.
- Connects to the center over TCP.
- Reports database metadata and heartbeat state.
- Executes tool calls on the IDA main thread.
- Returns structured results.

The plugin does not expose MCP directly.
