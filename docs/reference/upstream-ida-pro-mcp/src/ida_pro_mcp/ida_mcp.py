"""IDA Pro MCP Plugin Loader

This file serves as the entry point for IDA Pro's plugin system.
It loads the actual implementation from the ida_mcp package.
"""

import sys
import threading
import idaapi
import ida_kernwin
import ida_netnode
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from . import ida_mcp


NETNODE_AUTOSTART = "$ ida_mcp.autostart"


def _get_autostart() -> bool:
    """Read the autostart preference from the IDB. Defaults to True."""
    node = ida_netnode.netnode(NETNODE_AUTOSTART)
    val = node.altval(0)  # 0 = not set, 1 = off, 2 = on
    return val != 1


def _set_autostart(enabled: bool):
    """Persist the autostart preference into the IDB."""
    node = ida_netnode.netnode(NETNODE_AUTOSTART, 0, True)
    node.altset(0, 1 if not enabled else 2)


def unload_package(package_name: str):
    """Remove every module that belongs to the package from sys.modules."""
    to_remove = [
        mod_name
        for mod_name in sys.modules
        if mod_name == package_name or mod_name.startswith(package_name + ".")
    ]
    for mod_name in to_remove:
        del sys.modules[mod_name]


CONFIG_ACTION_ID = "mcp:configure"
CONFIG_ACTION_LABEL = "MCP Configuration"


class MCPConfigForm(idaapi.Form):
    """Form to configure MCP server host and port."""

    def __init__(self, host: str, port: int, autostart: bool):
        form_str = r"""STARTITEM 0
MCP Server Configuration

<Host:{host}>
<Port:{port}>
<Autostart server when IDA opens:{autostart}>{checks}>
"""
        super().__init__(
            form_str,
            {
                "host": idaapi.Form.StringInput(value=host),
                "port": idaapi.Form.NumericInput(value=port, tp=idaapi.Form.FT_DEC),
                "checks": idaapi.Form.ChkGroupControl(("autostart",), value=1 if autostart else 0),
            },
        )


class MCPConfigHandler(idaapi.action_handler_t):
    def __init__(self, plugin: "MCP"):
        idaapi.action_handler_t.__init__(self)
        self.plugin = plugin

    def activate(self, ctx):
        old_host = self.plugin.host
        old_port = self.plugin.port
        old_autostart = self.plugin.autostart

        form = MCPConfigForm(self.plugin.host, self.plugin.port, self.plugin.autostart)
        form.Compile()
        ok = form.Execute()
        if ok != 1:
            form.Free()
            return 0

        host = form.host.value
        port = form.port.value
        autostart = bool(form.checks.value & 1)
        form.Free()

        if port < 1 or port > 65535:
            print(f"[MCP] Invalid port: {port}")
            return 0

        if autostart != old_autostart:
            self.plugin.autostart = autostart
            _set_autostart(autostart)
            print(f"[MCP] Autostart {'enabled' if autostart else 'disabled'}")

        if host == old_host and port == old_port:
            if autostart == old_autostart:
                print(f"[MCP] Configuration unchanged: {host}:{port}")
            return 1

        self.plugin.host = host
        self.plugin.port = port
        print(f"[MCP] Configuration updated: {host}:{port}")

        # Apply new endpoint immediately if the server is running.
        if self.plugin.mcp is not None:
            print("[MCP] Applying configuration change without manual restart...")
            self.plugin.run(0)
        return 1

    def update(self, ctx):
        return idaapi.AST_ENABLE_ALWAYS


class MCPUIHooks(ida_kernwin.UI_Hooks):
    """Defers menu attachment and autostart until the UI is fully ready."""

    def __init__(self, plugin: "MCP"):
        super().__init__()
        self.plugin = plugin

    def ready_to_run(self):
        ida_kernwin.attach_action_to_menu(
            "Edit/Plugins/", CONFIG_ACTION_ID, idaapi.SETMENU_APP
        )
        # Skip autostart when running under idalib – the idalib_server manages
        # the MCP server lifecycle itself and would otherwise hit a port conflict
        # because unload_package creates a separate MCP_SERVER instance.
        if self.plugin.autostart and ida_kernwin.is_idaq():
            print("[MCP] Autostarting server...")
            self.plugin.run(0)
        self.unhook()


