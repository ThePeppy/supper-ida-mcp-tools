# IDA Plugin

Python-only IDA Pro plugin executor.

This package connects to `mcp-center` over TCP and executes IDA tool calls on behalf of the center.

The plugin connects to `127.0.0.1:9399` by default and sends:

- `hello` on connect
- `heartbeat` while connected
- `tool_result` for center-issued `tool_call` requests

The center removes the target when the TCP connection closes.

## Install

Install the IDA loader into the current user's IDA plugins directory:

```bash
python3 ida-plugin/install.py
```

Override the plugins directory:

```bash
python3 ida-plugin/install.py --plugins-dir "/path/to/ida-user/plugins"
```

Uninstall:

```bash
python3 ida-plugin/uninstall.py
```
