# Changelog

## 0.1.4

### New Features

- **opencode one-click configuration**: The desktop center now auto-detects opencode and supports writing MCP configuration to `opencode.json` with a single click. opencode connects via Streamable HTTP, no stdio bridge required.
- **Plugin directory folder picker**: Added a "Browse" button to the IDA Plugin settings card, allowing users to manually select the IDA plugins directory through a system folder dialog. The selection is persisted across restarts.

### Bug Fixes

- **Windows plugin installation directory**: Fixed a critical issue where the IDA plugin was installed to `%APPDATA%\Hex-Rays\IDA Pro\plugins` on Windows, which IDA Pro does not load plugins from. The desktop center and the Python CLI installer now automatically detect the IDA installation directory (e.g. `C:\Program Files\IDA Professional 9.3\plugins`) and install to the correct `plugins` subfolder. Falls back to the legacy APPDATA path only when no IDA installation is found.

### Improvements

- Plugin directory resolution now follows a clear priority chain: user preference > `IDAUSR` environment variable > auto-detected IDA installation directory > platform default fallback.
- Updated bundled plugin version to 0.1.4.

## 0.1.3

- Added batch-mode and Python-side timeout protection around IDA plugin tool execution to reduce UI stalls.
- Reworked `ida_search_text` again to avoid unbounded native text searches on large databases; search now returns partial results with a cursor when scan or time budgets are reached.
- Added `scanLimit` and `timeLimitMs` controls to `ida_search_text` for safer paging on very large IDBs.

## 0.1.2

- Reworked `ida_search_text` to use IDA's native `ida_search.find_text()` scan instead of Python-side full-function disassembly scanning.
- Added cursor pagination, literal-by-default matching, optional regex mode, case sensitivity, comment/disassembly filtering, and executable-segment filtering for text search.
- Bumped the bundled IDA plugin version so Settings can correctly require plugin reinstall after this search fix.

## 0.1.1

- Fixed Streamable HTTP MCP startup compatibility with Codex by accepting JSON-RPC notifications with HTTP 202 responses.
- Added detailed MCP operation log capture for tool name, request payload, response payload, and per-call detail inspection.
- Added scrollable operation log browsing, row selection, copy details, and clear log actions in the desktop center.
- Fixed release publishing workflow repository context so tag builds can create GitHub Releases correctly.

## 0.1.0

- Initial rewrite scaffold.
- Archived upstream IDA Pro MCP reference code under `docs/reference/upstream-ida-pro-mcp/`.
- Added stdio MCP center tools for target listing, target lookup, target ping, metadata calls, generic tool calls, and operation logs.
- Added TCP request/response dispatch between `mcp-center` and `ida-plugin`.
- Added core IDA analysis tools for functions, decompilation, disassembly, xrefs, strings, imports, byte reads, text search, renaming, and comments.
- Added cross-platform IDA discovery, launch, launched process listing, and target close tools.
- Added local dashboard for registered targets, active agent calls, launched processes, installation discovery, and operation logs.
- Added IDA plugin install and uninstall loader scripts.
- Added cross-platform desktop center as the product entrypoint with built-in local MCP HTTP endpoint.
- Added Settings support for plugin install/version checks and Codex/Claude MCP configuration.
- Added desktop app icons from the shared image assets and cross-platform tray operation for Windows/macOS.
