# Changelog

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
