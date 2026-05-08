# Changelog

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
