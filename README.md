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

## Current Status

The first runnable path is implemented:

- `mcp-center` exposes stdio MCP tools and listens for IDA plugin TCP clients.
- `ida-plugin` registers one IDA window over TCP and handles center-issued tool calls.
- Closing the TCP connection removes the target from the center registry.

Run the center as an MCP stdio server:

```bash
dotnet run --project mcp-center/src/SupperIdaMcp.Center.App/SupperIdaMcp.Center.App.csproj -- --stdio
```

The TCP hub listens on `127.0.0.1:9399` by default.

Run with the local dashboard:

```bash
dotnet run --project mcp-center/src/SupperIdaMcp.Center.App/SupperIdaMcp.Center.App.csproj -- --stdio --ui
```

Dashboard URL: `http://127.0.0.1:9400/`.
