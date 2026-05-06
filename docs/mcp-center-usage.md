# MCP Center Usage

## Desktop Program

`mcp-center` is a desktop program. It is the main product entrypoint and starts
the center services when the window opens.

```bash
dotnet run --project mcp-center/src/SupperIdaMcp.Center.Desktop/SupperIdaMcp.Center.Desktop.csproj
```

The desktop window shows:

- registered IDA windows and health
- active agent calls
- launched IDA processes
- discovered IDA installations
- operation logs
- close action for registered targets

## Local Endpoints

The desktop app starts these local endpoints:

- MCP HTTP endpoint: `http://127.0.0.1:9401/mcp`
- Health endpoint: `http://127.0.0.1:9401/health`
- IDA plugin TCP endpoint: `127.0.0.1:9399`

IDA plugin windows always connect to `127.0.0.1:9399`, so users do not configure
each IDA window.

For development only, ports can be overridden:

```bash
dotnet run --project mcp-center/src/SupperIdaMcp.Center.Desktop/SupperIdaMcp.Center.Desktop.csproj -- --tcp-port 19520 --mcp-port 19521
```

## MCP Client Configuration

Only configure the center in the MCP client. Do not configure every IDA window.

The center exposes stable MCP tools. Each target-specific tool accepts
`instanceId`, which lets an agent switch freely between multiple open IDA
windows.

Automation tools can also launch IDA:

- `ida_find_installations`
- `ida_launch_file`
- `ida_list_launched_processes`
- `ida_close_target`

## Development Debug Host

`SupperIdaMcp.Center.App` is retained as a development/debug entrypoint for
stdio and console diagnostics. It is not the desktop product entrypoint.

```bash
dotnet run --project mcp-center/src/SupperIdaMcp.Center.App/SupperIdaMcp.Center.App.csproj -- --stdio
```
