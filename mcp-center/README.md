# MCP Center Desktop

.NET desktop MCP center application.

This application owns the MCP endpoint, TCP hub, IDA process management, desktop UI, and operation logs.

## Run

Desktop mode:

```bash
dotnet run --project src/SupperIdaMcp.Center.Desktop/SupperIdaMcp.Center.Desktop.csproj
```

Start directly in the tray/menu bar:

```bash
dotnet run --project src/SupperIdaMcp.Center.Desktop/SupperIdaMcp.Center.Desktop.csproj -- --start-minimized
```

Default endpoints:

- MCP HTTP: `http://127.0.0.1:9401/mcp`
- IDA plugin TCP: `127.0.0.1:9399`

## Debug Entrypoint

`SupperIdaMcp.Center.App` is a development/debug host for stdio and diagnostics.
It is not the product entrypoint.

```bash
dotnet run --project src/SupperIdaMcp.Center.App/SupperIdaMcp.Center.App.csproj
```
