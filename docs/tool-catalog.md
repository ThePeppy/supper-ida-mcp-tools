# Tool Catalog

The MCP client sees center-owned tools only. IDA plugin executors do not expose
MCP endpoints directly.

## Current MCP Tools

### `ida_list_targets`

Lists all registered IDA windows.

Arguments: none.

### `ida_get_target`

Gets one registered target.

Arguments:

- `instanceId`: target IDA window id.

### `ida_ping_target`

Sends a lightweight health command to one target.

Arguments:

- `instanceId`: target IDA window id.

### `ida_get_metadata`

Asks one target to return current binary and database metadata.

Arguments:

- `instanceId`: target IDA window id.

### `ida_call_tool`

Generic executor bridge for center-owned plugin tools.

Arguments:

- `instanceId`: target IDA window id.
- `tool`: plugin executor tool name.
- `arguments`: JSON object passed to the executor.

Current plugin executor tools:

- `target.ping`
- `target.get_metadata`

### `ida_operation_log`

Lists recent center-side agent operation log entries.

Arguments:

- `limit`: optional maximum entry count.

## Routing Rule

All target-specific tools require `instanceId`. This lets an agent work across
multiple open IDA windows in the same task without reconfiguring MCP servers.
