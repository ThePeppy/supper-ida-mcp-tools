# MCP Center Usage

## Run as an MCP Server

The center can run as a stdio MCP server while also listening for IDA plugin TCP
connections.

```bash
dotnet run --project mcp-center/src/SupperIdaMcp.Center.App/SupperIdaMcp.Center.App.csproj -- --stdio
```

The TCP hub defaults to `127.0.0.1:9399`. The IDA plugin uses this fixed address
so users do not need to configure every IDA window.

Optional overrides:

```bash
dotnet run --project mcp-center/src/SupperIdaMcp.Center.App/SupperIdaMcp.Center.App.csproj -- --stdio --host 127.0.0.1 --port 9399
```

Pass an explicit IDA executable or app bundle when automatic discovery is not
enough:

```bash
dotnet run --project mcp-center/src/SupperIdaMcp.Center.App/SupperIdaMcp.Center.App.csproj -- --stdio --ida-path "/Applications/IDA Professional 9.0.app"
```

## MCP Client Configuration

Only configure the center in the MCP client. Do not configure every IDA window.

The center exposes stable MCP tools. Each tool accepts an `instanceId` when it
needs to operate on a specific IDA window.

Automation tools can also launch IDA:

- `ida_find_installations`
- `ida_launch_file`
- `ida_list_launched_processes`
- `ida_close_target`

## Console Diagnostics Mode

For local diagnostics without MCP stdio:

```bash
dotnet run --project mcp-center/src/SupperIdaMcp.Center.App/SupperIdaMcp.Center.App.csproj
```

This mode prints the registered target count every few seconds. It is not an MCP
stdio mode and should not be used directly by agents.
