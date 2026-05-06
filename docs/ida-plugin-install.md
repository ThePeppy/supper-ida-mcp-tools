# IDA Plugin Install

The plugin is installed into the user IDA plugins directory as:

- `supper_ida_mcp_plugin.py`: IDA loader file
- `supper_ida_plugin/`: copied plugin package

Install:

```bash
python3 ida-plugin/install.py
```

The installer archives known legacy `ida-mcp` plugin files (`ida_mcp.py`,
`ida_mcp/`, `mcp-plugin.py`) into `.supper_ida_mcp_legacy_backup/` under the
IDA plugins directory. This prevents IDA from loading an incompatible old plugin
beside the new center plugin after restart. Pass `--keep-legacy` only when you
intentionally need to keep the old plugin active.

Uninstall:

```bash
python3 ida-plugin/uninstall.py
```

Override the target plugins directory:

```bash
python3 ida-plugin/install.py --plugins-dir "/path/to/ida-user/plugins"
python3 ida-plugin/uninstall.py --plugins-dir "/path/to/ida-user/plugins"
```

Default plugin directories:

- macOS: `~/.idapro/plugins`
- Windows: `%APPDATA%\Hex-Rays\IDA Pro\plugins`
- Linux: `~/.idapro/plugins`

If `IDAUSR` is set, the installer uses `$IDAUSR/plugins`.
