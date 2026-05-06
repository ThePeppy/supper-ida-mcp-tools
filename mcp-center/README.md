# MCP Center

.NET-only MCP center application.

This application owns the MCP endpoint, TCP hub, IDA process management, desktop UI, and operation logs.

## Run

MCP stdio mode:

```bash
dotnet run --project src/SupperIdaMcp.Center.App/SupperIdaMcp.Center.App.csproj -- --stdio
```

Diagnostics mode:

```bash
dotnet run --project src/SupperIdaMcp.Center.App/SupperIdaMcp.Center.App.csproj
```

Default TCP hub: `127.0.0.1:9399`.