class MCP(idaapi.plugin_t):
    flags = idaapi.PLUGIN_KEEP
    comment = "MCP Plugin"
    help = "MCP"
    wanted_name = "MCP"
    wanted_hotkey = "Ctrl-Alt-M"

    DEFAULT_HOST = "127.0.0.1"
    DEFAULT_PORT = 13337

    def init(self):
        hotkey = MCP.wanted_hotkey.replace("-", "+")
        if __import__("sys").platform == "darwin":
            hotkey = hotkey.replace("Alt", "Option")

        self.mcp: "ida_mcp.rpc.McpServer | None" = None
        self.host = self.DEFAULT_HOST
        self.port = self.DEFAULT_PORT
        self.autostart = _get_autostart()

        if self.autostart and ida_kernwin.is_idaq():
            print("[MCP] Plugin loaded, server will start automatically")
        elif not ida_kernwin.is_idaq():
            print("[MCP] Plugin loaded (idalib mode, server managed externally)")
        else:
            print(
                f"[MCP] Plugin loaded, use Edit -> Plugins -> MCP ({hotkey}) to start the server"
            )

        # Register a separate menu item for host/port configuration
        ida_kernwin.register_action(
            ida_kernwin.action_desc_t(
                CONFIG_ACTION_ID,
                CONFIG_ACTION_LABEL,
                MCPConfigHandler(self),
            )
        )
        # Defer menu attachment and autostart until the UI is fully initialized
        self._ui_hooks = MCPUIHooks(self)
        self._ui_hooks.hook()

        return idaapi.PLUGIN_KEEP

    def _unregister_instance(self):
        self._stop_registry_registration()
        port = getattr(self, "_registered_port", None)
        if port is not None:
            try:
                if TYPE_CHECKING:
                    from .ida_mcp.discovery import unregister_instance
                else:
                    from ida_mcp.discovery import unregister_instance
                unregister_instance(port)
            except Exception as e:
                print(f"[MCP] Instance unregistration failed: {e}")
            self._registered_port = None

    def _build_registry_payload(self, port: int) -> dict:
        """Collect IDB metadata on the IDA main thread for registry registration."""
        import os
        import idc
        import ida_nalt

        def safe(call, default=None):
            try:
                value = call()
                return default if value is None else value
            except Exception:
                return default

        def json_safe(value):
            if isinstance(value, bytes):
                return value.hex()
            if value is None or isinstance(value, (str, int, float, bool)):
                return value
            return str(value)

        binary = safe(ida_nalt.get_root_filename, "") or ""
        idb_path = safe(idc.get_idb_path, "") or ""
        input_path = safe(ida_nalt.get_input_file_path, "") or ""
        metadata = {
            "module": binary,
            "idb_path": idb_path,
            "input_path": input_path,
            "imagebase": safe(lambda: hex(idaapi.get_imagebase()), ""),
        }

        try:
            import ida_ida

            metadata.update(
                {
                    "processor": safe(ida_ida.inf_get_procname, ""),
                    "min_ea": safe(lambda: hex(ida_ida.inf_get_min_ea()), ""),
                    "max_ea": safe(lambda: hex(ida_ida.inf_get_max_ea()), ""),
                    "start_ea": safe(lambda: hex(ida_ida.inf_get_start_ea()), ""),
                    "md5": safe(ida_ida.inf_get_md5, ""),
                    "sha256": safe(ida_ida.inf_get_sha256, ""),
                }
            )
        except Exception:
            pass

        try:
            import ida_auto

            metadata["auto_analysis_ready"] = safe(ida_auto.auto_is_ok, None)
        except Exception:
            pass

        metadata = {key: json_safe(value) for key, value in metadata.items()}

        return {
            "host": self.host,
            "port": port,
            "pid": os.getpid(),
            "backend": "ida",
            "binary": binary,
            "idb_path": idb_path,
            "input_path": input_path,
            "metadata": metadata,
        }

    def _start_registry_registration(self, payload: dict):
        """Continuously register with the fixed local registry when available."""
        self._registry_stop_event = threading.Event()
        self._registry_payload = dict(payload)
        self._registry_registered_once = False
        stop_event = self._registry_stop_event

        def worker():
            try:
                if TYPE_CHECKING:
                    from .ida_mcp.registry_client import register_instance
                else:
                    from ida_mcp.registry_client import register_instance
            except Exception as e:
                print(f"[MCP] Registry client unavailable: {e}")
                return

            logged_unavailable = False
            while not stop_event.is_set():
                ok, response = register_instance(payload, timeout=1.5)
                if ok:
                    if not self._registry_registered_once:
                        target = response.get("target", {}) if isinstance(response, dict) else {}
                        alias = target.get("alias") or payload.get("binary") or payload.get("port")
                        print(
                            f"[MCP] Registered with central registry: {alias} "
                            f"(127.0.0.1:9399)"
                        )
                    self._registry_registered_once = True
                    logged_unavailable = False
                    delay = 15.0
                else:
                    if not logged_unavailable:
                        err = response.get("error", "unknown error") if isinstance(response, dict) else response
                        print(
                            "[MCP] Central registry unavailable at "
                            f"127.0.0.1:9399 ({err}); retrying in background"
                        )
                    logged_unavailable = True
                    delay = 5.0
                stop_event.wait(delay)

        self._registry_thread = threading.Thread(
            target=worker,
            name="ida-mcp-registry-heartbeat",
            daemon=True,
        )
        self._registry_thread.start()

    def _stop_registry_registration(self):
        stop_event = getattr(self, "_registry_stop_event", None)
        if stop_event is not None:
            stop_event.set()

        thread = getattr(self, "_registry_thread", None)
        if thread is not None and thread.is_alive():
            thread.join(timeout=0.5)

        payload = getattr(self, "_registry_payload", None)
        if payload:
            try:
                if TYPE_CHECKING:
                    from .ida_mcp.registry_client import unregister_instance
                else:
                    from ida_mcp.registry_client import unregister_instance

                unregister_instance(payload, timeout=1.0)
            except Exception:
                pass

        self._registry_stop_event = None
        self._registry_thread = None
        self._registry_payload = None

    def run(self, arg):
        if self.mcp:
            self._unregister_instance()
            self.mcp.stop()
            self.mcp = None

        # HACK: ensure fresh load of ida_mcp package
        unload_package("ida_mcp")
        if TYPE_CHECKING:
            from .ida_mcp import MCP_SERVER, IdaMcpHttpRequestHandler, set_local_instance
        else:
            from ida_mcp import MCP_SERVER, IdaMcpHttpRequestHandler, set_local_instance

        port = self.port
        max_port = port + 100
        while port < max_port:
            try:
                MCP_SERVER.serve(
                    self.host, port, request_handler=IdaMcpHttpRequestHandler
                )
                print(f"  Config: http://{self.host}:{port}/config.html")
                self.mcp = MCP_SERVER
                set_local_instance(self.host, port)
                self._register_instance(port)
                return
            except OSError as e:
                if e.errno in (48, 98, 10048):  # Address already in use
                    port += 1
                else:
                    raise
        print(f"[MCP] Error: No available port in range {self.port}-{max_port - 1}")

    def _register_instance(self, port: int):
        try:
            if TYPE_CHECKING:
                from .ida_mcp.discovery import register_instance
            else:
                from ida_mcp.discovery import register_instance
            payload = self._build_registry_payload(port)
            binary = payload.get("binary", "")
            idb_path = payload.get("idb_path", "")
            file_path = register_instance(
                host=self.host,
                port=port,
                pid=payload.get("pid", 0),
                binary=binary,
                idb_path=idb_path,
            )
            self._registered_port = port
            print(f"[MCP] Registered instance: {binary} (pid={payload.get('pid')}, port={port})")
            print(f"  Discovery file: {file_path}")
            self._start_registry_registration(payload)
        except Exception as e:
            import traceback
            print(f"[MCP] Instance registration failed: {e}")
            traceback.print_exc()

    def term(self):
        if hasattr(self, "_ui_hooks"):
            self._ui_hooks.unhook()
        ida_kernwin.unregister_action(CONFIG_ACTION_ID)
        self._unregister_instance()
        if self.mcp:
            self.mcp.stop()


def PLUGIN_ENTRY():
    return MCP()
