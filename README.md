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

- `mcp-center` is a cross-platform desktop application.
- The desktop app exposes the local MCP endpoint and listens for IDA plugin TCP clients.
- `ida-plugin` registers one IDA window over TCP and handles center-issued tool calls.
- Closing the TCP connection removes the target from the center registry.

Install the IDA plugin loader:

```bash
python3 ida-plugin/install.py
```

Run the desktop center:

```bash
dotnet run --project mcp-center/src/SupperIdaMcp.Center.Desktop/SupperIdaMcp.Center.Desktop.csproj
```

Use `-- --start-minimized` to start the desktop center directly in the
Windows tray or macOS menu bar.

Default endpoints:

- MCP HTTP: `http://127.0.0.1:9401/mcp`
- IDA plugin TCP: `127.0.0.1:9399`

The desktop Settings tab can install or repair the IDA plugin loader, verify
that the installed loader belongs to this product/version, and configure Codex
or Claude MCP settings. See [docs/agent-configuration.md](docs/agent-configuration.md).
