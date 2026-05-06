# Supper IDA MCP Tools

Cross-platform multi-window IDA Pro MCP tooling.

## Project Layout

```text
ida-plugin/   Python plugin installed into IDA Pro
mcp-center/   .NET MCP center, TCP hub, IDA process manager, and desktop UI
docs/         Architecture, protocol, and usage documentation
images/       Screenshots and visual assets
```

## Architecture

Codex or another MCP client connects only to the MCP Center. IDA windows run a lightweight plugin executor that maintains a TCP connection to the Center. The Center owns tool registration, target routing, IDA process management, health state, and operation logs.

See `docs/architecture.md` and `docs/protocol.md` for the design notes.
