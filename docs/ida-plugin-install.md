# IDA Plugin Install

The plugin is installed as a small loader file in the user IDA plugins
directory. The loader points back to `ida-plugin/src`, so Python plugin code
stays inside the `ida-plugin` project tree.

Install:

```bash
python3 ida-plugin/install.py
```

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

- macOS: `~/Library/Application Support/Hex-Rays/IDA Pro/plugins`
- Windows: `%APPDATA%\Hex-Rays\IDA Pro\plugins`
- Linux: `~/.idapro/plugins`

If `IDAUSR` is set, the installer uses `$IDAUSR/plugins`.
