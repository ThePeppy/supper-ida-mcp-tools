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
- `analysis.list_functions`
- `analysis.get_function`
- `analysis.decompile`
- `analysis.disassemble`
- `analysis.xrefs`
- `analysis.list_strings`
- `analysis.list_imports`
- `analysis.get_bytes`
- `analysis.search_text`
- `analysis.rename`
- `analysis.set_comment`

## Analysis Tools

### `ida_list_functions`

Lists functions from one target database.

Arguments:

- `instanceId`
- `offset`
- `count`
- `filter`

### `ida_get_function`

Resolves one function by address, name, or address inside the function.

Arguments:

- `instanceId`
- `query`

### `ida_decompile`

Decompiles one function through Hex-Rays.

Arguments:

- `instanceId`
- `query`

### `ida_disassemble`

Returns disassembly lines for one function.

Arguments:

- `instanceId`
- `query`
- `maxLines`

### `ida_xrefs`

Lists code xrefs to, from, or both directions for one address.

Arguments:

- `instanceId`
- `query`
- `direction`
- `max`

### `ida_list_strings`

Lists strings from one target database.

Arguments:

- `instanceId`
- `offset`
- `count`
- `filter`

### `ida_list_imports`

Lists imports from one target database.

Arguments:

- `instanceId`
- `offset`
- `count`
- `filter`

### `ida_get_bytes`

Reads raw bytes from one target database.

Arguments:

- `instanceId`
- `address`
- `size`

### `ida_search_text`

Regex-searches generated disassembly text.

Arguments:

- `instanceId`
- `pattern`
- `max`

### `ida_rename`

Renames an address or function.

Arguments:

- `instanceId`
- `address`
- `newName`

### `ida_set_comment`

Sets a regular or repeatable comment.

Arguments:

- `instanceId`
- `address`
- `text`
- `repeatable`

### `ida_operation_log`

Lists recent center-side agent operation log entries.

Arguments:

- `limit`: optional maximum entry count.

## Routing Rule

All target-specific tools require `instanceId`. This lets an agent work across
multiple open IDA windows in the same task without reconfiguring MCP servers.
