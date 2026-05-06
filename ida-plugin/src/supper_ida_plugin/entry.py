"""IDA plugin entrypoint.

Actual IDA SDK imports stay lazy so the package remains importable outside IDA
for tests and packaging checks.
"""

from __future__ import annotations


def PLUGIN_ENTRY():  # noqa: N802 - IDA requires this exact name.
    import idaapi  # type: ignore

    class SupperIdaPlugin(idaapi.plugin_t):
        flags = idaapi.PLUGIN_KEEP
        comment = "Supper IDA MCP executor"
        help = "Supper IDA MCP executor"
        wanted_name = "Supper IDA MCP"
        wanted_hotkey = ""
        _client = None

        def init(self):
            from supper_ida_plugin.transport.tcp_client import CenterTcpClient

            self._client = CenterTcpClient()
            self._client.start()
            return idaapi.PLUGIN_KEEP

        def run(self, arg):
            print("[Supper IDA MCP] TCP executor is running")

        def term(self):
            if self._client is not None:
                self._client.stop()
                self._client = None

    return SupperIdaPlugin()
